using Microsoft.Extensions.Logging;
using WormForaging.Core;
using WormForaging.Core.Rng;
using WormForaging.Harness.Hosting;

namespace WormForaging.Harness.Experiment;

/// <summary>
/// Runs the Phase 1 baseline experiment: every seed under each of the three learning modes, pairing
/// them by seed so the only difference between modes is the learning rule.
/// </summary>
/// <remarks>
/// <para>
/// The comparison is <b>paired</b>: a given seed constructs an identical world, network, and initial
/// readout for all three modes (the RNG draws the same initial stream), so Full, Hebbian, and Frozen
/// start from exactly the same conditions. Their trajectories then diverge only because the learning
/// rule changes the readout weights differently — which is precisely the effect under study.
/// </para>
/// <para>
/// The runner only measures. It makes no claim about which mode wins; it writes the numbers and logs
/// a compact table, and interpretation is left to the write-up. Determinism is inherited from Core:
/// the same seed and config reproduce a run bit-for-bit, so these CSVs are reproducible.
/// </para>
/// </remarks>
internal sealed class ExperimentRunner
{
    /// <summary>The three modes under comparison: the rule and its two controls.</summary>
    private static readonly LearningMode[] Modes =
        [LearningMode.Full, LearningMode.Hebbian, LearningMode.Frozen];

    private readonly ExperimentSettings _settings;
    private readonly SimConfig _config;
    private readonly ILogger<ExperimentRunner> _logger;

    /// <summary>Creates the runner.</summary>
    /// <param name="settings">The resolved experiment parameters.</param>
    /// <param name="config">The base simulation config (shared, immutable).</param>
    /// <param name="logger">The logger for progress and the headline table.</param>
    public ExperimentRunner(ExperimentSettings settings, SimConfig config, ILogger<ExperimentRunner> logger)
    {
        _settings = settings;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Runs every (seed, mode) combination, writes the three CSVs, and logs the aggregate comparison.
    /// </summary>
    public void Run()
    {
        Directory.CreateDirectory(_settings.OutputDirectory);

        _logger.LogInformation(
            "Phase 1 baseline: {Seeds} seeds x {Modes} modes x {Steps} steps = {Runs} runs. Output: {Out}",
            _settings.Seeds, Modes.Length, _settings.Steps, _settings.Seeds * Modes.Length, _settings.OutputDirectory);

        var results = new List<RunResult>(_settings.Seeds * Modes.Length);
        var started = DateTimeOffset.UtcNow;

        foreach (ulong seed in _settings.SeedSequence())
        {
            foreach (LearningMode mode in Modes)
            {
                RunResult result = RunOne(seed, mode);
                results.Add(result);

                _logger.LogDebug(
                    "seed {Seed} {Mode,-7}: meals={Meals,4}  steps/meal={Spm}  meanE={MeanE:0.000}  meanDiscomfort={MeanD:0.0000}  finalE={FinalE:0.000}",
                    seed, mode, result.Meals, Format(result.MeanStepsPerMeal, "0.0"),
                    result.MeanEnergy, result.MeanDiscomfort, result.FinalEnergy);
            }
        }

        var aggregates = Modes.Select(mode => Aggregate(mode, results)).ToList();

        string summaryPath = Path.Combine(_settings.OutputDirectory, "summary.csv");
        string timeseriesPath = Path.Combine(_settings.OutputDirectory, "timeseries.csv");
        string aggregatePath = Path.Combine(_settings.OutputDirectory, "aggregate.csv");

        CsvExport.WriteSummary(summaryPath, results);
        CsvExport.WriteTimeseries(timeseriesPath, results);
        CsvExport.WriteAggregate(aggregatePath, aggregates);

        var elapsed = DateTimeOffset.UtcNow - started;
        _logger.LogInformation("Done in {Seconds:0.0}s. Wrote {Summary}, {Timeseries}, {Aggregate}.",
            elapsed.TotalSeconds, summaryPath, timeseriesPath, aggregatePath);

        LogHeadline(aggregates);
    }

    /// <summary>
    /// Runs one (seed, mode) combination to completion, collecting its time-series samples and
    /// scalar summary metrics.
    /// </summary>
    private RunResult RunOne(ulong seed, LearningMode mode)
    {
        // A fresh generator per run: the seed alone fixes the whole stream, so every mode sharing a
        // seed starts identically. (The app's single shared IRandom would be wrong here — the runs
        // must not share RNG state.)
        var random = new Pcg32Random(seed);
        var simulation = new Simulation(_config, random) { Mode = mode };

        int capacity = _settings.Steps / _settings.SampleInterval + 1;
        var samples = new List<Sample>(capacity);
        double energySum = 0.0;
        double discomfortSum = 0.0;

        for (long step = 1; step <= _settings.Steps; step++)
        {
            simulation.Step();

            if (step % _settings.SampleInterval == 0)
            {
                SimulationSnapshot snap = simulation.Snapshot();
                double discomfort = Math.Abs(snap.BodyTemperature - _config.ComfortTemperature);
                samples.Add(new Sample(step, snap.Energy, discomfort, snap.MealsEaten));
                energySum += snap.Energy;
                discomfortSum += discomfort;
            }
        }

        SimulationSnapshot final = simulation.Snapshot();
        int sampleCount = samples.Count;
        double meanEnergy = sampleCount > 0 ? energySum / sampleCount : final.Energy;
        double meanDiscomfort = sampleCount > 0
            ? discomfortSum / sampleCount
            : Math.Abs(final.BodyTemperature - _config.ComfortTemperature);
        double meanStepsPerMeal = final.MealIntervals.Count > 0
            ? final.MealIntervals.Average()
            : double.NaN;

        return new RunResult(
            Mode: mode,
            Seed: seed,
            Steps: _settings.Steps,
            Meals: final.MealsEaten,
            MeanStepsPerMeal: meanStepsPerMeal,
            FinalEnergy: final.Energy,
            MeanEnergy: meanEnergy,
            MeanDiscomfort: meanDiscomfort,
            Samples: samples,
            MealIntervals: final.MealIntervals.ToArray());
    }

    /// <summary>Aggregates one mode's runs across seeds into a <see cref="ModeAggregate"/>.</summary>
    private static ModeAggregate Aggregate(LearningMode mode, IEnumerable<RunResult> results)
    {
        var runs = results.Where(r => r.Mode == mode).ToList();
        return new ModeAggregate(
            Mode: mode,
            Seeds: runs.Count,
            Meals: Stats.Compute(runs.Select(r => (double)r.Meals)),
            StepsPerMeal: Stats.Compute(runs.Select(r => r.MeanStepsPerMeal)),
            MeanEnergy: Stats.Compute(runs.Select(r => r.MeanEnergy)),
            MeanDiscomfort: Stats.Compute(runs.Select(r => r.MeanDiscomfort)),
            FinalEnergy: Stats.Compute(runs.Select(r => r.FinalEnergy)));
    }

    /// <summary>Logs the aggregate comparison as a compact fixed-width table.</summary>
    private void LogHeadline(IReadOnlyList<ModeAggregate> aggregates)
    {
        _logger.LogInformation("Aggregate across {Seeds} seeds (mean +/- sample SD) -- MEASURED, not guessed:", _settings.Seeds);
        _logger.LogInformation("{Row}", $"{"mode",-8} {"meals",16} {"steps/meal",16} {"mean energy",16} {"mean discomfort",18} {"final energy",16}");
        foreach (ModeAggregate a in aggregates)
        {
            _logger.LogInformation("{Row}", string.Join(" ",
                $"{a.Mode,-8}",
                $"{Cell(a.Meals, "0.0"),16}",
                $"{Cell(a.StepsPerMeal, "0.0"),16}",
                $"{Cell(a.MeanEnergy, "0.000"),16}",
                $"{Cell(a.MeanDiscomfort, "0.0000"),18}",
                $"{Cell(a.FinalEnergy, "0.000"),16}"));
        }
    }

    /// <summary>Formats a statistic as <c>mean±sd</c> for the console table.</summary>
    private static string Cell(Stats s, string format)
        => s.Count == 0
            ? "n/a"
            : $"{s.Mean.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}" +
              $"±{s.StdDev.ToString(format, System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>Formats a possibly-NaN scalar for a log line.</summary>
    private static string Format(double value, string format)
        => double.IsFinite(value)
            ? value.ToString(format, System.Globalization.CultureInfo.InvariantCulture)
            : "n/a";
}

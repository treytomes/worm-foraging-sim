using Microsoft.Extensions.Logging;
using WormForaging.Core;
using WormForaging.Core.Rng;
using WormForaging.Harness.Hosting;

namespace WormForaging.Harness.Experiment;

/// <summary>
/// Runs the Phase 2 swap-back / savings experiment: warm the three-factor learner up on the identity
/// mapping, then toggle the sensor-to-direction mapping repeatedly and measure how fast it reforages
/// each time — in particular whether re-entering a mapping it has seen before is any faster than the
/// first time (savings).
/// </summary>
/// <remarks>
/// <para>
/// The prototype author's prediction is <b>no savings</b>: learning only rewrites the readout
/// <c>Wo</c>, so each segment overwrites the mapping learned in the previous one and leaves no trace
/// to resume from. This runner only measures; it logs the per-cycle and per-exposure recovery and
/// leaves the verdict to the write-up. Confirming or refuting the prediction is what decides whether
/// slow/fast weights (Phase 3) address a real problem.
/// </para>
/// <para>
/// Recovery is read as meals eaten per fixed epoch after each swap. The early-window meal count is
/// the headline scalar; the whole per-epoch curve goes to CSV. Everything is measured against the
/// worm's warm-up competence (the final window of warm-up), which is the ceiling a fully relearned
/// mapping would reach. Meals are read from <see cref="Simulation.Snapshot"/> at epoch boundaries, so
/// Core is untouched and the run stays deterministic.
/// </para>
/// </remarks>
internal sealed class SwapExperimentRunner
{
    private readonly SwapSettings _settings;
    private readonly SimConfig _config;
    private readonly ILogger<SwapExperimentRunner> _logger;

    /// <summary>Creates the runner.</summary>
    /// <param name="settings">The resolved swap-experiment parameters.</param>
    /// <param name="config">The base simulation config (shared, immutable).</param>
    /// <param name="logger">The logger for progress and the headline tables.</param>
    public SwapExperimentRunner(SwapSettings settings, SimConfig config, ILogger<SwapExperimentRunner> logger)
    {
        _settings = settings;
        _config = config;
        _logger = logger;
    }

    /// <summary>Runs every seed, writes the CSVs, and logs the recovery and savings tables.</summary>
    public void Run()
    {
        Directory.CreateDirectory(_settings.OutputDirectory);

        long perSeed = _settings.WarmupSteps + (long)_settings.Swaps * _settings.SegmentSteps;
        _logger.LogInformation(
            "Phase 2 swap/savings: {Seeds} seeds, warmup {Warmup} + {Swaps} swaps x {Segment} steps = {PerSeed} steps/seed. " +
            "Epoch {Epoch}, recovery window {Window}. Output: {Out}",
            _settings.Seeds, _settings.WarmupSteps, _settings.Swaps, _settings.SegmentSteps, perSeed,
            _settings.EpochSteps, _settings.RecoveryWindowSteps, _settings.OutputDirectory);

        var runs = new List<SwapRunResult>(_settings.Seeds);
        var started = DateTimeOffset.UtcNow;

        foreach (ulong seed in _settings.SeedSequence())
        {
            SwapRunResult run = RunOne(seed);
            runs.Add(run);
            _logger.LogDebug("seed {Seed}: warmup window meals={Warmup}; cycle windows=[{Windows}]",
                seed, run.WarmupWindowMeals, string.Join(", ", run.Cycles.Select(c => $"{c.Mapping[..3]}#{c.ExposureOrdinal}:{c.WindowMeals}")));
        }

        var exposureAggregates = AggregateByExposure(runs);
        double warmupMean = Stats.Compute(runs.Select(r => (double)r.WarmupWindowMeals)).Mean;

        string cyclesPath = Path.Combine(_settings.OutputDirectory, "swap_cycles.csv");
        string recoveryPath = Path.Combine(_settings.OutputDirectory, "swap_recovery.csv");
        string savingsPath = Path.Combine(_settings.OutputDirectory, "swap_savings.csv");

        SwapCsvExport.WriteCycles(cyclesPath, runs);
        SwapCsvExport.WriteRecovery(recoveryPath, runs);
        SwapCsvExport.WriteSavings(savingsPath, exposureAggregates, warmupMean, _settings.Seeds);

        var elapsed = DateTimeOffset.UtcNow - started;
        _logger.LogInformation("Done in {Seconds:0.0}s. Wrote {Cycles}, {Recovery}, {Savings}.",
            elapsed.TotalSeconds, cyclesPath, recoveryPath, savingsPath);

        LogHeadline(runs, exposureAggregates, warmupMean);
    }

    /// <summary>Runs one seed to completion: warm-up, then every swap cycle.</summary>
    private SwapRunResult RunOne(ulong seed)
    {
        var simulation = new Simulation(_config, new Pcg32Random(seed)) { Mode = LearningMode.Full };

        // cumAtBoundary[b] = cumulative meals eaten at global step b * EpochSteps (boundary 0 = 0).
        var cumAtBoundary = new List<int> { 0 };
        int warmupEpochs = _settings.WarmupSteps / _settings.EpochSteps;
        for (int b = 1; b <= warmupEpochs; b++)
        {
            StepEpoch(simulation);
            cumAtBoundary.Add(simulation.Snapshot().MealsEaten);
        }

        int warmupWindowMeals = cumAtBoundary[warmupEpochs] - cumAtBoundary[warmupEpochs - _settings.WindowEpochs];

        // Identity is the mapping the worm just spent warm-up learning, so its first exposure is done.
        var exposureCounts = new Dictionary<string, int> { ["Identity"] = 1 };
        var cycles = new List<SwapCycleResult>(_settings.Swaps);
        int boundary = warmupEpochs;

        for (int cycle = 1; cycle <= _settings.Swaps; cycle++)
        {
            simulation.SwapSenses();
            string mapping = simulation.Permutation.IsIdentity ? "Identity" : "Swapped";
            exposureCounts[mapping] = exposureCounts.GetValueOrDefault(mapping) + 1;
            int exposureOrdinal = exposureCounts[mapping];

            int cumAtSwap = cumAtBoundary[boundary];
            int preWindowMeals = cumAtSwap - cumAtBoundary[boundary - _settings.WindowEpochs];

            var epochMeals = new List<int>(_settings.EpochsPerSegment);
            int prev = cumAtSwap;
            for (int e = 1; e <= _settings.EpochsPerSegment; e++)
            {
                StepEpoch(simulation);
                int cum = simulation.Snapshot().MealsEaten;
                cumAtBoundary.Add(cum);
                epochMeals.Add(cum - prev);
                prev = cum;
                boundary++;
            }

            int windowMeals = epochMeals.Take(_settings.WindowEpochs).Sum();
            int segmentMeals = prev - cumAtSwap;

            cycles.Add(new SwapCycleResult(
                seed, cycle, mapping, exposureOrdinal, preWindowMeals, windowMeals, segmentMeals, epochMeals));
        }

        return new SwapRunResult(seed, warmupWindowMeals, cycles);
    }

    /// <summary>Advances the simulation by one epoch's worth of steps.</summary>
    private void StepEpoch(Simulation simulation)
    {
        for (int i = 0; i < _settings.EpochSteps; i++)
        {
            simulation.Step();
        }
    }

    /// <summary>
    /// Groups every swap cycle by (mapping, exposure ordinal) and summarizes the recovery scalars
    /// across seeds — the savings view.
    /// </summary>
    private static List<SwapExposureAggregate> AggregateByExposure(IEnumerable<SwapRunResult> runs)
    {
        var all = runs.SelectMany(r => r.Cycles).ToList();
        return all
            .GroupBy(c => (c.Mapping, c.ExposureOrdinal))
            .OrderBy(g => g.Key.Mapping)
            .ThenBy(g => g.Key.ExposureOrdinal)
            .Select(g => new SwapExposureAggregate(
                g.Key.Mapping,
                g.Key.ExposureOrdinal,
                Stats.Compute(g.Select(c => (double)c.WindowMeals)),
                Stats.Compute(g.Select(c => (double)c.SegmentMeals))))
            .ToList();
    }

    /// <summary>Logs the per-cycle recovery and the per-exposure savings view.</summary>
    private void LogHeadline(
        IReadOnlyList<SwapRunResult> runs,
        IReadOnlyList<SwapExposureAggregate> exposures,
        double warmupMean)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        _logger.LogInformation(
            "Warm-up competence (meals in the final {Window}-step window, identity mapping): {Mean} -- the ceiling a fully relearned mapping would reach.",
            _settings.RecoveryWindowSteps, warmupMean.ToString("0.0", inv));

        // Per-cycle: how each swap landed, in run order (mappings alternate).
        _logger.LogInformation("Per swap cycle (mean +/- SD across {Seeds} seeds) -- MEASURED:", _settings.Seeds);
        _logger.LogInformation("{Row}", $"{"cycle",-6}{"mapping",-10}{"exposure",-10}{"pre-window",-16}{"recovery window",-18}{"segment total",-16}");
        int cycleCount = runs.Count > 0 ? runs[0].Cycles.Count : 0;
        for (int c = 0; c < cycleCount; c++)
        {
            var at = runs.Select(r => r.Cycles[c]).ToList();
            var first = at[0];
            Stats pre = Stats.Compute(at.Select(x => (double)x.PreWindowMeals));
            Stats win = Stats.Compute(at.Select(x => (double)x.WindowMeals));
            Stats seg = Stats.Compute(at.Select(x => (double)x.SegmentMeals));
            _logger.LogInformation("{Row}", string.Concat(
                $"{first.Cycle,-6}", $"{first.Mapping,-10}", $"#{first.ExposureOrdinal,-9}",
                $"{Cell(pre, "0.0"),-16}", $"{Cell(win, "0.0"),-18}", $"{Cell(seg, "0.0"),-16}"));
        }

        // Per-exposure: the savings test. For each mapping, does the recovery window grow with
        // repeated exposure? Flat => no savings (the readout just overwrites).
        _logger.LogInformation("Savings view -- recovery-window meals by how many times the mapping was seen:");
        foreach (var byMapping in exposures.GroupBy(e => e.Mapping))
        {
            string trail = string.Join("  ", byMapping.Select(e =>
                $"#{e.ExposureOrdinal}={Cell(e.WindowMeals, "0.0")}"));
            _logger.LogInformation("  {Mapping,-9}: {Trail}", byMapping.Key, trail);
        }
    }

    /// <summary>Formats a statistic as <c>mean±sd</c>.</summary>
    private static string Cell(Stats s, string format)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return s.Count == 0
            ? "n/a"
            : $"{s.Mean.ToString(format, inv)}±{s.StdDev.ToString(format, inv)}";
    }
}

using System.Globalization;
using System.Text;

namespace WormForaging.Harness.Experiment;

/// <summary>
/// Writes the experiment results as plain CSV for later analysis (pandas, a spreadsheet, R, ...).
/// All values are formatted with the invariant culture so a comma is always the field separator and
/// the decimal point is always a dot, regardless of the machine's locale.
/// </summary>
internal static class CsvExport
{
    /// <summary>Significant-figure format for most metrics; compact but precise enough for analysis.</summary>
    private const string NUM = "0.######";

    /// <summary>
    /// Writes one row per (mode, seed): the scalar summary metrics for that run.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="results">All run results.</param>
    public static void WriteSummary(string path, IEnumerable<RunResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("mode,seed,steps,meals,mean_steps_per_meal,final_energy,mean_energy,mean_discomfort");
        foreach (RunResult r in results)
        {
            sb.Append(r.Mode).Append(',')
              .Append(r.Seed.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.Steps.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.Meals.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Num(r.MeanStepsPerMeal)).Append(',')
              .Append(Num(r.FinalEnergy)).Append(',')
              .Append(Num(r.MeanEnergy)).Append(',')
              .Append(Num(r.MeanDiscomfort)).Append('\n');
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Writes the sampled time series: one row per (mode, seed, sampled step), so energy and
    /// discomfort trajectories can be plotted and averaged across seeds downstream.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="results">All run results.</param>
    public static void WriteTimeseries(string path, IEnumerable<RunResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("mode,seed,step,energy,discomfort,meals");
        foreach (RunResult r in results)
        {
            string mode = r.Mode.ToString();
            string seed = r.Seed.ToString(CultureInfo.InvariantCulture);
            foreach (Sample s in r.Samples)
            {
                sb.Append(mode).Append(',')
                  .Append(seed).Append(',')
                  .Append(s.Step.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Num(s.Energy)).Append(',')
                  .Append(Num(s.Discomfort)).Append(',')
                  .Append(s.Meals.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Writes the headline table: one row per mode, each metric as mean, standard deviation, and the
    /// count of seeds that contributed a finite value.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="aggregates">The per-mode aggregates.</param>
    public static void WriteAggregate(string path, IEnumerable<ModeAggregate> aggregates)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "mode,seeds," +
            "meals_mean,meals_std," +
            "steps_per_meal_mean,steps_per_meal_std,steps_per_meal_n," +
            "mean_energy_mean,mean_energy_std," +
            "mean_discomfort_mean,mean_discomfort_std," +
            "final_energy_mean,final_energy_std");
        foreach (ModeAggregate a in aggregates)
        {
            sb.Append(a.Mode).Append(',')
              .Append(a.Seeds.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Num(a.Meals.Mean)).Append(',').Append(Num(a.Meals.StdDev)).Append(',')
              .Append(Num(a.StepsPerMeal.Mean)).Append(',').Append(Num(a.StepsPerMeal.StdDev)).Append(',')
              .Append(a.StepsPerMeal.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Num(a.MeanEnergy.Mean)).Append(',').Append(Num(a.MeanEnergy.StdDev)).Append(',')
              .Append(Num(a.MeanDiscomfort.Mean)).Append(',').Append(Num(a.MeanDiscomfort.StdDev)).Append(',')
              .Append(Num(a.FinalEnergy.Mean)).Append(',').Append(Num(a.FinalEnergy.StdDev)).Append('\n');
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>Formats a number invariantly, writing an empty field for a non-finite value.</summary>
    private static string Num(double value)
        => double.IsFinite(value) ? value.ToString(NUM, CultureInfo.InvariantCulture) : string.Empty;
}

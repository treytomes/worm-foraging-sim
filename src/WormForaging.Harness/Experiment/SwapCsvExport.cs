using System.Globalization;
using System.Text;

namespace WormForaging.Harness.Experiment;

/// <summary>
/// Writes the Phase 2 swap/savings results as plain CSV, invariant-culture formatted so the comma is
/// always the separator and the decimal is always a dot.
/// </summary>
internal static class SwapCsvExport
{
    /// <summary>Significant-figure format for aggregate statistics.</summary>
    private const string NUM = "0.######";

    /// <summary>Writes one row per (seed, swap cycle) with the scalar recovery metrics.</summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="runs">All seed runs.</param>
    public static void WriteCycles(string path, IEnumerable<SwapRunResult> runs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("seed,cycle,mapping,exposure_ordinal,pre_window_meals,window_meals,segment_meals");
        foreach (SwapRunResult run in runs)
        {
            foreach (SwapCycleResult c in run.Cycles)
            {
                sb.Append(run.Seed.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(c.Cycle.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(c.Mapping).Append(',')
                  .Append(c.ExposureOrdinal.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(c.PreWindowMeals.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(c.WindowMeals.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(c.SegmentMeals.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Writes the recovery curves: one row per (seed, cycle, epoch), so a plot can show meals climbing
    /// back over the epochs after each swap.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="runs">All seed runs.</param>
    public static void WriteRecovery(string path, IEnumerable<SwapRunResult> runs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("seed,cycle,mapping,exposure_ordinal,epoch,epoch_meals");
        foreach (SwapRunResult run in runs)
        {
            string seed = run.Seed.ToString(CultureInfo.InvariantCulture);
            foreach (SwapCycleResult c in run.Cycles)
            {
                string cycle = c.Cycle.ToString(CultureInfo.InvariantCulture);
                string ord = c.ExposureOrdinal.ToString(CultureInfo.InvariantCulture);
                for (int e = 0; e < c.EpochMeals.Count; e++)
                {
                    sb.Append(seed).Append(',')
                      .Append(cycle).Append(',')
                      .Append(c.Mapping).Append(',')
                      .Append(ord).Append(',')
                      .Append((e + 1).ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(c.EpochMeals[e].ToString(CultureInfo.InvariantCulture)).Append('\n');
                }
            }
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>
    /// Writes the savings table: one row per (mapping, exposure ordinal), the recovery-window and
    /// whole-segment meal counts aggregated across seeds. A warm-up reference row (cycle 0) records
    /// the identity-mapping competence ceiling.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="exposures">The per-exposure aggregates.</param>
    /// <param name="warmupWindowMean">Mean meals in the final warm-up window (the competence ceiling).</param>
    /// <param name="seeds">Number of seeds (for the warm-up reference row's count).</param>
    public static void WriteSavings(string path, IEnumerable<SwapExposureAggregate> exposures, double warmupWindowMean, int seeds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("mapping,exposure_ordinal,seeds,window_meals_mean,window_meals_std,segment_meals_mean,segment_meals_std");

        // Reference: warm-up competence on the identity mapping (learned from scratch, fully settled).
        sb.Append("Identity_warmup,1,")
          .Append(seeds.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(Num(warmupWindowMean)).Append(",,,").Append('\n');

        foreach (SwapExposureAggregate a in exposures)
        {
            sb.Append(a.Mapping).Append(',')
              .Append(a.ExposureOrdinal.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(a.WindowMeals.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Num(a.WindowMeals.Mean)).Append(',').Append(Num(a.WindowMeals.StdDev)).Append(',')
              .Append(Num(a.SegmentMeals.Mean)).Append(',').Append(Num(a.SegmentMeals.StdDev)).Append('\n');
        }

        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>Formats a number invariantly, writing an empty field for a non-finite value.</summary>
    private static string Num(double value)
        => double.IsFinite(value) ? value.ToString(NUM, CultureInfo.InvariantCulture) : string.Empty;
}

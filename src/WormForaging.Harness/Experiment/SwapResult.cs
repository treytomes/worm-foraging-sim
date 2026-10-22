namespace WormForaging.Harness.Experiment;

/// <summary>
/// What happened in one segment after one swap: which mapping the worm was dropped into, how many
/// times it had seen that mapping before, and how quickly it reforaged.
/// </summary>
/// <param name="Seed">The seed this run used.</param>
/// <param name="Cycle">The swap index, 1-based (cycle 1 is the first swap after warm-up).</param>
/// <param name="Mapping">The mapping active during this segment ("Identity" or "Swapped").</param>
/// <param name="ExposureOrdinal">
/// How many times this mapping has been entered so far, counting this segment (and counting warm-up
/// as the identity mapping's first exposure). Re-exposure to a previously seen mapping has ordinal 2+.
/// </param>
/// <param name="PreWindowMeals">Meals eaten in the window immediately <em>before</em> the swap (stable reference).</param>
/// <param name="WindowMeals">Meals eaten in the early window immediately <em>after</em> the swap (the recovery scalar).</param>
/// <param name="SegmentMeals">Total meals eaten across the whole segment.</param>
/// <param name="EpochMeals">Meals eaten in each epoch of the segment, in order (the recovery curve).</param>
internal sealed record SwapCycleResult(
    ulong Seed,
    int Cycle,
    string Mapping,
    int ExposureOrdinal,
    int PreWindowMeals,
    int WindowMeals,
    int SegmentMeals,
    IReadOnlyList<int> EpochMeals);

/// <summary>One seed's complete swap run: the warm-up competence plus every swap cycle.</summary>
/// <param name="Seed">The seed this run used.</param>
/// <param name="WarmupWindowMeals">
/// Meals in the final window of warm-up — the worm's stable competence on the identity mapping
/// before any swap, the reference the recovery scalars are read against.
/// </param>
/// <param name="Cycles">The per-swap results, in order.</param>
internal sealed record SwapRunResult(
    ulong Seed,
    int WarmupWindowMeals,
    IReadOnlyList<SwapCycleResult> Cycles);

/// <summary>
/// The savings view: one mapping seen a given number of times, aggregated across seeds. If relearning
/// shows savings, <see cref="WindowMeals"/> rises with <see cref="ExposureOrdinal"/>; the author
/// predicts it stays flat (the readout just overwrites).
/// </summary>
/// <param name="Mapping">The mapping ("Identity" or "Swapped").</param>
/// <param name="ExposureOrdinal">How many times the mapping had been entered.</param>
/// <param name="WindowMeals">Early-window meal-count statistics across seeds (the recovery scalar).</param>
/// <param name="SegmentMeals">Whole-segment meal-count statistics across seeds.</param>
internal sealed record SwapExposureAggregate(
    string Mapping,
    int ExposureOrdinal,
    Stats WindowMeals,
    Stats SegmentMeals);

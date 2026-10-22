using WormForaging.Core;

namespace WormForaging.Harness.Experiment;

/// <summary>One time-series observation taken during a run.</summary>
/// <param name="Step">The step at which the sample was taken.</param>
/// <param name="Energy">Energy reserve at that step, in [0, 1].</param>
/// <param name="Discomfort">Thermal discomfort <c>|bodyTemp - comfort|</c> at that step.</param>
/// <param name="Meals">Cumulative meals eaten by that step.</param>
internal readonly record struct Sample(long Step, double Energy, double Discomfort, int Meals);

/// <summary>
/// The complete result of one (mode, seed) run: the scalar summary metrics plus the raw series the
/// CSVs are built from. Nothing is interpreted here — the runner only records what happened.
/// </summary>
/// <param name="Mode">The learning mode this run used.</param>
/// <param name="Seed">The seed this run used.</param>
/// <param name="Steps">How many steps the run advanced.</param>
/// <param name="Meals">Total meals eaten (foraging throughput; higher is better).</param>
/// <param name="MeanStepsPerMeal">
/// Mean of the per-meal step intervals (how long each meal took to find; lower is better), or
/// <see cref="double.NaN"/> when no meal was eaten.
/// </param>
/// <param name="FinalEnergy">Energy at the final step.</param>
/// <param name="MeanEnergy">Mean energy across the sampled steps (higher is better).</param>
/// <param name="MeanDiscomfort">Mean thermal discomfort across the sampled steps (lower is better).</param>
/// <param name="Samples">The time-series samples taken during the run.</param>
/// <param name="MealIntervals">The raw per-meal step intervals, in order.</param>
internal sealed record RunResult(
    LearningMode Mode,
    ulong Seed,
    int Steps,
    int Meals,
    double MeanStepsPerMeal,
    double FinalEnergy,
    double MeanEnergy,
    double MeanDiscomfort,
    IReadOnlyList<Sample> Samples,
    IReadOnlyList<long> MealIntervals);

/// <summary>
/// One mode's metrics aggregated across all seeds: each scalar summarized by its mean and
/// (sample) standard deviation. This is the headline comparison table.
/// </summary>
/// <param name="Mode">The learning mode.</param>
/// <param name="Seeds">How many seeds contributed.</param>
/// <param name="Meals">Meals-eaten statistics across seeds.</param>
/// <param name="StepsPerMeal">Mean-steps-per-meal statistics across seeds (finite runs only).</param>
/// <param name="MeanEnergy">Mean-energy statistics across seeds.</param>
/// <param name="MeanDiscomfort">Mean-discomfort statistics across seeds.</param>
/// <param name="FinalEnergy">Final-energy statistics across seeds.</param>
internal sealed record ModeAggregate(
    LearningMode Mode,
    int Seeds,
    Stats Meals,
    Stats StepsPerMeal,
    Stats MeanEnergy,
    Stats MeanDiscomfort,
    Stats FinalEnergy);

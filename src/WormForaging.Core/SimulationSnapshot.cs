namespace WormForaging.Core;

/// <summary>
/// An immutable view of the simulation state at one instant, for the UI and the headless harness
/// to read without touching the live components.
/// </summary>
/// <remarks>
/// Every array field is a copy taken when the snapshot was made, so a snapshot stays valid after
/// the simulation steps again. Running controls (mode, temperature, homeostasis, the sense
/// permutation) are not here — they are inputs the caller already holds, not observed state.
/// </remarks>
/// <param name="Step">The number of steps taken so far.</param>
/// <param name="WormX">The worm's x position, in arena cells.</param>
/// <param name="WormY">The worm's y position, in arena cells.</param>
/// <param name="FoodX">The food's x position, in arena cells.</param>
/// <param name="FoodY">The food's y position, in arena cells.</param>
/// <param name="LampX">The heat lamp's x position, in arena cells.</param>
/// <param name="LampY">The heat lamp's y position, in arena cells.</param>
/// <param name="Energy">The worm's energy reserve, in [0, 1].</param>
/// <param name="Gut">Undigested food waiting to be absorbed.</param>
/// <param name="BodyTemperature">The worm's body temperature.</param>
/// <param name="MealsEaten">The total number of meals eaten.</param>
/// <param name="MealIntervals">Steps taken to reach each meal, in order (the steps-per-meal series).</param>
/// <param name="Activations">The interneuron activations this step (length = interneuron count).</param>
/// <param name="ReadoutWeights">The motor readout weights (motor-major, length = motors·neurons).</param>
/// <param name="Modulator">The broadcast neuromodulator delivered to the readout this step (reward in Full mode, fixed in Hebbian, zero in Frozen).</param>
/// <param name="FoodDrive">The food (hunger) channel's contribution to the reward this step; computed regardless of mode.</param>
/// <param name="ThermalDrive">The thermal channel's contribution to the reward this step; computed regardless of mode.</param>
public sealed record SimulationSnapshot(
    long Step,
    double WormX,
    double WormY,
    double FoodX,
    double FoodY,
    double LampX,
    double LampY,
    double Energy,
    double Gut,
    double BodyTemperature,
    int MealsEaten,
    IReadOnlyList<long> MealIntervals,
    IReadOnlyList<double> Activations,
    IReadOnlyList<double> ReadoutWeights,
    double Modulator,
    double FoodDrive,
    double ThermalDrive);

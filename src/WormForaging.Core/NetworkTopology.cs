namespace WormForaging.Core;

/// <summary>
/// An immutable snapshot of the network's <em>fixed</em> wiring — everything that never changes once
/// a seed is chosen — for a UI to draw a diagram from. It deliberately excludes the learned readout
/// (<c>Wo</c>, which rides in <see cref="SimulationSnapshot.ReadoutWeights"/>) and the live, plastic
/// gains and activations: those change every step and belong in the per-tick snapshot, not here.
/// </summary>
/// <remarks>
/// <para>
/// The arrays are copies taken when the topology was built, so they stay valid for the life of the
/// simulation (the wiring is a reservoir and is drawn once). Indexing conventions match
/// <see cref="Network"/>: an input weight is <c>InputWeights[i*SensorCount + j]</c> for sensor
/// <c>j</c> into neuron <c>i</c>; a recurrent weight is <c>RecurrentWeights[i*NeuronCount + j]</c> for
/// neuron <c>j</c>'s previous activation into neuron <c>i</c> (absent sparse connections are exactly
/// zero); the drive weights and biases are one value per neuron.
/// </para>
/// </remarks>
/// <param name="NeuronCount">Number of interneurons.</param>
/// <param name="SensorCount">Number of sensor channels feeding the network.</param>
/// <param name="MotorCount">Number of motor neurons the readout targets.</param>
/// <param name="InputWeights">Fixed input weights, neuron-major (<c>[i*SensorCount + j]</c> = sensor <c>j</c> → neuron <c>i</c>).</param>
/// <param name="RecurrentWeights">Fixed sparse recurrent weights, neuron-major (<c>[i*NeuronCount + j]</c> = neuron <c>j</c> → neuron <c>i</c>; absent = 0).</param>
/// <param name="HungerWeights">Fixed hunger-drive weight per neuron.</param>
/// <param name="ThermalWeights">Fixed thermal-drive weight per neuron.</param>
/// <param name="Biases">Fixed bias per neuron.</param>
public sealed record NetworkTopology(
    int NeuronCount,
    int SensorCount,
    int MotorCount,
    IReadOnlyList<double> InputWeights,
    IReadOnlyList<double> RecurrentWeights,
    IReadOnlyList<double> HungerWeights,
    IReadOnlyList<double> ThermalWeights,
    IReadOnlyList<double> Biases);

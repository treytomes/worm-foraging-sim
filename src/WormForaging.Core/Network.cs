using WormForaging.Core.Rng;

namespace WormForaging.Core;

/// <summary>
/// The recurrent interneuron layer: a fixed, sparsely wired pool of <c>tanh</c> units with
/// per-neuron homeostatic gain. It turns the sensor vector and the internal drives into a vector
/// of activations the motor layer reads.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the prototype's interneuron block. Each neuron <c>i</c> integrates the two internal
/// drives (hunger, thermal deviation) through its own weights, a bias, the sensor vector through
/// <c>Win</c>, and the previous activations through the sparse recurrent matrix <c>Wrec</c>, then
/// squashes with <c>tanh(gain·u)</c>.
/// </para>
/// <para>
/// <b>Fixed wiring, plastic gain.</b> The weights (<c>Win</c>, <c>Wrec</c>, drive weights, bias)
/// are random at construction and never change — this is a reservoir, not a trained hidden layer
/// (training the hidden layer by local rules is a Phase-3 experiment, never by backprop). The one
/// thing that adapts is each neuron's gain, nudged by a slow homeostatic rule toward a target
/// average activity. The readout weights that the motor layer learns live elsewhere; this type
/// holds no learning.
/// </para>
/// <para>
/// <b>Step contract.</b> Call <see cref="Activate"/> once to get this step's activations, let the
/// motor and learning layers consume them, then call <see cref="AdaptAndCommit"/> with those same
/// activations to run homeostasis and store them as the recurrent state for the next step — the
/// prototype does both at the very end of <c>step</c>.
/// </para>
/// </remarks>
public sealed class Network
{
    private readonly SimConfig _config;
    private readonly int _neuronCount;
    private readonly int _sensorCount;

    private readonly double[] _inputWeights;        // Win: neuronCount x sensorCount, row-major
    private readonly double[] _recurrentWeights;    // Wrec: neuronCount x neuronCount, row-major, sparse
    private readonly double[] _hungerWeights;       // wh
    private readonly double[] _thermalWeights;      // wt
    private readonly double[] _bias;                // b
    private readonly double[] _gain;                // g
    private readonly double[] _activityTrace;       // ax
    private readonly double[] _previousActivations; // xp (recurrent state)
    private double[] _lastActivations;              // act (most recent Activate result)

    /// <summary>
    /// Builds a network with randomly initialized, fixed wiring and the configured initial gains,
    /// activity traces, and zero recurrent state.
    /// </summary>
    /// <param name="config">The simulation constants.</param>
    /// <param name="random">The deterministic random source; the same seed yields the same wiring.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="random"/> is null.</exception>
    /// <remarks>
    /// Weights are drawn in a fixed order — input, recurrent, hunger, thermal, bias — so a given
    /// seed reproduces the wiring. Recurrent connections are present with
    /// <see cref="SimConfig.RecurrentConnectionProbability"/>; an absent connection draws no
    /// Gaussian (matching the prototype's short-circuit), which keeps the random stream aligned.
    /// </remarks>
    public Network(SimConfig config, IRandom random)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ArgumentNullException.ThrowIfNull(random);

        _neuronCount = config.InterneuronCount;
        _sensorCount = config.SensorCount;

        _inputWeights = new double[_neuronCount * _sensorCount];
        for (int k = 0; k < _inputWeights.Length; k++)
        {
            _inputWeights[k] = random.NextGaussian() * config.InputWeightScale;
        }

        _recurrentWeights = new double[_neuronCount * _neuronCount];
        for (int k = 0; k < _recurrentWeights.Length; k++)
        {
            _recurrentWeights[k] = random.NextDouble() < config.RecurrentConnectionProbability
                ? random.NextGaussian() * config.RecurrentWeightScale
                : 0.0;
        }

        _hungerWeights = new double[_neuronCount];
        for (int i = 0; i < _neuronCount; i++)
        {
            _hungerWeights[i] = random.NextGaussian() * config.HungerWeightScale;
        }

        _thermalWeights = new double[_neuronCount];
        for (int i = 0; i < _neuronCount; i++)
        {
            _thermalWeights[i] = random.NextGaussian() * config.ThermalWeightScale;
        }

        _bias = new double[_neuronCount];
        for (int i = 0; i < _neuronCount; i++)
        {
            _bias[i] = random.NextGaussian() * config.BiasScale;
        }

        _gain = new double[_neuronCount];
        Array.Fill(_gain, config.InitialGain);

        _activityTrace = new double[_neuronCount];
        Array.Fill(_activityTrace, config.InitialActivityTrace);

        _previousActivations = new double[_neuronCount];
        _lastActivations = new double[_neuronCount];
    }

    /// <summary>The number of interneurons.</summary>
    public int NeuronCount => _neuronCount;

    /// <summary>The number of sensor channels the network expects as input.</summary>
    public int SensorCount => _sensorCount;

    /// <summary>Live, read-only view of the most recent <see cref="Activate"/> result. Do not mutate.</summary>
    public IReadOnlyList<double> Activations => _lastActivations;

    /// <summary>Live, read-only view of the per-neuron homeostatic gains. Do not mutate.</summary>
    public IReadOnlyList<double> Gains => _gain;

    /// <summary>Live, read-only view of the per-neuron running activity traces. Do not mutate.</summary>
    public IReadOnlyList<double> ActivityTrace => _activityTrace;

    /// <summary>Live, read-only view of the recurrent state (previous activations). Do not mutate.</summary>
    public IReadOnlyList<double> PreviousActivations => _previousActivations;

    /// <summary>Read-only view of the fixed input weights (neuron-major, length neurons·sensors). Do not mutate.</summary>
    public IReadOnlyList<double> InputWeights => _inputWeights;

    /// <summary>Read-only view of the fixed sparse recurrent weights (neuron-major, length neurons²). Do not mutate.</summary>
    public IReadOnlyList<double> RecurrentWeights => _recurrentWeights;

    /// <summary>Read-only view of the fixed hunger-drive weights. Do not mutate.</summary>
    public IReadOnlyList<double> HungerWeights => _hungerWeights;

    /// <summary>Read-only view of the fixed thermal-drive weights. Do not mutate.</summary>
    public IReadOnlyList<double> ThermalWeights => _thermalWeights;

    /// <summary>Read-only view of the fixed per-neuron biases. Do not mutate.</summary>
    public IReadOnlyList<double> Biases => _bias;

    /// <summary>
    /// Computes this step's interneuron activations from the sensor vector and internal drives,
    /// using the current gains and recurrent state. Does not change any state.
    /// </summary>
    /// <param name="sensors">The sensor vector, of length <see cref="SensorCount"/>.</param>
    /// <param name="hunger">The hunger drive (<c>1 − energy</c>).</param>
    /// <param name="thermalDeviation">The signed thermal deviation from comfort.</param>
    /// <returns>A fresh array of <see cref="NeuronCount"/> activations, each in (−1, 1).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sensors"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sensors"/> is not length <see cref="SensorCount"/>.</exception>
    public double[] Activate(double[] sensors, double hunger, double thermalDeviation)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        if (sensors.Length != _sensorCount)
        {
            throw new ArgumentException(
                $"Expected {_sensorCount} sensors but got {sensors.Length}.", nameof(sensors));
        }

        var activations = new double[_neuronCount];
        for (int i = 0; i < _neuronCount; i++)
        {
            double u = _hungerWeights[i] * hunger * _config.HungerInputScale
                + _thermalWeights[i] * thermalDeviation * _config.ThermalInputScale
                + _bias[i];

            int inputRow = i * _sensorCount;
            for (int j = 0; j < _sensorCount; j++)
            {
                u += _inputWeights[inputRow + j] * sensors[j];
            }

            int recurrentRow = i * _neuronCount;
            for (int j = 0; j < _neuronCount; j++)
            {
                u += _recurrentWeights[recurrentRow + j] * _previousActivations[j];
            }

            activations[i] = Math.Tanh(_gain[i] * u);
        }

        _lastActivations = activations;
        return activations;
    }

    /// <summary>
    /// Runs the homeostatic update and commits the given activations as the recurrent state for
    /// the next step. Call after the motor and learning layers have consumed the activations.
    /// </summary>
    /// <param name="activations">The activations returned by the matching <see cref="Activate"/> call.</param>
    /// <param name="homeostasisEnabled">Whether to adapt the gains this step (the prototype's checkbox).</param>
    /// <exception cref="ArgumentNullException"><paramref name="activations"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="activations"/> is not length <see cref="NeuronCount"/>.</exception>
    /// <remarks>
    /// The activity trace always updates; only the gain adjustment is gated by
    /// <paramref name="homeostasisEnabled"/>, matching the prototype. Gains are clamped to
    /// [<see cref="SimConfig.GainMin"/>, <see cref="SimConfig.GainMax"/>].
    /// </remarks>
    public void AdaptAndCommit(double[] activations, bool homeostasisEnabled)
    {
        ArgumentNullException.ThrowIfNull(activations);
        if (activations.Length != _neuronCount)
        {
            throw new ArgumentException(
                $"Expected {_neuronCount} activations but got {activations.Length}.", nameof(activations));
        }

        for (int i = 0; i < _neuronCount; i++)
        {
            _activityTrace[i] += _config.ActivityTraceRate * (Math.Abs(activations[i]) - _activityTrace[i]);

            if (homeostasisEnabled)
            {
                double adjusted = _gain[i] + _config.GainAdaptRate * (_config.GainActivityTarget - _activityTrace[i]);
                _gain[i] = Math.Max(_config.GainMin, Math.Min(_config.GainMax, adjusted));
            }
        }

        Array.Copy(activations, _previousActivations, _neuronCount);
    }
}

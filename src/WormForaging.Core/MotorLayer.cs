using WormForaging.Core.Rng;

namespace WormForaging.Core;

/// <summary>
/// The motor readout: a linear map from interneuron activations to four action logits, a softmax
/// policy over the directions, and the sampling that picks one.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the prototype's readout block. The readout weights <c>Wo</c> are the <em>only</em>
/// weights the learning rule changes; this type owns them, uses them to score actions, and exposes
/// them for the three-factor rule to read and update. The eligibility trace and the modulated
/// update themselves live in the learning layer — this type holds no learning, only the policy.
/// </para>
/// <para>
/// <b>Sampling.</b> The prototype's inverse-CDF sampler is written as a guarded single pass
/// (<c>if(r0&lt;c &amp;&amp; ch===K-1 &amp;&amp; k&lt;K-1)</c>) that the handoff flags as "correct but
/// fragile." This port uses the plain, equivalent form: walk the cumulative probabilities and take
/// the first bucket the uniform draw falls in, with the last direction as the fallback when
/// rounding leaves the draw just past the end. Same choice for every draw, far easier to read.
/// </para>
/// </remarks>
public sealed class MotorLayer
{
    private readonly int _motorCount;
    private readonly int _neuronCount;
    private readonly double[] _readoutWeights; // Wo: motorCount x neuronCount, row-major

    /// <summary>Builds a motor layer with small random readout weights.</summary>
    /// <param name="config">The simulation constants.</param>
    /// <param name="random">The deterministic random source; same seed yields the same readout.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="random"/> is null.</exception>
    public MotorLayer(SimConfig config, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(random);

        _motorCount = config.MotorCount;
        _neuronCount = config.InterneuronCount;

        _readoutWeights = new double[_motorCount * _neuronCount];
        for (int k = 0; k < _readoutWeights.Length; k++)
        {
            _readoutWeights[k] = random.NextGaussian() * config.ReadoutInitScale;
        }
    }

    /// <summary>The number of motor neurons / directions.</summary>
    public int MotorCount => _motorCount;

    /// <summary>The number of interneurons the readout reads from.</summary>
    public int NeuronCount => _neuronCount;

    /// <summary>The number of readout weights (<see cref="MotorCount"/> × <see cref="NeuronCount"/>).</summary>
    public int ReadoutWeightCount => _readoutWeights.Length;

    /// <summary>Live, read-only view of the readout weights (motor-major). Do not mutate; use <see cref="SetReadoutWeight"/>.</summary>
    public IReadOnlyList<double> ReadoutWeights => _readoutWeights;

    /// <summary>Reads one readout weight by flat index (matching the eligibility trace's layout).</summary>
    /// <param name="index">Flat index in [0, <see cref="ReadoutWeightCount"/>).</param>
    /// <returns>The weight at that index.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public double GetReadoutWeight(int index)
    {
        if ((uint)index >= (uint)_readoutWeights.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _readoutWeights[index];
    }

    /// <summary>Writes one readout weight by flat index. The learning rule uses this to apply its update.</summary>
    /// <param name="index">Flat index in [0, <see cref="ReadoutWeightCount"/>).</param>
    /// <param name="value">The new weight value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void SetReadoutWeight(int index, double value)
    {
        if ((uint)index >= (uint)_readoutWeights.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _readoutWeights[index] = value;
    }

    /// <summary>
    /// Computes the softmax action policy for the given activations and exploration temperature,
    /// without sampling.
    /// </summary>
    /// <param name="activations">The interneuron activations, of length <see cref="NeuronCount"/>.</param>
    /// <param name="temperature">The exploration temperature (higher = flatter policy); must be positive.</param>
    /// <returns>Normalized probabilities over the directions, length <see cref="MotorCount"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="activations"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="activations"/> is not length <see cref="NeuronCount"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="temperature"/> is not positive.</exception>
    public double[] Policy(IReadOnlyList<double> activations, double temperature)
    {
        ArgumentNullException.ThrowIfNull(activations);
        if (activations.Count != _neuronCount)
        {
            throw new ArgumentException(
                $"Expected {_neuronCount} activations but got {activations.Count}.", nameof(activations));
        }

        if (temperature <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(temperature), temperature, "Temperature must be positive.");
        }

        var logits = new double[_motorCount];
        double max = double.NegativeInfinity;
        for (int k = 0; k < _motorCount; k++)
        {
            double v = 0.0;
            int row = k * _neuronCount;
            for (int j = 0; j < _neuronCount; j++)
            {
                v += _readoutWeights[row + j] * activations[j];
            }

            logits[k] = v / temperature;
            if (logits[k] > max)
            {
                max = logits[k];
            }
        }

        var probabilities = new double[_motorCount];
        double sum = 0.0;
        for (int k = 0; k < _motorCount; k++)
        {
            probabilities[k] = Math.Exp(logits[k] - max); // shift by max for numerical stability
            sum += probabilities[k];
        }

        for (int k = 0; k < _motorCount; k++)
        {
            probabilities[k] /= sum;
        }

        return probabilities;
    }

    /// <summary>
    /// Computes the policy and samples one action from it.
    /// </summary>
    /// <param name="activations">The interneuron activations, of length <see cref="NeuronCount"/>.</param>
    /// <param name="temperature">The exploration temperature; must be positive.</param>
    /// <param name="random">The random source the sample is drawn from.</param>
    /// <returns>The chosen direction and the probabilities it was drawn from.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="activations"/> or <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="activations"/> is the wrong length.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="temperature"/> is not positive.</exception>
    public MotorDecision Select(IReadOnlyList<double> activations, double temperature, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        double[] probabilities = Policy(activations, temperature);

        // Inverse-CDF sample: first bucket the draw falls in; last direction is the fallback.
        double draw = random.NextDouble();
        double cumulative = 0.0;
        int choice = _motorCount - 1;
        for (int k = 0; k < _motorCount; k++)
        {
            cumulative += probabilities[k];
            if (draw < cumulative)
            {
                choice = k;
                break;
            }
        }

        return new MotorDecision(choice, probabilities);
    }
}

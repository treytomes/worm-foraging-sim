namespace WormForaging.Core;

/// <summary>
/// The online learning rule: a neuromodulated, three-factor Hebbian update on the motor readout
/// weights. This is the whole point of the project.
/// </summary>
/// <remarks>
/// <para>
/// <b>No backpropagation. No gradients. No automatic differentiation.</b> A readout weight changes
/// by a function of only three things available right at that synapse: its own eligibility trace
/// <c>e[i]</c>, its own current value <c>Wo[i]</c>, and one scalar <c>modulator</c> broadcast to
/// every synapse alike. There is no backward pass and no cross-weight term — <see cref="ApplyReadoutUpdate"/>
/// could run on each weight in isolation and give the same answer. That locality is the constraint
/// the experiment exists to test, not an implementation detail.
/// </para>
/// <para>
/// The rule has four parts, driven by the simulation step in order:
/// </para>
/// <list type="number">
/// <item><description>
/// <see cref="AccumulateEligibility"/> — at action time, decay the eligibility trace and add the
/// policy-gradient term <c>(onehot − p)·x</c> for the chosen action.
/// </description></item>
/// <item><description>
/// <see cref="UpdateRewardPrediction"/> — after acting, track running baselines and scales of the
/// odor and discomfort changes, and return each as a normalized, clipped "surprise" signal.
/// </description></item>
/// <item><description>
/// <see cref="Modulator"/> — combine the two surprise signals, weighted by how urgent each need
/// is, into the single broadcast scalar (or a fixed/zero value in the other modes).
/// </description></item>
/// <item><description>
/// <see cref="ApplyReadoutUpdate"/> — nudge each readout weight by <c>lr·mod·e[i] − decay·Wo[i]</c>,
/// clamped. Skipped entirely when <see cref="Mode"/> is <see cref="LearningMode.Frozen"/>.
/// </description></item>
/// </list>
/// </remarks>
public sealed class LearningRule
{
    private readonly SimConfig _config;
    private readonly int _weightCount; // K*N
    private readonly int _motorCount;  // K
    private readonly int _neuronCount; // N
    private readonly double[] _eligibility;

    private double _odorBaseline;
    private double _odorScale;
    private double _thermalBaseline;
    private double _thermalScale;

    /// <summary>Creates the learning rule with a zero eligibility trace and the configured initial
    /// reward-prediction state.</summary>
    /// <param name="config">The simulation constants.</param>
    /// <param name="mode">The starting learning mode (defaults to <see cref="LearningMode.Full"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
    public LearningRule(SimConfig config, LearningMode mode = LearningMode.Full)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _motorCount = config.MotorCount;
        _neuronCount = config.InterneuronCount;
        _weightCount = _motorCount * _neuronCount;
        _eligibility = new double[_weightCount];

        Mode = mode;
        _odorBaseline = config.InitialOdorRewardBaseline;
        _odorScale = config.InitialOdorRewardScale;
        _thermalBaseline = config.InitialThermalRewardBaseline;
        _thermalScale = config.InitialThermalRewardScale;
    }

    /// <summary>The learning mode. Settable at runtime, matching the prototype's live dropdown.</summary>
    public LearningMode Mode { get; set; }

    /// <summary>Live, read-only view of the eligibility trace (motor-major, length motors·neurons). Do not mutate.</summary>
    public IReadOnlyList<double> EligibilityTrace => _eligibility;

    /// <summary>Running baseline of the odor change (the prototype's <c>bo</c>).</summary>
    public double OdorBaseline => _odorBaseline;

    /// <summary>Running scale of the odor-change surprise (the prototype's <c>ro</c>).</summary>
    public double OdorScale => _odorScale;

    /// <summary>Running baseline of the discomfort change (the prototype's <c>bt</c>).</summary>
    public double ThermalBaseline => _thermalBaseline;

    /// <summary>Running scale of the discomfort-change surprise (the prototype's <c>rt</c>).</summary>
    public double ThermalScale => _thermalScale;

    /// <summary>
    /// Decays the eligibility trace and accumulates the policy-gradient term for the chosen action:
    /// <c>e[k,j] = decay·e[k,j] + ((k == choice ? 1 : 0) − p[k])·x[j]</c>.
    /// </summary>
    /// <param name="decision">The action selected this step, carrying the choice and probabilities.</param>
    /// <param name="activations">The interneuron activations this step, of length neurons.</param>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> or <paramref name="activations"/> is null.</exception>
    /// <exception cref="ArgumentException">The activation or probability lengths do not match the configured sizes.</exception>
    public void AccumulateEligibility(MotorDecision decision, IReadOnlyList<double> activations)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(activations);
        if (activations.Count != _neuronCount)
        {
            throw new ArgumentException(
                $"Expected {_neuronCount} activations but got {activations.Count}.", nameof(activations));
        }

        if (decision.Probabilities.Count != _motorCount)
        {
            throw new ArgumentException(
                $"Expected {_motorCount} probabilities but got {decision.Probabilities.Count}.", nameof(decision));
        }

        for (int k = 0; k < _motorCount; k++)
        {
            double y = (k == decision.Choice ? 1.0 : 0.0) - decision.Probabilities[k];
            int row = k * _neuronCount;
            for (int j = 0; j < _neuronCount; j++)
            {
                _eligibility[row + j] = _config.EligibilityDecay * _eligibility[row + j] + y * activations[j];
            }
        }
    }

    /// <summary>
    /// Updates the running reward-prediction baselines and scales from this step's raw changes, then
    /// returns each change as a normalized, clipped surprise signal.
    /// </summary>
    /// <param name="odorChange">The change in odor concentration from the move (the prototype's <c>dc</c>).</param>
    /// <param name="discomfortReduction">The drop in thermal discomfort from the move (the prototype's <c>dt</c>).</param>
    /// <returns>The odor and thermal surprise signals, each clipped to ±<see cref="SimConfig.RewardClip"/>.</returns>
    /// <remarks>
    /// The baselines update first, so the surprise is measured against the just-updated prediction —
    /// matching the prototype's statement order.
    /// </remarks>
    public (double Odor, double Thermal) UpdateRewardPrediction(double odorChange, double discomfortReduction)
    {
        double rate = _config.RewardPredictionRate;

        _odorBaseline += rate * (odorChange - _odorBaseline);
        _odorScale += rate * (Math.Abs(odorChange - _odorBaseline) - _odorScale);
        _thermalBaseline += rate * (discomfortReduction - _thermalBaseline);
        _thermalScale += rate * (Math.Abs(discomfortReduction - _thermalBaseline) - _thermalScale);

        double odor = Clip(
            (odorChange - _odorBaseline) / (_odorScale + _config.OdorRewardScaleEpsilon),
            _config.RewardClip);
        double thermal = Clip(
            (discomfortReduction - _thermalBaseline) / (_thermalScale + _config.ThermalRewardScaleEpsilon),
            _config.RewardClip);

        return (odor, thermal);
    }

    /// <summary>
    /// Computes the broadcast modulator scalar for this step from the two surprise signals, weighted
    /// by how urgent each need is — or a fixed/zero value in the non-reward modes.
    /// </summary>
    /// <param name="odorSurprise">The odor surprise signal from <see cref="UpdateRewardPrediction"/>.</param>
    /// <param name="thermalSurprise">The thermal surprise signal from <see cref="UpdateRewardPrediction"/>.</param>
    /// <param name="hunger">The hunger drive at the start of the step (urgency of the food need).</param>
    /// <param name="discomfort">The thermal discomfort after moving (urgency of the thermal need).</param>
    /// <param name="absorbed">Food absorbed this step (the prototype's <c>ab</c>), counted as hunger reward.</param>
    /// <returns>
    /// The modulator: in <see cref="LearningMode.Full"/> the competing-needs reward
    /// <c>wH·(odor + absorbed·50) + wT·thermal</c>; in <see cref="LearningMode.Hebbian"/> the fixed
    /// constant; in <see cref="LearningMode.Frozen"/> zero.
    /// </returns>
    public double Modulator(double odorSurprise, double thermalSurprise, double hunger, double discomfort, double absorbed)
    {
        double hungerWeight = _config.HungerRewardWeightBase + hunger;
        double thermalWeight = _config.ThermalRewardWeightBase + _config.ThermalRewardWeightScale * discomfort;
        double reward = hungerWeight * (odorSurprise + absorbed * _config.AbsorptionRewardScale)
            + thermalWeight * thermalSurprise;

        return Mode switch
        {
            LearningMode.Full => reward,
            LearningMode.Hebbian => _config.HebbianModulator,
            LearningMode.Frozen => 0.0,
            _ => 0.0,
        };
    }

    /// <summary>
    /// Applies the modulated local update to every readout weight:
    /// <c>Wo[i] = clip(Wo[i] + lr·mod·e[i] − decay·Wo[i])</c>. Does nothing in
    /// <see cref="LearningMode.Frozen"/> mode (the weights stay exactly fixed, with no decay).
    /// </summary>
    /// <param name="motor">The motor layer whose readout weights are updated in place.</param>
    /// <param name="modulator">The broadcast scalar from <see cref="Modulator"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="motor"/> is null.</exception>
    /// <exception cref="ArgumentException">The motor's readout size does not match the eligibility trace.</exception>
    public void ApplyReadoutUpdate(MotorLayer motor, double modulator)
    {
        ArgumentNullException.ThrowIfNull(motor);
        if (Mode == LearningMode.Frozen)
        {
            return;
        }

        if (motor.ReadoutWeightCount != _weightCount)
        {
            throw new ArgumentException(
                $"Motor readout has {motor.ReadoutWeightCount} weights but the eligibility trace has {_weightCount}.",
                nameof(motor));
        }

        for (int i = 0; i < _weightCount; i++)
        {
            double w = motor.GetReadoutWeight(i);
            double updated = Clip(
                w + _config.LearningRate * modulator * _eligibility[i] - _config.WeightDecay * w,
                _config.ReadoutClip);
            motor.SetReadoutWeight(i, updated);
        }
    }

    private static double Clip(double value, double magnitude)
        => Math.Max(-magnitude, Math.Min(magnitude, value));
}

namespace WormForaging.Core;

/// <summary>
/// The complete set of tunable constants for the simulation, in one place.
/// </summary>
/// <remarks>
/// <para>
/// Every value here began life as an inline literal in the browser prototype
/// (<c>worm_foraging.html</c>), where the author flagged most of them as untuned guesses.
/// Porting them is fine; scattering them through the step loop is not. This record is the
/// single source of truth, so an experiment changes behavior by constructing a modified
/// config (via <c>with</c>), never by editing code.
/// </para>
/// <para>
/// The prototype computes in single precision (<c>Float32Array</c>); this port computes in
/// <see cref="double"/>. The goal is <em>qualitative</em> parity with the prototype, not a
/// bit-for-bit match to JavaScript. Determinism is defined against this implementation: the
/// same seed and the same config yield a bit-identical C# run.
/// </para>
/// <para>
/// Fields are grouped by concern. Values are the prototype defaults unless a comment says
/// otherwise; "(guessed)" marks a value the prototype author had not tuned.
/// </para>
/// </remarks>
public sealed record SimConfig
{
    // ---- Structure (fixed sizes; the prototype's N, K, NS, AR, COMF) ----

    /// <summary>Side length of the square arena, in cells. Prototype <c>AR = 40</c>.</summary>
    public int ArenaSize { get; init; } = 40;

    /// <summary>Number of recurrent interneurons. Prototype <c>N = 16</c>.</summary>
    public int InterneuronCount { get; init; } = 16;

    /// <summary>Number of motor neurons / movement directions (N, E, S, W). Prototype <c>K = 4</c>.</summary>
    public int MotorCount { get; init; } = 4;

    /// <summary>Total sensor channels: 4 odor + 4 heat. Prototype <c>NS = 8</c>.</summary>
    public int SensorCount { get; init; } = 8;

    /// <summary>Comfortable body temperature; discomfort is distance from this. Prototype <c>COMF = 0.55</c>.</summary>
    public double ComfortTemperature { get; init; } = 0.55;

    // ---- Odor field: conc(x,y) = exp(-dist(x,y → food) / OdorLengthScale) ----

    /// <summary>Decay length of the food odor field. Prototype <c>/12</c> in <c>conc</c>.</summary>
    public double OdorLengthScale { get; init; } = 12.0;

    // ---- Heat lamp: a moving Gaussian hot-spot on a Lissajous orbit ----

    /// <summary>Ambient temperature away from the lamp. Prototype <c>0.25</c> in <c>temp</c>.</summary>
    public double LampTempBaseline { get; init; } = 0.25;

    /// <summary>Peak temperature rise at the lamp centre. Prototype <c>0.9</c> in <c>temp</c>.</summary>
    public double LampTempAmplitude { get; init; } = 0.9;

    /// <summary>Squared-distance decay scale of the lamp's heat. Prototype <c>/128</c> in <c>temp</c>.</summary>
    public double LampTempLengthScale { get; init; } = 128.0;

    /// <summary>Centre of the lamp's circular orbit (both axes). Prototype <c>20</c>.</summary>
    public double LampOrbitCenter { get; init; } = 20.0;

    /// <summary>Radius of the lamp's orbit. Prototype <c>13</c>.</summary>
    public double LampOrbitRadius { get; init; } = 13.0;

    /// <summary>Angular speed of the lamp on the x-axis. Prototype <c>0.006</c>.</summary>
    public double LampAngularSpeedX { get; init; } = 0.006;

    /// <summary>Angular speed of the lamp on the y-axis; differs from x to trace a Lissajous curve. Prototype <c>0.0041</c>.</summary>
    public double LampAngularSpeedY { get; init; } = 0.0041;

    // ---- Food placement (the prototype's spawn()) ----

    /// <summary>Inset of the food-spawn box from the arena edge. Prototype <c>4</c> in <c>spawn</c>.</summary>
    public double FoodSpawnMargin { get; init; } = 4.0;

    /// <summary>Side length of the food-spawn box (so food lands in [margin, margin+span)). Prototype <c>32</c>.</summary>
    public double FoodSpawnSpan { get; init; } = 32.0;

    /// <summary>Minimum distance new food must keep from a reference point (so it never spawns on the worm). Prototype <c>12</c>.</summary>
    public double FoodSpawnMinDistance { get; init; } = 12.0;

    /// <summary>Distance within which the worm eats the food. Prototype <c>1.8</c>.</summary>
    public double EatRadius { get; init; } = 1.8;

    /// <summary>Food added to the gut when a meal is eaten. Prototype <c>0.6</c>.</summary>
    public double GutRefillPerMeal { get; init; } = 0.6;

    // ---- Body, movement, metabolism ----

    /// <summary>Starting energy, mid-range. Prototype <c>energy = 0.5</c>.</summary>
    public double InitialEnergy { get; init; } = 0.5;

    /// <summary>Distance ahead the worm samples odor/heat in each direction. Prototype <c>1.2</c>.</summary>
    public double SensorSampleDistance { get; init; } = 1.2;

    /// <summary>How fast body temperature relaxes toward local temperature per step. Prototype <c>0.08</c>.</summary>
    public double BodyTempLag { get; init; } = 0.08;

    /// <summary>Food absorbed from the gut into energy per step. Prototype <c>0.01</c>.</summary>
    public double AbsorptionRate { get; init; } = 0.01;

    /// <summary>Baseline energy drain per step. Prototype <c>0.002</c>.</summary>
    public double BaseMetabolicCost { get; init; } = 0.002;

    /// <summary>Extra energy drain per unit of thermal discomfort. Prototype <c>0.006</c>.</summary>
    public double ThermalDiscomfortCost { get; init; } = 0.006;

    // ---- Senses (the prototype's s[] construction) ----

    /// <summary>Floor of the odor-sensor gain gate. Prototype <c>0.3</c> in <c>gate</c>.</summary>
    public double OdorGateBase { get; init; } = 0.3;

    /// <summary>How much hunger opens the odor gate. Prototype <c>0.7</c> in <c>gate</c>.</summary>
    public double OdorGateHungerScale { get; init; } = 0.7;

    /// <summary>Overall odor-sensor gain. Prototype <c>10</c>.</summary>
    public double OdorSensorGain { get; init; } = 10.0;

    /// <summary>Guards the odor Weber normalization against divide-by-zero. Prototype <c>1e-6</c>.</summary>
    public double OdorNormalizationEpsilon { get; init; } = 1e-6;

    /// <summary>Converts thermal deviation into a drive (negative: too hot pushes to cool). Prototype <c>-dev*6</c>.</summary>
    public double ThermalDriveScale { get; init; } = 6.0;

    /// <summary>Overall heat-sensor gain. Prototype <c>6</c> in <c>s[4+i]</c>.</summary>
    public double HeatSensorGain { get; init; } = 6.0;

    // ---- Network init and drive inputs ----

    /// <summary>Std-dev scale of input weights <c>Win</c> (Gaussian). Prototype <c>randn()*0.7</c>.</summary>
    public double InputWeightScale { get; init; } = 0.7;

    /// <summary>Probability that any recurrent connection <c>Wrec[i,j]</c> exists (sparse wiring). Prototype <c>0.2</c>.</summary>
    public double RecurrentConnectionProbability { get; init; } = 0.2;

    /// <summary>Std-dev scale of present recurrent weights (Gaussian). Prototype <c>randn()*0.35</c>.</summary>
    public double RecurrentWeightScale { get; init; } = 0.35;

    /// <summary>Std-dev scale of the hunger-drive weights <c>wh</c>. Prototype <c>randn()*0.5</c>.</summary>
    public double HungerWeightScale { get; init; } = 0.5;

    /// <summary>Std-dev scale of the thermal-drive weights <c>wt</c>. Prototype <c>randn()*0.5</c>.</summary>
    public double ThermalWeightScale { get; init; } = 0.5;

    /// <summary>Std-dev scale of interneuron biases <c>b</c>. Prototype <c>randn()*0.1</c>.</summary>
    public double BiasScale { get; init; } = 0.1;

    /// <summary>Coefficient on the hunger drive into each interneuron. Prototype <c>wh[i]*hunger</c> (implicit 1).</summary>
    public double HungerInputScale { get; init; } = 1.0;

    /// <summary>Coefficient on the thermal-deviation drive into each interneuron. Prototype <c>wt[i]*dev*3</c>.</summary>
    public double ThermalInputScale { get; init; } = 3.0;

    /// <summary>Std-dev scale of the readout weights <c>Wo</c> at init. Prototype <c>randn()*0.1</c>.</summary>
    public double ReadoutInitScale { get; init; } = 0.1;

    // ---- Homeostatic gain control ----

    /// <summary>Initial per-neuron gain. Prototype <c>g = 1</c>.</summary>
    public double InitialGain { get; init; } = 1.0;

    /// <summary>Initial per-neuron activity trace. Prototype <c>ax = 0.4</c>.</summary>
    public double InitialActivityTrace { get; init; } = 0.4;

    /// <summary>Rate the activity trace <c>ax</c> tracks |activity|. Prototype <c>0.01</c>.</summary>
    public double ActivityTraceRate { get; init; } = 0.01;

    /// <summary>Rate the homeostatic gain adapts toward the target activity. Prototype <c>0.002</c>.</summary>
    public double GainAdaptRate { get; init; } = 0.002;

    /// <summary>Target average activity the gain controller aims for. Prototype <c>0.4</c>.</summary>
    public double GainActivityTarget { get; init; } = 0.4;

    /// <summary>Lower clamp on per-neuron gain. Prototype <c>0.3</c>.</summary>
    public double GainMin { get; init; } = 0.3;

    /// <summary>Upper clamp on per-neuron gain. Prototype <c>3</c>.</summary>
    public double GainMax { get; init; } = 3.0;

    // ---- Action selection ----

    /// <summary>Default softmax temperature (the "explore" control; prototype slider 0.3–3, default 1).</summary>
    public double DefaultExploreTemperature { get; init; } = 1.0;

    // ---- Learning: eligibility trace, reward shaping, readout update ----

    /// <summary>Decay of the eligibility trace per step. Prototype <c>0.7</c>.</summary>
    public double EligibilityDecay { get; init; } = 0.7;

    /// <summary>Rate the running reward-prediction baselines and scales update. Prototype <c>0.01</c> (all four).</summary>
    public double RewardPredictionRate { get; init; } = 0.01;

    /// <summary>Guards the odor reward-surprise normalization. Prototype <c>1e-4</c> (ro + 1e-4).</summary>
    public double OdorRewardScaleEpsilon { get; init; } = 1e-4;

    /// <summary>Guards the thermal reward-surprise normalization. Prototype <c>0.002</c> (rt + 0.002). Note the asymmetry with the odor epsilon — flagged for review.</summary>
    public double ThermalRewardScaleEpsilon { get; init; } = 0.002;

    /// <summary>Symmetric clamp on each normalized reward-surprise signal. Prototype <c>clip(...,3)</c>.</summary>
    public double RewardClip { get; init; } = 3.0;

    /// <summary>Floor weight on the hunger reward channel; grows with hunger. Prototype <c>0.2 + hunger</c>.</summary>
    public double HungerRewardWeightBase { get; init; } = 0.2;

    /// <summary>Floor weight on the thermal reward channel; grows with discomfort. Prototype <c>0.2 + 4*d1</c>.</summary>
    public double ThermalRewardWeightBase { get; init; } = 0.2;

    /// <summary>How fast the thermal reward weight grows with discomfort. Prototype <c>4</c> in <c>wT</c>.</summary>
    public double ThermalRewardWeightScale { get; init; } = 4.0;

    /// <summary>How strongly absorbed food counts as hunger reward. Prototype <c>ab*50</c>.</summary>
    public double AbsorptionRewardScale { get; init; } = 50.0;

    /// <summary>Readout learning rate. Prototype <c>0.04</c>.</summary>
    public double LearningRate { get; init; } = 0.04;

    /// <summary>Readout weight decay per step. Prototype <c>1e-4</c>.</summary>
    public double WeightDecay { get; init; } = 1e-4;

    /// <summary>Symmetric clamp on each readout weight. Prototype <c>clip(...,4)</c>.</summary>
    public double ReadoutClip { get; init; } = 4.0;

    /// <summary>Fixed modulator used in "plain Hebbian" mode. Prototype <c>0.6</c> — arbitrary, flagged for review.</summary>
    public double HebbianModulator { get; init; } = 0.6;

    // ---- Initial reward-prediction state (the prototype's bo, ro, bt, rt) ----

    /// <summary>Initial odor reward-prediction baseline. Prototype <c>bo = 0</c>.</summary>
    public double InitialOdorRewardBaseline { get; init; } = 0.0;

    /// <summary>Initial odor reward-prediction scale. Prototype <c>ro = 0.01</c>.</summary>
    public double InitialOdorRewardScale { get; init; } = 0.01;

    /// <summary>Initial thermal reward-prediction baseline. Prototype <c>bt = 0</c>.</summary>
    public double InitialThermalRewardBaseline { get; init; } = 0.0;

    /// <summary>Initial thermal reward-prediction scale. Prototype <c>rt = 0.002</c>.</summary>
    public double InitialThermalRewardScale { get; init; } = 0.002;
}

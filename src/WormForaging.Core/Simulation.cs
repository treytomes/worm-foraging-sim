using System.Linq;
using WormForaging.Core.Rng;

namespace WormForaging.Core;

/// <summary>
/// The whole agent-in-world loop: it owns the world, body, senses, network, motor layer, and
/// learning rule, and advances them one step at a time in the prototype's exact order.
/// </summary>
/// <remarks>
/// <para>
/// This is the last Core piece and the one that makes the parts a simulation. It is pure and
/// deterministic: constructed from a <see cref="SimConfig"/> and an <see cref="IRandom"/>, a given
/// seed reproduces the entire run bit-for-bit (guarded by a test). The UI and the harness both
/// drive it the same way — call <see cref="Step"/> to advance, <see cref="Snapshot"/> to read.
/// </para>
/// <para>
/// <b>Step order</b> follows <c>worm_foraging.html</c>'s <c>step()</c> precisely, because the
/// couplings are order-sensitive. In particular the hunger and thermal drives are read once at the
/// top of the step (before the body moves or eats) and reused for both the network input and the
/// reward modulator, and the odor "did I get closer" signal is measured across the move but before
/// metabolism respawns the food.
/// </para>
/// <para>
/// <b>Reset</b> is construction: make a new <see cref="Simulation"/> with a fresh seed. There is no
/// in-place reset, so a run's state can never be half-cleared.
/// </para>
/// </remarks>
public sealed class Simulation
{
    private readonly SimConfig _config;
    private readonly IRandom _random;
    private readonly World _world;
    private readonly Body _body;
    private readonly Senses _senses;
    private readonly Network _network;
    private readonly MotorLayer _motor;
    private readonly LearningRule _learning;

    private double _exploreTemperature;
    private long _step;
    private NetworkTopology? _topology;

    /// <summary>
    /// Builds a fresh simulation: the worm starts at the arena centre, food is placed clear of it,
    /// and all weights are drawn from <paramref name="random"/> in a fixed order (world, network,
    /// motor) so the seed reproduces the run.
    /// </summary>
    /// <param name="config">The simulation constants.</param>
    /// <param name="random">The deterministic random source for the whole run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="random"/> is null.</exception>
    public Simulation(SimConfig config, IRandom random)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _random = random ?? throw new ArgumentNullException(nameof(random));

        double start = config.ArenaSize / 2.0;
        _world = new World(config, random, start, start); // draws food placement
        _network = new Network(config, random);           // draws Win, Wrec, wh, wt, b
        _motor = new MotorLayer(config, random);          // draws Wo
        _body = new Body(config, start, start);
        _senses = new Senses(config);
        _learning = new LearningRule(config);

        _exploreTemperature = config.DefaultExploreTemperature;
        HomeostasisEnabled = true;
        Permutation = SensePermutation.Identity;
    }

    /// <summary>The learning mode (delegates to the learning rule); settable at runtime.</summary>
    public LearningMode Mode
    {
        get => _learning.Mode;
        set => _learning.Mode = value;
    }

    /// <summary>The softmax exploration temperature; must be positive.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Set to a non-positive value.</exception>
    public double ExploreTemperature
    {
        get => _exploreTemperature;
        set
        {
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Temperature must be positive.");
            }

            _exploreTemperature = value;
        }
    }

    /// <summary>Whether homeostatic gain adaptation runs each step (the prototype's checkbox).</summary>
    public bool HomeostasisEnabled { get; set; }

    /// <summary>The current sensor-to-direction mapping; changing it is the "swap senses" action.</summary>
    public SensePermutation Permutation { get; set; }

    /// <summary>The number of steps taken so far.</summary>
    public long StepCount => _step;

    /// <summary>
    /// The network's fixed wiring, for a UI to draw a diagram from. Built once on first access and
    /// cached, because the reservoir never changes; the learned readout and the live activations are
    /// read from <see cref="Snapshot"/> instead.
    /// </summary>
    public NetworkTopology Topology => _topology ??= new NetworkTopology(
        NeuronCount: _network.NeuronCount,
        SensorCount: _network.SensorCount,
        MotorCount: _config.MotorCount,
        InputWeights: _network.InputWeights.ToArray(),
        RecurrentWeights: _network.RecurrentWeights.ToArray(),
        HungerWeights: _network.HungerWeights.ToArray(),
        ThermalWeights: _network.ThermalWeights.ToArray(),
        Biases: _network.Biases.ToArray());

    /// <summary>Toggles the sense swap between the identity and swapped mappings.</summary>
    public void SwapSenses() => Permutation = Permutation.Toggle();

    /// <summary>
    /// Advances the simulation by one step, in the prototype's order: move the lamp, sense, run the
    /// network, choose and record an action, act, shape the reward, metabolise, apply the learning
    /// update, and run homeostasis.
    /// </summary>
    public void Step()
    {
        // Drives read once at the top of the step and reused below (energy/temperature change later).
        double hunger = _body.Hunger;
        double thermalDeviation = _body.ThermalDeviation;

        _world.Advance();                                                 // 1. move the heat lamp
        double[] sensors = _senses.Sense(_body, _world, Permutation);     // 2. sense the surroundings
        double[] activations = _network.Activate(sensors, hunger, thermalDeviation); // 3. interneurons
        MotorDecision decision = _motor.Select(activations, _exploreTemperature, _random); // 4. choose
        _learning.AccumulateEligibility(decision, activations);           // 5. tag the eligibility trace

        double odorBefore = _world.OdorAt(_body.X, _body.Y);              // 6. capture pre-move state
        double discomfortBefore = _body.Discomfort;

        (int dx, int dy) = Directions.All[decision.Choice];
        _body.Move(dx, dy, _world);                                       // 7. act

        double odorChange = _world.OdorAt(_body.X, _body.Y) - odorBefore; // 8. what the move achieved
        double discomfortAfter = _body.Discomfort;
        double discomfortReduction = discomfortBefore - discomfortAfter;

        (double odorSurprise, double thermalSurprise) =
            _learning.UpdateRewardPrediction(odorChange, discomfortReduction); // 9. normalise surprise

        double absorbed = _body.Metabolize(_world);                      // 10. eat and digest

        double modulator = _learning.Modulator(                         // 11. the broadcast scalar
            odorSurprise, thermalSurprise, hunger, discomfortAfter, absorbed);
        _learning.ApplyReadoutUpdate(_motor, modulator);                // 12. local readout update

        _network.AdaptAndCommit(activations, HomeostasisEnabled);       // 13. homeostasis + recurrent state

        _step++;
    }

    /// <summary>
    /// Takes an immutable snapshot of the current state, copying the arrays so it stays valid after
    /// later steps.
    /// </summary>
    /// <returns>The current <see cref="SimulationSnapshot"/>.</returns>
    public SimulationSnapshot Snapshot()
        => new(
            Step: _step,
            WormX: _body.X,
            WormY: _body.Y,
            FoodX: _world.FoodX,
            FoodY: _world.FoodY,
            LampX: _world.LampX,
            LampY: _world.LampY,
            Energy: _body.Energy,
            Gut: _body.Gut,
            BodyTemperature: _body.Temperature,
            MealsEaten: _body.MealsEaten,
            MealIntervals: _body.MealIntervals.ToArray(),
            Activations: _network.Activations.ToArray(),
            ReadoutWeights: _motor.ReadoutWeights.ToArray(),
            Modulator: _learning.LastModulator,
            FoodDrive: _learning.LastFoodDrive,
            ThermalDrive: _learning.LastThermalDrive);
}

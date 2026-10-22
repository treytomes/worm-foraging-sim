namespace WormForaging.Core;

/// <summary>
/// The worm's body: where it is, how much energy it has, what's in its gut, and how warm it is.
/// Owns the state transitions for moving, thermoregulating, eating, and metabolising.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the prototype's per-step body updates: the position clamp and
/// <c>body += 0.08*(temp(x,y) - body)</c> thermal lag (<see cref="Move"/>), and the
/// gut/energy block — eat on contact, absorb slowly, pay a baseline plus a thermal-discomfort
/// cost (<see cref="Metabolize"/>).
/// </para>
/// <para>
/// The body reads the environment through <see cref="World"/> but holds no sensing, network, or
/// learning logic. The reward signals the learning rule needs (odor change, discomfort change)
/// are derived by the caller from this body's observable state before and after a step; the body
/// itself only reports the one quantity that is otherwise lost — the food <em>absorbed</em> this
/// step — as the return of <see cref="Metabolize"/>.
/// </para>
/// </remarks>
public sealed class Body
{
    private readonly SimConfig _config;
    private readonly List<long> _mealIntervals = new();

    /// <summary>
    /// Creates a body at a start position with full-comfort temperature and the configured
    /// initial energy, an empty gut, and no meals eaten.
    /// </summary>
    /// <param name="config">The simulation constants.</param>
    /// <param name="startX">Initial x position, in arena cells.</param>
    /// <param name="startY">Initial y position, in arena cells.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
    public Body(SimConfig config, double startX, double startY)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        X = startX;
        Y = startY;
        Energy = config.InitialEnergy;
        Gut = 0.0;
        Temperature = config.ComfortTemperature;
        StepsSinceMeal = 0;
    }

    /// <summary>The worm's x position, in arena cells.</summary>
    public double X { get; private set; }

    /// <summary>The worm's y position, in arena cells.</summary>
    public double Y { get; private set; }

    /// <summary>Energy reserve, clamped to [0, 1]. Prototype <c>energy</c>.</summary>
    public double Energy { get; private set; }

    /// <summary>Undigested food waiting to be absorbed into energy. Prototype <c>gut</c>.</summary>
    public double Gut { get; private set; }

    /// <summary>Body temperature, relaxing toward local temperature each move. Prototype <c>body</c>.</summary>
    public double Temperature { get; private set; }

    /// <summary>Steps elapsed since the last meal (reset to 0 when a meal is eaten). Prototype <c>since</c>.</summary>
    public long StepsSinceMeal { get; private set; }

    /// <summary>Number of meals eaten so far.</summary>
    public int MealsEaten => _mealIntervals.Count;

    /// <summary>
    /// The number of steps each meal took to reach, in order — the prototype's <c>meals</c> array,
    /// the raw material for the "steps per meal" metric.
    /// </summary>
    public IReadOnlyList<long> MealIntervals => _mealIntervals;

    /// <summary>Unmet hunger drive: <c>1 - Energy</c>, in [0, 1]. Prototype <c>hunger</c>.</summary>
    public double Hunger => 1.0 - Energy;

    /// <summary>Signed thermal deviation from comfort: positive when too hot. Prototype <c>dev</c>.</summary>
    public double ThermalDeviation => Temperature - _config.ComfortTemperature;

    /// <summary>Thermal discomfort: the magnitude of <see cref="ThermalDeviation"/>. Prototype <c>d1</c>/<c>d0</c>.</summary>
    public double Discomfort => Math.Abs(ThermalDeviation);

    /// <summary>
    /// Moves one cell in the chosen direction (clamped to stay inside the arena interior) and
    /// relaxes body temperature toward the local temperature at the new cell.
    /// </summary>
    /// <param name="deltaX">Step in x: -1, 0, or +1.</param>
    /// <param name="deltaY">Step in y: -1, 0, or +1.</param>
    /// <param name="world">The world, read for the temperature at the new position.</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is null.</exception>
    public void Move(int deltaX, int deltaY, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        double max = _config.ArenaSize - 1;
        X = Math.Min(max, Math.Max(1.0, X + deltaX));
        Y = Math.Min(max, Math.Max(1.0, Y + deltaY));

        Temperature += _config.BodyTempLag * (world.TemperatureAt(X, Y) - Temperature);
    }

    /// <summary>
    /// Runs one step of metabolism: eat if food is within reach, absorb a little gut food into
    /// energy, and pay the baseline and thermal-discomfort energy costs.
    /// </summary>
    /// <param name="world">The world, checked for food in reach and told to respawn an eaten meal.</param>
    /// <returns>The amount of food absorbed into energy this step (the prototype's <c>ab</c>).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> is null.</exception>
    /// <remarks>
    /// Call after <see cref="Move"/> in a step so <see cref="Discomfort"/> reflects the new cell,
    /// matching the prototype's ordering. The returned absorbed amount feeds the hunger reward
    /// channel in the learning rule (prototype <c>ab*50</c>); it is returned rather than stored
    /// because nothing else preserves it.
    /// </remarks>
    public double Metabolize(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        StepsSinceMeal++;
        if (world.IsFoodWithinReach(X, Y))
        {
            Gut += _config.GutRefillPerMeal;
            _mealIntervals.Add(StepsSinceMeal);
            StepsSinceMeal = 0;
            world.RespawnFood(X, Y);
        }

        double absorbed = Math.Min(Gut, _config.AbsorptionRate);
        Gut -= absorbed;

        double energy = Energy
            + absorbed
            - _config.BaseMetabolicCost
            - _config.ThermalDiscomfortCost * Discomfort;
        Energy = Math.Max(0.0, Math.Min(1.0, energy));

        return absorbed;
    }
}

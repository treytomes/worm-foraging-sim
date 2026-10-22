using WormForaging.Core.Rng;

namespace WormForaging.Core;

/// <summary>
/// The physical environment the worm lives in: a square arena carrying a food odor field and a
/// moving heat lamp, plus the single piece of food currently present.
/// </summary>
/// <remarks>
/// <para>
/// This type owns only the <em>environment</em> — the odor and temperature fields, the lamp's
/// motion, and where the food is. It deliberately knows nothing about the worm's body, senses,
/// network, or learning; those live in their own types and read the world through
/// <see cref="OdorAt"/> and <see cref="TemperatureAt"/>. Eating is reported, not enacted:
/// <see cref="IsFoodWithinReach"/> tells a caller a meal is available and <see cref="RespawnFood"/>
/// moves the food, but the gut/energy consequences belong to the body.
/// </para>
/// <para>
/// Ported from the prototype's <c>conc</c>, <c>temp</c>, and <c>spawn</c> functions and the
/// lamp update at the top of <c>step</c>. All randomness flows through the injected
/// <see cref="IRandom"/>, so a given seed reproduces the same food placements.
/// </para>
/// </remarks>
public sealed class World
{
    private readonly SimConfig _config;
    private readonly IRandom _random;

    /// <summary>
    /// Creates a world and places the first piece of food away from the worm's start position.
    /// </summary>
    /// <param name="config">The simulation constants.</param>
    /// <param name="random">The deterministic random source used for food placement.</param>
    /// <param name="worldStartX">The worm's initial x position, kept clear of food.</param>
    /// <param name="worldStartY">The worm's initial y position, kept clear of food.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="random"/> is null.</exception>
    public World(SimConfig config, IRandom random, double worldStartX, double worldStartY)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _random = random ?? throw new ArgumentNullException(nameof(random));

        UpdateLamp();
        RespawnFood(worldStartX, worldStartY);
    }

    /// <summary>The current simulation step count; drives the lamp's position.</summary>
    public long Time { get; private set; }

    /// <summary>The food's x position, in arena cells.</summary>
    public double FoodX { get; private set; }

    /// <summary>The food's y position, in arena cells.</summary>
    public double FoodY { get; private set; }

    /// <summary>The heat lamp's x position, in arena cells.</summary>
    public double LampX { get; private set; }

    /// <summary>The heat lamp's y position, in arena cells.</summary>
    public double LampY { get; private set; }

    /// <summary>
    /// The food odor concentration sensed at a point: <c>exp(-distance-to-food / OdorLengthScale)</c>,
    /// so it peaks at 1 on the food and decays with distance.
    /// </summary>
    /// <param name="x">The x position to sample, in arena cells.</param>
    /// <param name="y">The y position to sample, in arena cells.</param>
    /// <returns>Odor concentration in (0, 1].</returns>
    public double OdorAt(double x, double y)
    {
        double distance = Math.Sqrt(Square(x - FoodX) + Square(y - FoodY));
        return Math.Exp(-distance / _config.OdorLengthScale);
    }

    /// <summary>
    /// The temperature at a point: an ambient baseline plus a Gaussian hot-spot centred on the
    /// lamp, <c>baseline + amplitude * exp(-squaredDistanceToLamp / lengthScale)</c>.
    /// </summary>
    /// <param name="x">The x position to sample, in arena cells.</param>
    /// <param name="y">The y position to sample, in arena cells.</param>
    /// <returns>Temperature, ranging from the baseline far away up to baseline + amplitude at the lamp.</returns>
    public double TemperatureAt(double x, double y)
    {
        double squaredDistance = Square(x - LampX) + Square(y - LampY);
        return _config.LampTempBaseline
            + _config.LampTempAmplitude * Math.Exp(-squaredDistance / _config.LampTempLengthScale);
    }

    /// <summary>
    /// Advances world time by one step and moves the heat lamp along its Lissajous orbit.
    /// </summary>
    public void Advance()
    {
        Time++;
        UpdateLamp();
    }

    /// <summary>
    /// Reports whether the food is close enough to eat from the given position.
    /// </summary>
    /// <param name="x">The worm's x position, in arena cells.</param>
    /// <param name="y">The worm's y position, in arena cells.</param>
    /// <returns><see langword="true"/> if within <see cref="SimConfig.EatRadius"/> of the food.</returns>
    public bool IsFoodWithinReach(double x, double y)
    {
        double distance = Math.Sqrt(Square(x - FoodX) + Square(y - FoodY));
        return distance < _config.EatRadius;
    }

    /// <summary>
    /// Moves the food to a fresh random location inside the spawn box, rejecting any point too
    /// close to the given reference position (so it never reappears on top of the worm).
    /// </summary>
    /// <param name="awayFromX">The x position to keep clear.</param>
    /// <param name="awayFromY">The y position to keep clear.</param>
    public void RespawnFood(double awayFromX, double awayFromY)
    {
        double x, y;
        do
        {
            x = _config.FoodSpawnMargin + _random.NextDouble() * _config.FoodSpawnSpan;
            y = _config.FoodSpawnMargin + _random.NextDouble() * _config.FoodSpawnSpan;
        }
        while (Math.Sqrt(Square(x - awayFromX) + Square(y - awayFromY)) < _config.FoodSpawnMinDistance);

        FoodX = x;
        FoodY = y;
    }

    /// <summary>Recomputes the lamp position from the current <see cref="Time"/>.</summary>
    private void UpdateLamp()
    {
        LampX = _config.LampOrbitCenter + _config.LampOrbitRadius * Math.Cos(_config.LampAngularSpeedX * Time);
        LampY = _config.LampOrbitCenter + _config.LampOrbitRadius * Math.Sin(_config.LampAngularSpeedY * Time);
    }

    private static double Square(double v) => v * v;
}

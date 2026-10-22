namespace WormForaging.Core;

/// <summary>
/// Turns the world around the worm into the 8-channel sensor vector the network reads: four odor
/// channels and four heat channels, one per direction.
/// </summary>
/// <remarks>
/// <para>
/// Ported from the prototype's <c>s[]</c> construction. In each direction the worm samples the
/// odor and temperature a short distance ahead. Both sensors are <em>differential</em> — they
/// report how a direction compares to the mean over all four, not the absolute level — so a
/// uniform field reads as zero and only gradients drive behaviour.
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// Odor is gated by hunger (a full worm barely smells food) and Weber-normalized by the mean
/// concentration (equal sensitivity to a gradient whether food is near or far):
/// <c>gate·10·(odor[dir] − meanOdor) / (meanOdor + 1e-6)</c>.
/// </description>
/// </item>
/// <item>
/// <description>
/// Heat is scaled by a thermal drive that flips sign with comfort — too hot pushes away from
/// warmth, too cold pulls toward it — and vanishes exactly at comfort:
/// <c>(−dev·6)·6·(temp[dir] − meanTemp)</c>.
/// </description>
/// </item>
/// </list>
/// <para>
/// A <see cref="SensePermutation"/> remaps which direction feeds each channel; the swap leaves the
/// means untouched (a permutation of the same four values) and only reorders the channels.
/// </para>
/// </remarks>
public sealed class Senses
{
    private readonly SimConfig _config;

    /// <summary>Creates a sensor model bound to the simulation constants.</summary>
    /// <param name="config">The simulation constants.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
    public Senses(SimConfig config)
        => _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// Builds the sensor vector for the worm's current body state and surroundings.
    /// </summary>
    /// <param name="body">The worm, read for position, hunger, and thermal deviation.</param>
    /// <param name="world">The world, sampled for odor and temperature ahead in each direction.</param>
    /// <param name="permutation">Which direction each channel reads (the sense-swap mapping).</param>
    /// <returns>
    /// A length-8 vector: indices 0–3 are the odor channels (one per direction, in permutation
    /// order), indices 4–7 are the heat channels. The network consumes it as a flat input.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> or <paramref name="world"/> is null.</exception>
    public double[] Sense(Body body, World world, SensePermutation permutation)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(world);

        // Sample odor and temperature a short distance ahead in each direction.
        Span<double> odor = stackalloc double[Directions.Count];
        Span<double> temp = stackalloc double[Directions.Count];
        double odorSum = 0.0;
        double tempSum = 0.0;
        for (int d = 0; d < Directions.Count; d++)
        {
            (int dx, int dy) = Directions.All[d];
            double sampleX = body.X + dx * _config.SensorSampleDistance;
            double sampleY = body.Y + dy * _config.SensorSampleDistance;
            odor[d] = world.OdorAt(sampleX, sampleY);
            temp[d] = world.TemperatureAt(sampleX, sampleY);
            odorSum += odor[d];
            tempSum += temp[d];
        }

        double meanOdor = odorSum / Directions.Count;
        double meanTemp = tempSum / Directions.Count;

        double gate = _config.OdorGateBase + _config.OdorGateHungerScale * body.Hunger;
        double thermalDrive = -body.ThermalDeviation * _config.ThermalDriveScale;

        var sensors = new double[_config.SensorCount];
        for (int i = 0; i < Directions.Count; i++)
        {
            int dir = permutation[i];
            sensors[i] = gate * _config.OdorSensorGain
                * (odor[dir] - meanOdor) / (meanOdor + _config.OdorNormalizationEpsilon);
            sensors[Directions.Count + i] = thermalDrive * _config.HeatSensorGain
                * (temp[dir] - meanTemp);
        }

        return sensors;
    }
}

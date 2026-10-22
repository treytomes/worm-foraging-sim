using System;
using System.Linq;
using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// Parity tests for <see cref="Senses"/> against the prototype's <c>s[]</c> construction:
/// the 8-channel layout, hunger gating, Weber normalization, thermal-drive sign, and the swap.
/// </summary>
public class SensesTests
{
    private static readonly SimConfig Config = new();
    private static readonly Senses TheSenses = new(Config);

    private static World NewWorld(SimConfig config, ulong seed = 1UL, double startX = 20, double startY = 20)
        => new(config, new Pcg32Random(seed), startX, startY);

    /// <summary>Temperatures sampled ahead in each direction — mirrors what <see cref="Senses"/> reads.</summary>
    private static double[] DirectionTemps(Body body, World world)
    {
        var t = new double[Directions.Count];
        for (int d = 0; d < Directions.Count; d++)
        {
            (int dx, int dy) = Directions.All[d];
            t[d] = world.TemperatureAt(body.X + dx * 1.2, body.Y + dy * 1.2);
        }

        return t;
    }

    private static Body WarmedBody(SimConfig config, World world)
    {
        // Oscillate on the lamp's thermal *slope* (~8 cells out), not its peak. The heat sensor is
        // differential, and a Gaussian is flat at its peak — sitting exactly on the lamp gives a
        // hot body but no directional heat signal. Eight cells out is both warm (temp ~0.8 >
        // comfort) and steep, so body temperature climbs above comfort AND the channels are
        // nonzero.
        var body = new Body(config, world.LampX - 8, world.LampY);
        for (int i = 0; i < 80; i++)
        {
            body.Move(1, 0, world);
            body.Move(-1, 0, world);
        }

        return body;
    }

    private static Body CooledBody(SimConfig config, World world)
    {
        // Oscillate in a cold corner far from the lamp to push body temperature below comfort.
        var body = new Body(config, 2, 2);
        for (int i = 0; i < 120; i++)
        {
            body.Move(1, 0, world);
            body.Move(-1, 0, world);
        }

        return body;
    }

    // ---- Layout ----

    [Fact]
    public void Sense_ReturnsEightChannels()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, 15, 25);

        Assert.Equal(8, TheSenses.Sense(body, world, SensePermutation.Identity).Length);
    }

    // ---- Differential: channels within a modality sum to zero ----

    [Fact]
    public void OdorChannels_SumToZero()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, 12, 28);

        double[] s = TheSenses.Sense(body, world, SensePermutation.Identity);

        Assert.Equal(0.0, s[0] + s[1] + s[2] + s[3], precision: 10);
    }

    [Fact]
    public void HeatChannels_SumToZero_AndAreNonTrivialWhenUncomfortable()
    {
        var world = NewWorld(Config);
        var body = WarmedBody(Config, world);

        double[] s = TheSenses.Sense(body, world, SensePermutation.Identity);

        Assert.Equal(0.0, s[4] + s[5] + s[6] + s[7], precision: 10);
        Assert.True(s.Skip(4).Any(v => Math.Abs(v) > 1e-6), "warmed body should produce nonzero heat channels");
    }

    // ---- Heat channels vanish exactly at comfort ----

    [Fact]
    public void AtComfort_HeatChannelsAreZero()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, 15, 25); // fresh: temperature == comfort, so thermal drive == 0

        double[] s = TheSenses.Sense(body, world, SensePermutation.Identity);

        Assert.Equal(0.0, body.ThermalDeviation);
        Assert.Equal(0.0, s[4]);
        Assert.Equal(0.0, s[5]);
        Assert.Equal(0.0, s[6]);
        Assert.Equal(0.0, s[7]);
    }

    // ---- Thermal-drive sign: seek heat when cold, avoid it when hot ----

    [Fact]
    public void ColdBody_HeatChannelRewardsTheWarmerDirection()
    {
        var world = NewWorld(Config);
        var body = CooledBody(Config, world);
        double[] t = DirectionTemps(body, world);
        int hottest = Array.IndexOf(t, t.Max());
        int coldest = Array.IndexOf(t, t.Min());

        double[] s = TheSenses.Sense(body, world, SensePermutation.Identity);

        Assert.True(body.ThermalDeviation < 0, "body should be below comfort");
        Assert.True(s[4 + hottest] > 0, "cold worm should be pulled toward warmth");
        Assert.True(s[4 + coldest] < 0, "cold worm should be pushed from cold");
    }

    [Fact]
    public void HotBody_HeatChannelRepelsTheWarmerDirection()
    {
        var world = NewWorld(Config);
        var body = WarmedBody(Config, world);
        double[] t = DirectionTemps(body, world);
        int hottest = Array.IndexOf(t, t.Max());
        int coldest = Array.IndexOf(t, t.Min());

        double[] s = TheSenses.Sense(body, world, SensePermutation.Identity);

        Assert.True(body.ThermalDeviation > 0, "body should be above comfort");
        Assert.True(s[4 + hottest] < 0, "hot worm should be pushed from warmth");
        Assert.True(s[4 + coldest] > 0, "hot worm should be pulled toward cooler");
    }

    // ---- Hunger gates odor sensitivity ----

    [Fact]
    public void Hunger_ScalesOdorChannelsByTheGate()
    {
        var world = NewWorld(Config);
        const double x = 12, y = 28;
        var hungry = new Body(Config with { InitialEnergy = 0.0 }, x, y); // hunger 1 -> gate 1.0
        var full = new Body(Config with { InitialEnergy = 1.0 }, x, y);   // hunger 0 -> gate 0.3

        double[] sh = TheSenses.Sense(hungry, world, SensePermutation.Identity);
        double[] sf = TheSenses.Sense(full, world, SensePermutation.Identity);

        double expectedRatio = (0.3 + 0.7 * 1.0) / (0.3 + 0.7 * 0.0); // gateHungry / gateFull
        bool sawNonZero = false;
        for (int i = 0; i < 4; i++)
        {
            if (Math.Abs(sf[i]) > 1e-9)
            {
                sawNonZero = true;
                Assert.Equal(expectedRatio, sh[i] / sf[i], precision: 9);
            }
        }

        Assert.True(sawNonZero, "expected a nonzero odor channel to compare");
    }

    // ---- The swap permutes channels without touching the means ----

    [Fact]
    public void Swap_ReordersChannelsByPermutation()
    {
        var world = NewWorld(Config);
        var body = WarmedBody(Config, world); // nonzero heat channels; odor gradient too

        double[] identity = TheSenses.Sense(body, world, SensePermutation.Identity);
        double[] swapped = TheSenses.Sense(body, world, SensePermutation.Swapped);

        var perm = SensePermutation.Swapped;
        for (int i = 0; i < 4; i++)
        {
            // Channel i of the swapped vector reads direction perm[i], i.e. identity's channel perm[i].
            Assert.Equal(identity[perm[i]], swapped[i], precision: 12);          // odor
            Assert.Equal(identity[4 + perm[i]], swapped[4 + i], precision: 12);  // heat
        }
    }

    // ---- Determinism and guards ----

    [Fact]
    public void Sense_IsPureForSameInputs()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, 15, 25);

        Assert.Equal(
            TheSenses.Sense(body, world, SensePermutation.Identity),
            TheSenses.Sense(body, world, SensePermutation.Identity));
    }

    [Fact]
    public void Sense_NullBody_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => TheSenses.Sense(null!, NewWorld(Config), SensePermutation.Identity));

    [Fact]
    public void Sense_NullWorld_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => TheSenses.Sense(new Body(Config, 20, 20), null!, SensePermutation.Identity));

    [Fact]
    public void Constructor_NullConfig_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Senses(null!));
}

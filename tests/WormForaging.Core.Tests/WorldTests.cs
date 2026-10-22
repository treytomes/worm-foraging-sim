using System;
using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// Parity tests for <see cref="World"/> against the prototype's <c>conc</c>, <c>temp</c>,
/// lamp motion, and <c>spawn</c> logic.
/// </summary>
public class WorldTests
{
    private static readonly SimConfig Config = new();

    private static World NewWorld(ulong seed = 1UL, double startX = 20, double startY = 20)
        => new(Config, new Pcg32Random(seed), startX, startY);

    // ---- Odor field: exp(-dist/12), peaking at 1 on the food ----

    [Fact]
    public void OdorAt_FoodLocation_IsOne()
    {
        var world = NewWorld();

        Assert.Equal(1.0, world.OdorAt(world.FoodX, world.FoodY), precision: 12);
    }

    [Fact]
    public void OdorAt_MatchesPrototypeFormula()
    {
        var world = NewWorld();
        const double sampleX = 10.0;
        const double sampleY = 15.0;

        double distance = Math.Sqrt(
            Math.Pow(sampleX - world.FoodX, 2) + Math.Pow(sampleY - world.FoodY, 2));
        double expected = Math.Exp(-distance / 12.0);

        Assert.Equal(expected, world.OdorAt(sampleX, sampleY), precision: 12);
    }

    [Fact]
    public void OdorAt_DecaysWithDistance()
    {
        var world = NewWorld();

        double near = world.OdorAt(world.FoodX + 1, world.FoodY);
        double far = world.OdorAt(world.FoodX + 10, world.FoodY);

        Assert.True(near > far);
        Assert.InRange(near, 0.0, 1.0);
        Assert.InRange(far, 0.0, 1.0);
    }

    // ---- Temperature field: 0.25 + 0.9*exp(-dsq/128) ----

    [Fact]
    public void TemperatureAt_LampCentre_IsBaselinePlusAmplitude()
    {
        var world = NewWorld();

        Assert.Equal(0.25 + 0.9, world.TemperatureAt(world.LampX, world.LampY), precision: 12);
    }

    [Fact]
    public void TemperatureAt_FarFromLamp_ApproachesBaseline()
    {
        var world = NewWorld();

        // A point far from the lamp: the Gaussian term is negligible.
        double temp = world.TemperatureAt(world.LampX + 100, world.LampY + 100);

        Assert.Equal(0.25, temp, precision: 6);
    }

    [Fact]
    public void TemperatureAt_MatchesPrototypeFormula()
    {
        var world = NewWorld();
        const double sampleX = 12.0;
        const double sampleY = 30.0;

        double squaredDistance = Math.Pow(sampleX - world.LampX, 2) + Math.Pow(sampleY - world.LampY, 2);
        double expected = 0.25 + 0.9 * Math.Exp(-squaredDistance / 128.0);

        Assert.Equal(expected, world.TemperatureAt(sampleX, sampleY), precision: 12);
    }

    // ---- Lamp motion: Lissajous orbit from t ----

    [Fact]
    public void Lamp_AtTimeZero_IsAtRightOfOrbit()
    {
        var world = NewWorld();

        // t=0: cos(0)=1 -> centre+radius=33; sin(0)=0 -> centre=20. Matches the prototype's init.
        Assert.Equal(0L, world.Time);
        Assert.Equal(33.0, world.LampX, precision: 12);
        Assert.Equal(20.0, world.LampY, precision: 12);
    }

    [Fact]
    public void Advance_IncrementsTimeAndMovesLamp()
    {
        var world = NewWorld();
        double x0 = world.LampX;
        double y0 = world.LampY;

        world.Advance();

        Assert.Equal(1L, world.Time);
        double expectedX = 20 + 13 * Math.Cos(0.006 * 1);
        double expectedY = 20 + 13 * Math.Sin(0.0041 * 1);
        Assert.Equal(expectedX, world.LampX, precision: 12);
        Assert.Equal(expectedY, world.LampY, precision: 12);
        Assert.NotEqual(y0, world.LampY); // y moved off 20
    }

    [Fact]
    public void Lamp_EachAxisStaysWithinRadiusOfCentre()
    {
        // The lamp traces a Lissajous figure, not a circle (x and y use different angular
        // speeds), so radial distance from the centre is NOT bounded by the radius — it can
        // reach radius*sqrt(2). What IS bounded is each axis independently: cos/sin keep x and
        // y within [centre - radius, centre + radius] = [7, 33].
        var world = NewWorld();

        for (int i = 0; i < 2000; i++)
        {
            world.Advance();
            Assert.InRange(world.LampX, 7.0 - 1e-9, 33.0 + 1e-9);
            Assert.InRange(world.LampY, 7.0 - 1e-9, 33.0 + 1e-9);
        }
    }

    // ---- Food placement: spawn box + rejection radius ----

    [Fact]
    public void InitialFood_IsWithinSpawnBox()
    {
        var world = NewWorld();

        Assert.InRange(world.FoodX, 4.0, 36.0);
        Assert.InRange(world.FoodY, 4.0, 36.0);
    }

    [Fact]
    public void InitialFood_KeepsMinimumDistanceFromStart()
    {
        const double startX = 20, startY = 20;
        var world = NewWorld(startX: startX, startY: startY);

        double distance = Math.Sqrt(
            Math.Pow(world.FoodX - startX, 2) + Math.Pow(world.FoodY - startY, 2));

        Assert.True(distance >= 12.0, $"food spawned {distance:F3} from start, under the 12-cell minimum");
    }

    [Fact]
    public void RespawnFood_KeepsMinimumDistanceFromReference_OverManyDraws()
    {
        var world = NewWorld();

        for (int i = 0; i < 500; i++)
        {
            double refX = 4 + (i % 33);
            double refY = 4 + ((i * 7) % 33);
            world.RespawnFood(refX, refY);

            double distance = Math.Sqrt(
                Math.Pow(world.FoodX - refX, 2) + Math.Pow(world.FoodY - refY, 2));
            Assert.True(distance >= 12.0);
            Assert.InRange(world.FoodX, 4.0, 36.0);
            Assert.InRange(world.FoodY, 4.0, 36.0);
        }
    }

    [Fact]
    public void IsFoodWithinReach_TrueOnFood_FalseFarAway()
    {
        var world = NewWorld();

        Assert.True(world.IsFoodWithinReach(world.FoodX, world.FoodY));
        Assert.True(world.IsFoodWithinReach(world.FoodX + 1.0, world.FoodY)); // 1.0 < 1.8
        Assert.False(world.IsFoodWithinReach(world.FoodX + 2.0, world.FoodY)); // 2.0 >= 1.8
    }

    // ---- Determinism: same seed, same world ----

    [Fact]
    public void SameSeed_ProducesIdenticalFoodPlacement()
    {
        var a = NewWorld(seed: 99UL);
        var b = NewWorld(seed: 99UL);

        Assert.Equal(a.FoodX, b.FoodX);
        Assert.Equal(a.FoodY, b.FoodY);

        for (int i = 0; i < 50; i++)
        {
            a.RespawnFood(10, 10);
            b.RespawnFood(10, 10);
            Assert.Equal(a.FoodX, b.FoodX);
            Assert.Equal(a.FoodY, b.FoodY);
        }
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
        => Assert.Throws<ArgumentNullException>(() => new World(null!, new Pcg32Random(1), 20, 20));

    [Fact]
    public void Constructor_NullRandom_Throws()
        => Assert.Throws<ArgumentNullException>(() => new World(new SimConfig(), null!, 20, 20));
}

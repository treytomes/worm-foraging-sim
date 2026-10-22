using System;
using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// Parity tests for <see cref="Body"/> against the prototype's movement clamp, thermal lag,
/// eating, and energy balance.
/// </summary>
public class BodyTests
{
    private static readonly SimConfig Config = new();

    private static World NewWorld(SimConfig config, ulong seed = 1UL, double startX = 20, double startY = 20)
        => new(config, new Pcg32Random(seed), startX, startY);

    // ---- Initial state ----

    [Fact]
    public void InitialState_MatchesPrototypeInit()
    {
        var body = new Body(Config, 20, 20);

        Assert.Equal(20, body.X);
        Assert.Equal(20, body.Y);
        Assert.Equal(0.5, body.Energy);
        Assert.Equal(0.0, body.Gut);
        Assert.Equal(0.55, body.Temperature); // COMF
        Assert.Equal(0, body.StepsSinceMeal);
        Assert.Equal(0, body.MealsEaten);
        Assert.Empty(body.MealIntervals);
    }

    [Fact]
    public void DerivedDrives_AtStart_AreNeutral()
    {
        var body = new Body(Config, 20, 20);

        Assert.Equal(0.5, body.Hunger);          // 1 - 0.5
        Assert.Equal(0.0, body.ThermalDeviation); // temp == COMF
        Assert.Equal(0.0, body.Discomfort);
    }

    // ---- Movement + thermal lag ----

    [Fact]
    public void Move_StepsOneCell_AndRelaxesTemperatureTowardLocal()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, 20, 20);

        double localTemp = world.TemperatureAt(21, 20); // temperature at the NEW cell
        double expectedTemp = 0.55 + 0.08 * (localTemp - 0.55);

        body.Move(1, 0, world);

        Assert.Equal(21, body.X);
        Assert.Equal(20, body.Y);
        Assert.Equal(expectedTemp, body.Temperature, precision: 12);
    }

    [Fact]
    public void Move_ClampsToArenaInteriorLowerBound()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, 1, 1);

        body.Move(-1, -1, world); // would leave the arena; clamps to [1,1]

        Assert.Equal(1, body.X);
        Assert.Equal(1, body.Y);
    }

    [Fact]
    public void Move_ClampsToArenaInteriorUpperBound()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, 39, 39); // ArenaSize - 1

        body.Move(1, 1, world);

        Assert.Equal(39, body.X);
        Assert.Equal(39, body.Y);
    }

    [Fact]
    public void Move_NullWorld_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Body(Config, 20, 20).Move(1, 0, null!));

    // ---- Metabolism: no food in reach ----

    [Fact]
    public void Metabolize_NoFood_DrainsBaselineEnergyAndCountsStep()
    {
        // World places food >=12 cells from (20,20); the body never moves, so it never eats.
        var world = NewWorld(Config);
        var body = new Body(Config, 20, 20);

        double absorbed = body.Metabolize(world);

        Assert.Equal(0.0, absorbed);
        Assert.Equal(0.0, body.Gut);
        Assert.Equal(1, body.StepsSinceMeal);
        Assert.Equal(0, body.MealsEaten);
        Assert.Equal(0.5 - 0.002, body.Energy, precision: 12); // baseline cost only (no discomfort)
    }

    [Fact]
    public void Metabolize_WithDiscomfort_DrainsExtraEnergy()
    {
        var world = NewWorld(Config);
        world.RespawnFood(world.LampX, world.LampY); // keep food clear of the lamp area

        // Warm the body up by oscillating next to the (stationary) lamp.
        var body = new Body(Config, world.LampX, world.LampY);
        for (int i = 0; i < 100; i++)
        {
            body.Move(1, 0, world);
            body.Move(-1, 0, world);
        }

        Assert.True(body.Discomfort > 0.1, $"expected meaningful discomfort, got {body.Discomfort:F3}");

        double before = body.Energy;
        double discomfort = body.Discomfort;
        double absorbed = body.Metabolize(world);

        Assert.Equal(0.0, absorbed); // gut empty
        double expected = before - 0.002 - 0.006 * discomfort;
        Assert.Equal(expected, body.Energy, precision: 12);
    }

    // ---- Metabolism: eating ----

    [Fact]
    public void Metabolize_OnFood_EatsAbsorbsRecordsAndRespawns()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, world.FoodX, world.FoodY); // sitting on the food
        double oldFoodX = world.FoodX;
        double oldFoodY = world.FoodY;

        double absorbed = body.Metabolize(world);

        // since: 0 -> 1 (incremented), meal recorded as interval 1, then reset to 0
        Assert.Equal(1, body.MealsEaten);
        Assert.Equal(1L, body.MealIntervals[0]);
        Assert.Equal(0, body.StepsSinceMeal);

        // gut: +0.6 then -0.01 absorbed = 0.59; absorbed = 0.01
        Assert.Equal(0.01, absorbed, precision: 12);
        Assert.Equal(0.59, body.Gut, precision: 12);

        // energy: 0.5 + 0.01 - 0.002 - 0 = 0.508
        Assert.Equal(0.508, body.Energy, precision: 12);

        // food respawned elsewhere
        Assert.True(oldFoodX != world.FoodX || oldFoodY != world.FoodY);
    }

    [Fact]
    public void Metabolize_AbsorbsGutSlowlyOverManySteps()
    {
        var world = NewWorld(Config);
        var body = new Body(Config, world.FoodX, world.FoodY);

        body.Metabolize(world); // eat: gut 0.6 -> 0.59
        double gutAfterEat = body.Gut;

        // Food has respawned >=12 away; subsequent steps only absorb, 0.01 per step.
        body.Metabolize(world);

        Assert.Equal(gutAfterEat - 0.01, body.Gut, precision: 12);
    }

    // ---- Energy clamps (exercised via config overrides) ----

    [Fact]
    public void Metabolize_EnergyClampsAtZero()
    {
        var config = Config with { InitialEnergy = 0.001 };
        var world = NewWorld(config);
        var body = new Body(config, 20, 20);

        body.Metabolize(world); // 0.001 - 0.002 = -0.001 -> clamped to 0

        Assert.Equal(0.0, body.Energy);
    }

    [Fact]
    public void Metabolize_EnergyClampsAtOne()
    {
        var config = Config with { InitialEnergy = 0.999 };
        var world = NewWorld(config);
        var body = new Body(config, world.FoodX, world.FoodY); // on food

        body.Metabolize(world); // 0.999 + 0.01 - 0.002 = 1.007 -> clamped to 1

        Assert.Equal(1.0, body.Energy);
    }

    [Fact]
    public void Metabolize_NullWorld_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Body(Config, 20, 20).Metabolize(null!));

    [Fact]
    public void Constructor_NullConfig_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Body(null!, 20, 20));

    // ---- Determinism ----

    [Fact]
    public void SameSeedAndMoves_ProduceIdenticalBodyState()
    {
        var worldA = NewWorld(Config, seed: 7UL);
        var worldB = NewWorld(Config, seed: 7UL);
        var bodyA = new Body(Config, 20, 20);
        var bodyB = new Body(Config, 20, 20);

        int[] dx = { 1, 1, 0, -1, 0, 1 };
        int[] dy = { 0, -1, 1, 0, 1, 1 };
        for (int i = 0; i < dx.Length; i++)
        {
            bodyA.Move(dx[i], dy[i], worldA);
            bodyA.Metabolize(worldA);
            bodyB.Move(dx[i], dy[i], worldB);
            bodyB.Metabolize(worldB);
        }

        Assert.Equal(bodyA.X, bodyB.X);
        Assert.Equal(bodyA.Y, bodyB.Y);
        Assert.Equal(bodyA.Energy, bodyB.Energy);
        Assert.Equal(bodyA.Temperature, bodyB.Temperature);
        Assert.Equal(bodyA.Gut, bodyB.Gut);
    }
}

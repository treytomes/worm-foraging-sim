using System;
using System.Linq;
using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// End-to-end tests for the <see cref="Simulation"/> step loop: correct wiring of the components,
/// bit-identical determinism from a seed, state invariants, and snapshot behaviour. These are
/// plumbing and reproducibility checks, not claims about whether the rule learns well — that is
/// measured later by the harness.
/// </summary>
public class SimulationTests
{
    private static readonly SimConfig Config = new();

    private static Simulation NewSim(ulong seed = 1UL) => new(Config, new Pcg32Random(seed));

    // ---- Initial state ----

    [Fact]
    public void InitialSnapshot_MatchesPrototypeStart()
    {
        var snap = NewSim().Snapshot();

        Assert.Equal(0, snap.Step);
        Assert.Equal(20, snap.WormX); // arena centre
        Assert.Equal(20, snap.WormY);
        Assert.Equal(0.5, snap.Energy);
        Assert.Equal(0.0, snap.Gut);
        Assert.Equal(0.55, snap.BodyTemperature);
        Assert.Equal(0, snap.MealsEaten);
        Assert.Equal(33.0, snap.LampX, precision: 12); // lamp at t=0
        Assert.Equal(20.0, snap.LampY, precision: 12);
        Assert.Equal(16, snap.Activations.Count);
        Assert.Equal(64, snap.ReadoutWeights.Count);

        double foodDistance = Math.Sqrt(Math.Pow(snap.FoodX - 20, 2) + Math.Pow(snap.FoodY - 20, 2));
        Assert.True(foodDistance >= 12.0);
    }

    [Fact]
    public void DefaultControls_AreSet()
    {
        var sim = NewSim();

        Assert.Equal(LearningMode.Full, sim.Mode);
        Assert.Equal(1.0, sim.ExploreTemperature);
        Assert.True(sim.HomeostasisEnabled);
        Assert.True(sim.Permutation.IsIdentity);
    }

    // ---- Stepping ----

    [Fact]
    public void Step_AdvancesTimeLampAndWorm()
    {
        var sim = NewSim();
        var before = sim.Snapshot();

        sim.Step();
        var after = sim.Snapshot();

        Assert.Equal(1, after.Step);
        // Lamp moved to t=1.
        Assert.Equal(20 + 13 * Math.Cos(0.006 * 1), after.LampX, precision: 12);
        // Worm moved exactly one cell along one axis.
        double moved = Math.Abs(after.WormX - before.WormX) + Math.Abs(after.WormY - before.WormY);
        Assert.Equal(1.0, moved, precision: 12);
    }

    // ---- Determinism: the bit-identical-run guarantee ----

    [Fact]
    public void SameSeed_ProducesBitIdenticalRuns()
    {
        var a = NewSim(seed: 20261022UL);
        var b = NewSim(seed: 20261022UL);

        for (int i = 0; i < 2000; i++)
        {
            a.Step();
            b.Step();
        }

        var sa = a.Snapshot();
        var sb = b.Snapshot();

        Assert.Equal(sa.WormX, sb.WormX);
        Assert.Equal(sa.WormY, sb.WormY);
        Assert.Equal(sa.Energy, sb.Energy);
        Assert.Equal(sa.BodyTemperature, sb.BodyTemperature);
        Assert.Equal(sa.Gut, sb.Gut);
        Assert.Equal(sa.FoodX, sb.FoodX);
        Assert.Equal(sa.MealsEaten, sb.MealsEaten);
        Assert.Equal(sa.Activations, sb.Activations);
        Assert.Equal(sa.ReadoutWeights, sb.ReadoutWeights);
    }

    [Fact]
    public void DifferentSeeds_Diverge()
    {
        var a = NewSim(seed: 1UL);
        var b = NewSim(seed: 2UL);
        for (int i = 0; i < 500; i++)
        {
            a.Step();
            b.Step();
        }

        Assert.False(a.Snapshot().ReadoutWeights.SequenceEqual(b.Snapshot().ReadoutWeights));
    }

    // ---- Wiring: learning actually drives the readout (and frozen doesn't) ----

    [Fact]
    public void FullMode_ChangesReadoutWeightsOverTime()
    {
        var sim = NewSim(seed: 5UL);
        sim.Mode = LearningMode.Full;
        double[] initial = sim.Snapshot().ReadoutWeights.ToArray();

        for (int i = 0; i < 1000; i++)
        {
            sim.Step();
        }

        Assert.False(initial.SequenceEqual(sim.Snapshot().ReadoutWeights));
    }

    [Fact]
    public void FrozenMode_NeverChangesReadoutWeights()
    {
        var sim = NewSim(seed: 5UL);
        sim.Mode = LearningMode.Frozen;
        double[] initial = sim.Snapshot().ReadoutWeights.ToArray();

        for (int i = 0; i < 1000; i++)
        {
            sim.Step();
        }

        // Frozen means exactly fixed: not even weight decay.
        Assert.Equal(initial, sim.Snapshot().ReadoutWeights);
    }

    // ---- Invariants over a long run ----

    [Fact]
    public void State_StaysWithinBounds_OverALongRun()
    {
        var sim = NewSim(seed: 7UL);

        for (int i = 0; i < 5000; i++)
        {
            sim.Step();
            var s = sim.Snapshot();
            Assert.InRange(s.Energy, 0.0, 1.0);
            Assert.InRange(s.WormX, 1.0, 39.0);
            Assert.InRange(s.WormY, 1.0, 39.0);
            Assert.True(s.Gut >= 0.0);
            Assert.All(s.Activations, a => Assert.InRange(a, -1.0, 1.0));
            Assert.All(s.ReadoutWeights, w => Assert.InRange(w, -4.0, 4.0));
        }
    }

    [Fact]
    public void Worm_EatsAtLeastOnce_OverALongRun()
    {
        // A plumbing check that the eat/respawn path fires end to end, not a learning claim.
        var sim = NewSim(seed: 3UL);
        for (int i = 0; i < 20000; i++)
        {
            sim.Step();
        }

        var snap = sim.Snapshot();
        Assert.True(snap.MealsEaten > 0, "worm should reach food at least once in 20k steps");
        Assert.Equal(snap.MealsEaten, snap.MealIntervals.Count);
    }

    // ---- Controls ----

    [Fact]
    public void SwapSenses_TogglesPermutation()
    {
        var sim = NewSim();
        Assert.True(sim.Permutation.IsIdentity);

        sim.SwapSenses();
        Assert.Equal(SensePermutation.Swapped, sim.Permutation);

        sim.SwapSenses();
        Assert.True(sim.Permutation.IsIdentity);
    }

    [Fact]
    public void Mode_DelegatesToLearningRule()
    {
        var sim = NewSim();
        sim.Mode = LearningMode.Hebbian;
        Assert.Equal(LearningMode.Hebbian, sim.Mode);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    public void ExploreTemperature_NonPositive_Throws(double value)
        => Assert.Throws<ArgumentOutOfRangeException>(() => NewSim().ExploreTemperature = value);

    // ---- Snapshot immutability ----

    [Fact]
    public void Snapshot_IsFrozen_AfterFurtherSteps()
    {
        var sim = NewSim(seed: 9UL);
        sim.Step();
        var snap = sim.Snapshot();
        double[] activationsAtSnapshot = snap.Activations.ToArray();

        for (int i = 0; i < 10; i++)
        {
            sim.Step();
        }

        // The earlier snapshot's arrays are copies and must be untouched by later steps.
        Assert.Equal(activationsAtSnapshot, snap.Activations);
    }

    // ---- Guards ----

    [Fact]
    public void Constructor_NullConfig_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Simulation(null!, new Pcg32Random(1)));

    [Fact]
    public void Constructor_NullRandom_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Simulation(Config, null!));
}

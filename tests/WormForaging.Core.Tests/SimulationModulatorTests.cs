using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// Tests that the broadcast modulator and its two reward channels are exposed on the snapshot, and
/// that the broadcast value matches each mode's contract (reward in Full, fixed in Hebbian, zero in
/// Frozen). These are display-only read-outs and must not perturb the run.
/// </summary>
public class SimulationModulatorTests
{
    private static readonly SimConfig Config = new();

    private static Simulation NewSim(LearningMode mode, ulong seed = 3UL) =>
        new(Config, new Pcg32Random(seed)) { Mode = mode };

    [Fact]
    public void Full_Modulator_IsTheSumOfTheTwoChannels()
    {
        var sim = NewSim(LearningMode.Full);
        for (int i = 0; i < 50; i++)
        {
            sim.Step();
        }

        var snap = sim.Snapshot();
        Assert.Equal(snap.FoodDrive + snap.ThermalDrive, snap.Modulator, precision: 12);
    }

    [Fact]
    public void Hebbian_Modulator_IsTheFixedConstant()
    {
        var sim = NewSim(LearningMode.Hebbian);
        sim.Step();

        Assert.Equal(Config.HebbianModulator, sim.Snapshot().Modulator);
    }

    [Fact]
    public void Frozen_Modulator_IsZero()
    {
        var sim = NewSim(LearningMode.Frozen);
        for (int i = 0; i < 10; i++)
        {
            sim.Step();
        }

        Assert.Equal(0.0, sim.Snapshot().Modulator);
    }

    [Fact]
    public void Channels_AreComputedEvenWhenTheModeIgnoresThem()
    {
        // Frozen ignores the reward, but the channels are still computed from the senses, so they can
        // be displayed. Over 100 steps at least one channel should move off zero.
        var sim = NewSim(LearningMode.Frozen);
        bool sawNonZero = false;
        for (int i = 0; i < 100; i++)
        {
            sim.Step();
            var snap = sim.Snapshot();
            if (snap.FoodDrive != 0.0 || snap.ThermalDrive != 0.0)
            {
                sawNonZero = true;
                break;
            }
        }

        Assert.True(sawNonZero);
    }
}

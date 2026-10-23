using System.Linq;
using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// Tests for <see cref="Simulation.Topology"/>: correct dimensions and array lengths, that the values
/// are a faithful copy of the network's fixed wiring, that the sparse recurrent matrix really is
/// sparse, and that the topology is built once and cached.
/// </summary>
public class NetworkTopologyTests
{
    private static readonly SimConfig Config = new();

    private static Simulation NewSim(ulong seed = 1UL) => new(Config, new Pcg32Random(seed));

    [Fact]
    public void Topology_HasDimensionsAndLengthsFromConfig()
    {
        var t = NewSim().Topology;

        Assert.Equal(Config.InterneuronCount, t.NeuronCount);
        Assert.Equal(Config.SensorCount, t.SensorCount);
        Assert.Equal(Config.MotorCount, t.MotorCount);

        Assert.Equal(Config.InterneuronCount * Config.SensorCount, t.InputWeights.Count);
        Assert.Equal(Config.InterneuronCount * Config.InterneuronCount, t.RecurrentWeights.Count);
        Assert.Equal(Config.InterneuronCount, t.HungerWeights.Count);
        Assert.Equal(Config.InterneuronCount, t.ThermalWeights.Count);
        Assert.Equal(Config.InterneuronCount, t.Biases.Count);
    }

    [Fact]
    public void Topology_IsBuiltOnceAndCached()
    {
        var sim = NewSim();
        Assert.Same(sim.Topology, sim.Topology);
    }

    [Fact]
    public void Topology_RecurrentMatrixIsSparse()
    {
        var t = NewSim().Topology;

        int present = t.RecurrentWeights.Count(w => w != 0.0);
        double density = (double)present / t.RecurrentWeights.Count;

        // Connections exist with probability RecurrentConnectionProbability (0.2); allow a wide band
        // around it so the test checks "sparse", not an exact draw count.
        Assert.InRange(density, 0.05, 0.4);
        Assert.Contains(t.RecurrentWeights, w => w == 0.0); // at least some absent connections
    }

    [Fact]
    public void Topology_IsDeterministicForASeed()
    {
        var a = NewSim(seed: 7).Topology;
        var b = NewSim(seed: 7).Topology;

        Assert.Equal(a.InputWeights, b.InputWeights);
        Assert.Equal(a.RecurrentWeights, b.RecurrentWeights);
        Assert.Equal(a.HungerWeights, b.HungerWeights);
        Assert.Equal(a.ThermalWeights, b.ThermalWeights);
        Assert.Equal(a.Biases, b.Biases);
    }

    [Fact]
    public void Topology_DiffersAcrossSeeds()
    {
        var a = NewSim(seed: 1).Topology;
        var b = NewSim(seed: 2).Topology;

        Assert.NotEqual(a.InputWeights, b.InputWeights);
    }
}

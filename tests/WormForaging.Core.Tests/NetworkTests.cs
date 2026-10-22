using System;
using System.Linq;
using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// Parity tests for <see cref="Network"/> against the prototype's interneuron block: the forward
/// formula, sparse recurrent wiring, recurrence, and homeostatic gain control.
/// </summary>
public class NetworkTests
{
    private static readonly SimConfig Config = new();

    private static double[] Sensors(double fill = 0.0)
    {
        var s = new double[Config.SensorCount];
        Array.Fill(s, fill);
        return s;
    }

    // ---- Structure ----

    [Fact]
    public void Construction_SetsExpectedSizes()
    {
        var net = new Network(Config, new Pcg32Random(1));

        Assert.Equal(16, net.NeuronCount);
        Assert.Equal(8, net.SensorCount);
        Assert.Equal(16 * 8, net.InputWeights.Count);
        Assert.Equal(16 * 16, net.RecurrentWeights.Count);
        Assert.Equal(16, net.HungerWeights.Count);
        Assert.Equal(16, net.ThermalWeights.Count);
        Assert.Equal(16, net.Biases.Count);
        Assert.All(net.Gains, g => Assert.Equal(1.0, g));               // InitialGain
        Assert.All(net.ActivityTrace, a => Assert.Equal(0.4, a));       // InitialActivityTrace
        Assert.All(net.PreviousActivations, x => Assert.Equal(0.0, x)); // zero recurrent state
    }

    // ---- Forward formula parity (with zero recurrent state on first activation) ----

    [Fact]
    public void Activate_MatchesPrototypeForwardFormula()
    {
        var net = new Network(Config, new Pcg32Random(5));
        double[] s = { 0.1, -0.2, 0.3, -0.4, 0.5, -0.6, 0.7, -0.8 };
        const double hunger = 0.3;
        const double dev = 0.2;

        double[] xn = net.Activate(s, hunger, dev);

        Assert.Equal(xn, net.Activations); // Activations exposes the latest Activate result

        for (int i = 0; i < net.NeuronCount; i++)
        {
            double u = net.HungerWeights[i] * hunger * Config.HungerInputScale
                + net.ThermalWeights[i] * dev * Config.ThermalInputScale
                + net.Biases[i];
            for (int j = 0; j < net.SensorCount; j++)
            {
                u += net.InputWeights[i * net.SensorCount + j] * s[j];
            }
            // recurrent term is zero: PreviousActivations are all zero before any commit.

            Assert.Equal(Math.Tanh(net.Gains[i] * u), xn[i], precision: 12);
        }
    }

    [Fact]
    public void Activate_OutputsAreBoundedByTanh()
    {
        var net = new Network(Config, new Pcg32Random(9));

        double[] xn = net.Activate(Sensors(5.0), hunger: 1.0, thermalDeviation: 2.0);

        Assert.All(xn, v => Assert.InRange(v, -1.0, 1.0));
    }

    // ---- Recurrence ----

    [Fact]
    public void Recurrence_PastActivationsChangeTheNextOutput()
    {
        var net = new Network(Config, new Pcg32Random(3));
        double[] s = Sensors();

        double[] first = net.Activate(s, 0.0, 0.0);
        net.AdaptAndCommit(first, homeostasisEnabled: false);

        // Same input, but now the recurrent state is nonzero, so the output must differ.
        double[] second = net.Activate(s, 0.0, 0.0);

        Assert.Equal(first, net.PreviousActivations); // committed as recurrent state
        Assert.False(first.SequenceEqual(second));
    }

    [Fact]
    public void AdaptAndCommit_StoresACopy_NotAReference()
    {
        var net = new Network(Config, new Pcg32Random(3));
        double[] xn = net.Activate(Sensors(), 0.0, 0.0);
        net.AdaptAndCommit(xn, homeostasisEnabled: false);

        double original = net.PreviousActivations[0];
        xn[0] = 42.0; // mutating the caller's array must not disturb committed state

        Assert.Equal(original, net.PreviousActivations[0]);
    }

    // ---- Homeostatic gain ----

    [Fact]
    public void Homeostasis_RaisesGainWhenActivityIsBelowTarget()
    {
        var net = new Network(Config, new Pcg32Random(7));

        // Zero sensors/drives give small activations (~tanh(g·b), |b|~0.1), well under the 0.4
        // target, so the gain controller should push gains up over time.
        double gainBefore = net.Gains.Average();
        for (int step = 0; step < 300; step++)
        {
            double[] xn = net.Activate(Sensors(), 0.0, 0.0);
            net.AdaptAndCommit(xn, homeostasisEnabled: true);
        }
        double gainAfter = net.Gains.Average();

        Assert.True(gainAfter > gainBefore, $"gain should rise under low activity ({gainBefore:F3} -> {gainAfter:F3})");
        Assert.All(net.Gains, g => Assert.InRange(g, 0.3, 3.0)); // stays clamped
    }

    [Fact]
    public void Homeostasis_Disabled_LeavesGainsUnchangedButStillTracksActivity()
    {
        var net = new Network(Config, new Pcg32Random(7));
        double[] gainsBefore = net.Gains.ToArray();
        double[] traceBefore = net.ActivityTrace.ToArray();

        for (int step = 0; step < 50; step++)
        {
            double[] xn = net.Activate(Sensors(), 0.0, 0.0);
            net.AdaptAndCommit(xn, homeostasisEnabled: false);
        }

        Assert.Equal(gainsBefore, net.Gains); // gains frozen
        Assert.False(traceBefore.SequenceEqual(net.ActivityTrace)); // but ax still moves
    }

    // ---- Initialization statistics (aggregated across seeds to keep bounds tight) ----

    [Fact]
    public void InputWeights_HaveApproximatelyConfiguredScale()
    {
        var values = Enumerable.Range(0, 60)
            .SelectMany(seed => new Network(Config, new Pcg32Random((ulong)seed)).InputWeights)
            .ToArray();

        double mean = values.Average();
        double std = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Average());

        Assert.InRange(mean, -0.05, 0.05);  // ~0
        Assert.InRange(std, 0.66, 0.74);    // ~0.7
    }

    [Fact]
    public void RecurrentWeights_AreAboutTwentyPercentDense()
    {
        var all = Enumerable.Range(0, 60)
            .SelectMany(seed => new Network(Config, new Pcg32Random((ulong)seed)).RecurrentWeights)
            .ToArray();

        double nonzeroFraction = all.Count(v => v != 0.0) / (double)all.Length;
        double[] present = all.Where(v => v != 0.0).ToArray();
        double mean = present.Average();
        double std = Math.Sqrt(present.Select(v => (v - mean) * (v - mean)).Average());

        Assert.InRange(nonzeroFraction, 0.18, 0.22); // ~0.2 connection probability
        Assert.InRange(std, 0.32, 0.38);             // present weights ~0.35 scale
    }

    // ---- Determinism ----

    [Fact]
    public void SameSeed_ProducesIdenticalWiringAndActivations()
    {
        var a = new Network(Config, new Pcg32Random(123));
        var b = new Network(Config, new Pcg32Random(123));

        Assert.Equal(a.InputWeights, b.InputWeights);
        Assert.Equal(a.RecurrentWeights, b.RecurrentWeights);

        double[] s = { 0.2, 0.1, 0.0, -0.1, -0.2, 0.3, -0.3, 0.05 };
        Assert.Equal(a.Activate(s, 0.4, -0.1), b.Activate(s, 0.4, -0.1));
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentWiring()
    {
        var a = new Network(Config, new Pcg32Random(1));
        var b = new Network(Config, new Pcg32Random(2));

        Assert.False(a.InputWeights.SequenceEqual(b.InputWeights));
    }

    // ---- Guards ----

    [Fact]
    public void Activate_WrongSensorLength_Throws()
        => Assert.Throws<ArgumentException>(
            () => new Network(Config, new Pcg32Random(1)).Activate(new double[7], 0, 0));

    [Fact]
    public void Activate_NullSensors_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new Network(Config, new Pcg32Random(1)).Activate(null!, 0, 0));

    [Fact]
    public void AdaptAndCommit_WrongLength_Throws()
        => Assert.Throws<ArgumentException>(
            () => new Network(Config, new Pcg32Random(1)).AdaptAndCommit(new double[15], false));

    [Fact]
    public void AdaptAndCommit_NullActivations_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new Network(Config, new Pcg32Random(1)).AdaptAndCommit(null!, false));

    [Fact]
    public void Constructor_NullConfig_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Network(null!, new Pcg32Random(1)));

    [Fact]
    public void Constructor_NullRandom_Throws()
        => Assert.Throws<ArgumentNullException>(() => new Network(Config, null!));
}

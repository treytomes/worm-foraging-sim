using System;
using System.Linq;
using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// Parity tests for <see cref="MotorLayer"/> against the prototype's readout, softmax, and
/// inverse-CDF sampling.
/// </summary>
public class MotorLayerTests
{
    private static readonly SimConfig Config = new();

    private static double[] Activations(ulong seed = 42UL)
    {
        // Arbitrary but deterministic activations in (-1, 1).
        var rng = new Pcg32Random(seed);
        var x = new double[Config.InterneuronCount];
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = Math.Tanh(rng.NextGaussian());
        }

        return x;
    }

    // ---- Structure / init ----

    [Fact]
    public void Construction_SetsSizes()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(1));

        Assert.Equal(4, motor.MotorCount);
        Assert.Equal(16, motor.NeuronCount);
        Assert.Equal(4 * 16, motor.ReadoutWeightCount);
        Assert.Equal(4 * 16, motor.ReadoutWeights.Count);
    }

    [Fact]
    public void ReadoutWeights_HaveApproximatelyConfiguredScale()
    {
        var values = Enumerable.Range(0, 60)
            .SelectMany(seed => new MotorLayer(Config, new Pcg32Random((ulong)seed)).ReadoutWeights)
            .ToArray();

        double mean = values.Average();
        double std = Math.Sqrt(values.Select(v => (v - mean) * (v - mean)).Average());

        Assert.InRange(mean, -0.02, 0.02); // ~0
        Assert.InRange(std, 0.09, 0.11);   // ~0.1
    }

    // ---- Softmax policy ----

    [Fact]
    public void Policy_MatchesPrototypeSoftmaxFormula()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(11));
        double[] x = Activations();
        const double temperature = 1.3;

        double[] p = motor.Policy(x, temperature);

        // Recompute logits from the exposed readout weights.
        var logits = new double[motor.MotorCount];
        for (int k = 0; k < motor.MotorCount; k++)
        {
            double v = 0.0;
            for (int j = 0; j < motor.NeuronCount; j++)
            {
                v += motor.ReadoutWeights[k * motor.NeuronCount + j] * x[j];
            }
            logits[k] = v / temperature;
        }
        double max = logits.Max();
        double[] expUnnorm = logits.Select(l => Math.Exp(l - max)).ToArray();
        double z = expUnnorm.Sum();
        double[] expected = expUnnorm.Select(e => e / z).ToArray();

        for (int k = 0; k < motor.MotorCount; k++)
        {
            Assert.Equal(expected[k], p[k], precision: 12);
        }
    }

    [Fact]
    public void Policy_ProbabilitiesAreValidDistribution()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(2));
        double[] p = motor.Policy(Activations(), temperature: 1.0);

        Assert.Equal(4, p.Length);
        Assert.All(p, v => Assert.InRange(v, 0.0, 1.0));
        Assert.Equal(1.0, p.Sum(), precision: 12);
    }

    [Fact]
    public void Policy_LowerTemperatureSharpensTheDistribution()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(2));
        double[] x = Activations();

        double peakCold = motor.Policy(x, temperature: 0.3).Max();
        double peakHot = motor.Policy(x, temperature: 3.0).Max();

        Assert.True(peakCold > peakHot, $"low T should concentrate probability ({peakHot:F3} -> {peakCold:F3})");
    }

    // ---- Sampling: inverse-CDF correctness with scripted draws ----

    [Fact]
    public void Select_DrawInEachBucket_PicksThatBucket()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(2));
        double[] x = Activations();
        double[] p = motor.Policy(x, temperature: 1.0);

        // Cumulative boundaries; a draw just inside each bucket must select it.
        double c0 = p[0];
        double c1 = c0 + p[1];
        double c2 = c1 + p[2];

        Assert.Equal(0, motor.Select(x, 1.0, new SequenceRandom(c0 * 0.5)).Choice);
        Assert.Equal(1, motor.Select(x, 1.0, new SequenceRandom((c0 + c1) * 0.5)).Choice);
        Assert.Equal(2, motor.Select(x, 1.0, new SequenceRandom((c1 + c2) * 0.5)).Choice);
        Assert.Equal(3, motor.Select(x, 1.0, new SequenceRandom((c2 + 1.0) * 0.5)).Choice);
    }

    [Fact]
    public void Select_DrawPastTheEnd_FallsBackToLastDirection()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(2));
        double[] x = Activations();

        // A draw of (almost) 1 lands in or past the last bucket -> last direction.
        var decision = motor.Select(x, 1.0, new SequenceRandom(0.9999999999));

        Assert.Equal(motor.MotorCount - 1, decision.Choice);
    }

    [Fact]
    public void Select_EmpiricalFrequenciesMatchThePolicy()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(2));
        double[] x = Activations();
        double[] p = motor.Policy(x, temperature: 1.0);

        var counts = new int[motor.MotorCount];
        var rng = new Pcg32Random(1234);
        const int trials = 200_000;
        for (int t = 0; t < trials; t++)
        {
            counts[motor.Select(x, 1.0, rng).Choice]++;
        }

        for (int k = 0; k < motor.MotorCount; k++)
        {
            double empirical = counts[k] / (double)trials;
            Assert.InRange(empirical, p[k] - 0.01, p[k] + 0.01);
        }
    }

    [Fact]
    public void Select_ReturnsTheProbabilitiesUsed()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(2));
        double[] x = Activations();

        var decision = motor.Select(x, 1.0, new SequenceRandom(0.5));

        Assert.Equal(motor.Policy(x, 1.0), decision.Probabilities);
        Assert.InRange(decision.Choice, 0, motor.MotorCount - 1);
    }

    // ---- Readout weight access (for the learning rule) ----

    [Fact]
    public void ReadoutWeight_GetSet_RoundTripsAndAffectsPolicy()
    {
        var motor = new MotorLayer(Config, new Pcg32Random(2));
        double[] x = Enumerable.Repeat(0.0, Config.InterneuronCount).ToArray();
        x[0] = 1.0; // only neuron 0 active, so only weights to neuron 0 matter

        double[] before = motor.Policy(x, 1.0);
        motor.SetReadoutWeight(0, motor.GetReadoutWeight(0) + 5.0); // boost direction 0's logit
        double[] after = motor.Policy(x, 1.0);

        Assert.True(after[0] > before[0], "raising a readout weight should raise that action's probability");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    public void ReadoutWeight_OutOfRange_Throws(int index)
    {
        var motor = new MotorLayer(Config, new Pcg32Random(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => motor.GetReadoutWeight(index));
        Assert.Throws<ArgumentOutOfRangeException>(() => motor.SetReadoutWeight(index, 0.0));
    }

    // ---- Determinism ----

    [Fact]
    public void SameSeed_ProducesIdenticalReadoutAndDecisions()
    {
        var a = new MotorLayer(Config, new Pcg32Random(77));
        var b = new MotorLayer(Config, new Pcg32Random(77));
        double[] x = Activations();

        Assert.Equal(a.ReadoutWeights, b.ReadoutWeights);
        Assert.Equal(
            a.Select(x, 1.0, new Pcg32Random(5)).Choice,
            b.Select(x, 1.0, new Pcg32Random(5)).Choice);
    }

    // ---- Guards ----

    [Fact]
    public void Policy_WrongActivationLength_Throws()
        => Assert.Throws<ArgumentException>(
            () => new MotorLayer(Config, new Pcg32Random(1)).Policy(new double[15], 1.0));

    [Fact]
    public void Policy_NullActivations_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new MotorLayer(Config, new Pcg32Random(1)).Policy(null!, 1.0));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Policy_NonPositiveTemperature_Throws(double temperature)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new MotorLayer(Config, new Pcg32Random(1)).Policy(Activations(), temperature));

    [Fact]
    public void Select_NullRandom_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new MotorLayer(Config, new Pcg32Random(1)).Select(Activations(), 1.0, null!));

    [Fact]
    public void Constructor_NullConfig_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MotorLayer(null!, new Pcg32Random(1)));

    [Fact]
    public void Constructor_NullRandom_Throws()
        => Assert.Throws<ArgumentNullException>(() => new MotorLayer(Config, null!));
}

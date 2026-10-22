using System;
using System.Linq;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests.Rng;

public class Pcg32RandomTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalUIntSequence()
    {
        var a = new Pcg32Random(seed: 12345UL);
        var b = new Pcg32Random(seed: 12345UL);

        uint[] first = Enumerable.Range(0, 1000).Select(_ => a.NextUInt()).ToArray();
        uint[] second = Enumerable.Range(0, 1000).Select(_ => b.NextUInt()).ToArray();

        Assert.Equal(first, second);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalGaussianAndDoubleSequence()
    {
        var a = new Pcg32Random(seed: 777UL, sequence: 42UL);
        var b = new Pcg32Random(seed: 777UL, sequence: 42UL);

        for (int i = 0; i < 500; i++)
        {
            Assert.Equal(a.NextDouble(), b.NextDouble());
            Assert.Equal(a.NextGaussian(), b.NextGaussian());
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var a = new Pcg32Random(seed: 1UL);
        var b = new Pcg32Random(seed: 2UL);

        uint[] first = Enumerable.Range(0, 50).Select(_ => a.NextUInt()).ToArray();
        uint[] second = Enumerable.Range(0, 50).Select(_ => b.NextUInt()).ToArray();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentSequences_ProduceDifferentStreamsForSameSeed()
    {
        var a = new Pcg32Random(seed: 99UL, sequence: 1UL);
        var b = new Pcg32Random(seed: 99UL, sequence: 2UL);

        uint[] first = Enumerable.Range(0, 50).Select(_ => a.NextUInt()).ToArray();
        uint[] second = Enumerable.Range(0, 50).Select(_ => b.NextUInt()).ToArray();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NextDouble_StaysInUnitInterval()
    {
        var rng = new Pcg32Random(seed: 2024UL);

        for (int i = 0; i < 100_000; i++)
        {
            double value = rng.NextDouble();
            Assert.InRange(value, 0.0, 1.0 - double.Epsilon);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(40)]
    public void NextInt_StaysWithinBounds(int maxExclusive)
    {
        var rng = new Pcg32Random(seed: 555UL);

        for (int i = 0; i < 100_000; i++)
        {
            int value = rng.NextInt(maxExclusive);
            Assert.InRange(value, 0, maxExclusive - 1);
        }
    }

    [Fact]
    public void NextInt_CoversTheFullRange()
    {
        var rng = new Pcg32Random(seed: 8675309UL);
        var seen = new bool[6];

        for (int i = 0; i < 10_000; i++)
        {
            seen[rng.NextInt(6)] = true;
        }

        Assert.All(seen, hit => Assert.True(hit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NextInt_ThrowsForNonPositiveBound(int maxExclusive)
    {
        var rng = new Pcg32Random(seed: 1UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(maxExclusive));
    }

    [Fact]
    public void NextGaussian_HasApproximatelyStandardNormalMoments()
    {
        var rng = new Pcg32Random(seed: 314159UL);
        const int count = 200_000;

        double sum = 0.0;
        double sumSquares = 0.0;
        for (int i = 0; i < count; i++)
        {
            double sample = rng.NextGaussian();
            sum += sample;
            sumSquares += sample * sample;
        }

        double mean = sum / count;
        double variance = (sumSquares / count) - (mean * mean);

        // Loose bounds: enough to catch a broken transform, slack enough not to be flaky.
        Assert.InRange(mean, -0.02, 0.02);
        Assert.InRange(variance, 0.95, 1.05);
    }

    [Fact]
    public void InjectedAsIRandom_BehavesDeterministically()
    {
        // Guards the intended usage: components depend on IRandom, not the concrete type.
        IRandom rng = new Pcg32Random(seed: 2026UL);
        IRandom twin = new Pcg32Random(seed: 2026UL);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(rng.NextUInt(), twin.NextUInt());
        }
    }
}

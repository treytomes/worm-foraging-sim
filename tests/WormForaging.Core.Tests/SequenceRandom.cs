using System;
using System.Collections.Generic;
using WormForaging.Core.Rng;

namespace WormForaging.Core.Tests;

/// <summary>
/// A test double for <see cref="IRandom"/> that returns a preset queue of <see cref="NextDouble"/>
/// values, so a sampler's behaviour can be pinned to specific draws. The other members are not
/// used by the code under test and throw if called.
/// </summary>
internal sealed class SequenceRandom : IRandom
{
    private readonly Queue<double> _doubles;

    public SequenceRandom(params double[] doubles) => _doubles = new Queue<double>(doubles);

    public double NextDouble() => _doubles.Dequeue();

    public double NextGaussian() => throw new NotSupportedException();

    public uint NextUInt() => throw new NotSupportedException();

    public int NextInt(int maxExclusive) => throw new NotSupportedException();
}

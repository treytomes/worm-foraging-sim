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
    private readonly double[] _initial;

    public SequenceRandom(params double[] doubles)
    {
        _initial = doubles;
        _doubles = new Queue<double>(doubles);
    }

    /// <summary>Restores the scripted queue to its original contents.</summary>
    public void Reset() => Restore();

    /// <summary>
    /// Restores the scripted queue. A scripted fake has no real seed, so the arguments are ignored;
    /// this exists only to satisfy <see cref="IRandom"/> and mirror <see cref="Reset"/>'s semantics.
    /// </summary>
    public void Reseed(ulong seed, ulong sequence = 0) => Restore();

    private void Restore()
    {
        _doubles.Clear();
        foreach (double value in _initial)
        {
            _doubles.Enqueue(value);
        }
    }

    public double NextDouble() => _doubles.Dequeue();

    public double NextGaussian() => throw new NotSupportedException();

    public uint NextUInt() => throw new NotSupportedException();

    public int NextInt(int maxExclusive) => throw new NotSupportedException();
}

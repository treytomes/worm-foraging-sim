using System;
using WormForaging.Core;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>Tests for <see cref="SensePermutation"/> — the sense-swap mapping.</summary>
public class SensePermutationTests
{
    [Fact]
    public void Identity_MapsEachChannelToItself()
    {
        var p = SensePermutation.Identity;

        Assert.Equal(0, p[0]);
        Assert.Equal(1, p[1]);
        Assert.Equal(2, p[2]);
        Assert.Equal(3, p[3]);
        Assert.True(p.IsIdentity);
    }

    [Fact]
    public void Swapped_MatchesPrototypeMapping()
    {
        var p = SensePermutation.Swapped;

        Assert.Equal(2, p[0]);
        Assert.Equal(3, p[1]);
        Assert.Equal(0, p[2]);
        Assert.Equal(1, p[3]);
        Assert.False(p.IsIdentity);
    }

    [Fact]
    public void Toggle_FlipsBetweenIdentityAndSwapped()
    {
        Assert.Equal(SensePermutation.Swapped, SensePermutation.Identity.Toggle());
        Assert.Equal(SensePermutation.Identity, SensePermutation.Swapped.Toggle());
        // Any non-identity permutation toggles back to identity (two-state button).
        Assert.Equal(SensePermutation.Identity, new SensePermutation(1, 0, 2, 3).Toggle());
    }

    [Theory]
    [InlineData(0, 0, 1, 2)] // repeat
    [InlineData(0, 1, 2, 4)] // out of range high
    [InlineData(-1, 1, 2, 3)] // out of range low
    public void Constructor_RejectsNonPermutations(int c0, int c1, int c2, int c3)
        => Assert.Throws<ArgumentException>(() => new SensePermutation(c0, c1, c2, c3));

    [Fact]
    public void Default_IsNotUsable()
    {
        SensePermutation def = default;

        Assert.False(def.IsIdentity);
        Assert.Throws<InvalidOperationException>(() => def[0]);
    }

    [Fact]
    public void Indexer_RejectsOutOfRangeChannel()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SensePermutation.Identity[4]);

    [Fact]
    public void Equality_And_Operators()
    {
        Assert.True(SensePermutation.Identity == new SensePermutation(0, 1, 2, 3));
        Assert.True(SensePermutation.Identity != SensePermutation.Swapped);
        Assert.Equal(SensePermutation.Identity.GetHashCode(), new SensePermutation(0, 1, 2, 3).GetHashCode());
        Assert.False(SensePermutation.Identity.Equals("not a permutation"));
    }

    [Fact]
    public void ToString_ShowsMapping()
        => Assert.Equal("[2,3,0,1]", SensePermutation.Swapped.ToString());
}

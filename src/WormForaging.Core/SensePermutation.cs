namespace WormForaging.Core;

/// <summary>
/// A remapping of sensor channels to directions — the mechanism behind "swap senses."
/// </summary>
/// <remarks>
/// <para>
/// Each of the four odor channels and four heat channels reads one of the four directions. This
/// permutation says which: channel <c>i</c> reads the direction <c>perm[i]</c>. The prototype's
/// <c>perm</c> array does exactly this, and both the odor and heat sensors share it.
/// </para>
/// <para>
/// The sense-swap is the whole point of the Phase-2 experiment (does relearning get faster after
/// repeated swaps?), so the permutation is a first-class, validated value rather than a bare
/// array. <see cref="Identity"/> is the unswapped mapping; <see cref="Swapped"/> is the
/// prototype's swap (<c>[2,3,0,1]</c>); <see cref="Toggle"/> models the two-state swap button.
/// </para>
/// <para>
/// <c>default(SensePermutation)</c> is <em>not</em> a usable value — use <see cref="Identity"/>.
/// A defaulted instance throws when indexed, so it cannot silently behave as "all channels read
/// direction 0."
/// </para>
/// </remarks>
public readonly struct SensePermutation : IEquatable<SensePermutation>
{
    private readonly int _channel0;
    private readonly int _channel1;
    private readonly int _channel2;
    private readonly int _channel3;
    private readonly bool _initialized;

    /// <summary>
    /// Creates a permutation from the direction index each channel reads. The four values must be
    /// a permutation of {0, 1, 2, 3}.
    /// </summary>
    /// <param name="channel0">Direction read by channel 0.</param>
    /// <param name="channel1">Direction read by channel 1.</param>
    /// <param name="channel2">Direction read by channel 2.</param>
    /// <param name="channel3">Direction read by channel 3.</param>
    /// <exception cref="ArgumentException">The four values are not a permutation of {0, 1, 2, 3}.</exception>
    public SensePermutation(int channel0, int channel1, int channel2, int channel3)
    {
        Span<bool> seen = stackalloc bool[Directions.Count];
        ReadOnlySpan<int> values = [channel0, channel1, channel2, channel3];
        foreach (int v in values)
        {
            if (v < 0 || v >= Directions.Count || seen[v])
            {
                throw new ArgumentException(
                    $"[{channel0},{channel1},{channel2},{channel3}] is not a permutation of 0..{Directions.Count - 1}.");
            }

            seen[v] = true;
        }

        _channel0 = channel0;
        _channel1 = channel1;
        _channel2 = channel2;
        _channel3 = channel3;
        _initialized = true;
    }

    /// <summary>The unswapped mapping: channel <c>i</c> reads direction <c>i</c>.</summary>
    public static SensePermutation Identity => new(0, 1, 2, 3);

    /// <summary>The prototype's swap: channels read directions <c>[2, 3, 0, 1]</c>.</summary>
    public static SensePermutation Swapped => new(2, 3, 0, 1);

    /// <summary>The direction index read by the given channel.</summary>
    /// <param name="channel">The channel, 0..3.</param>
    /// <returns>The direction index that channel reads.</returns>
    /// <exception cref="InvalidOperationException">This is a defaulted (uninitialized) permutation.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="channel"/> is outside 0..3.</exception>
    public int this[int channel]
    {
        get
        {
            if (!_initialized)
            {
                throw new InvalidOperationException(
                    "Default SensePermutation is not usable; use SensePermutation.Identity.");
            }

            return channel switch
            {
                0 => _channel0,
                1 => _channel1,
                2 => _channel2,
                3 => _channel3,
                _ => throw new ArgumentOutOfRangeException(nameof(channel)),
            };
        }
    }

    /// <summary>Whether this is the unswapped <see cref="Identity"/> mapping.</summary>
    public bool IsIdentity => _initialized && _channel0 == 0 && _channel1 == 1 && _channel2 == 2 && _channel3 == 3;

    /// <summary>
    /// Returns the other state of the two-state swap button: <see cref="Swapped"/> from
    /// <see cref="Identity"/>, and <see cref="Identity"/> from anything else. Mirrors the
    /// prototype's <c>perm = perm[0]===0 ? [2,3,0,1] : [0,1,2,3]</c>.
    /// </summary>
    /// <returns>The toggled permutation.</returns>
    public SensePermutation Toggle() => IsIdentity ? Swapped : Identity;

    /// <summary>Whether two permutations map every channel the same way.</summary>
    /// <param name="other">The permutation to compare with.</param>
    /// <returns><see langword="true"/> if they are equal.</returns>
    public bool Equals(SensePermutation other)
        => _initialized == other._initialized
        && _channel0 == other._channel0
        && _channel1 == other._channel1
        && _channel2 == other._channel2
        && _channel3 == other._channel3;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SensePermutation other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(_channel0, _channel1, _channel2, _channel3, _initialized);

    /// <summary>Equality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if the permutations are equal.</returns>
    public static bool operator ==(SensePermutation left, SensePermutation right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if the permutations differ.</returns>
    public static bool operator !=(SensePermutation left, SensePermutation right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() => $"[{_channel0},{_channel1},{_channel2},{_channel3}]";
}

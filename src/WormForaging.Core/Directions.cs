namespace WormForaging.Core;

/// <summary>
/// The four movement directions, in the prototype's fixed order: North, East, South, West.
/// </summary>
/// <remarks>
/// This order is load-bearing: it is the order of the odor/heat sensors, the motor neurons, and
/// the <c>DIRS</c> array in the prototype (<c>[[0,-1],[1,0],[0,1],[-1,0]]</c>). The y axis points
/// down (screen coordinates), so North is <c>-y</c>. Senses and the motor layer both index by it.
/// </remarks>
public static class Directions
{
    /// <summary>
    /// The four unit steps as (dx, dy) pairs, ordered North, East, South, West. Matches the
    /// prototype's <c>DIRS</c>.
    /// </summary>
    public static IReadOnlyList<(int Dx, int Dy)> All { get; } = new[]
    {
        (0, -1), // North
        (1, 0),  // East
        (0, 1),  // South
        (-1, 0), // West
    };

    /// <summary>The number of directions (4).</summary>
    public const int Count = 4;
}

namespace WormForaging.App.Shell;

/// <summary>
/// Which palette the shell should paint itself with. The "system" option follows the operating
/// system's light/dark setting; the other two pin the shell to a palette.
/// </summary>
public enum AppTheme
{
    /// <summary>The light palette.</summary>
    Light,

    /// <summary>The dark palette.</summary>
    Dark,

    /// <summary>Follow the operating system's light/dark setting.</summary>
    System,
}

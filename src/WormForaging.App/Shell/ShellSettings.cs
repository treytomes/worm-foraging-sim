namespace WormForaging.App.Shell;

/// <summary>
/// The persisted shell preferences.
/// </summary>
/// <remarks>
/// Stored as JSON in the per-user application data folder. Missing or unrecognized values fall
/// back to the application's current state, so an older or hand-edited file can never break startup.
/// </remarks>
public sealed record ShellSettings
{
    /// <summary>The selected theme name (<see cref="AppTheme"/>), or <see langword="null"/> if unset.</summary>
    public string? Theme { get; init; }
}

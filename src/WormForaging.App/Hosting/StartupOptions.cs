namespace WormForaging.App.Hosting;

/// <summary>
/// Immutable options parsed from the command line that steer host startup.
/// </summary>
/// <param name="Seed">
/// Explicit RNG seed for a reproducible run, or <see langword="null"/> to fall back to configuration
/// (and then to a logged random seed).
/// </param>
/// <param name="ConfigPath">
/// Path to an <c>appsettings.yaml</c> to load instead of the one discovered at the repository root,
/// or <see langword="null"/> to use the default discovery.
/// </param>
/// <param name="LogLevel">
/// Serilog minimum level override (Verbose, Debug, Information, Warning, Error, Fatal), or
/// <see langword="null"/> to use the configured level.
/// </param>
public sealed record StartupOptions(ulong? Seed, string? ConfigPath, string? LogLevel);

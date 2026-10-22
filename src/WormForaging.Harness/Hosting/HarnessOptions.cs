namespace WormForaging.Harness.Hosting;

/// <summary>
/// The raw command-line options parsed by <c>Program.Main</c>, before they are resolved against
/// configuration and defaults. A <see langword="null"/> field means "not supplied on the command
/// line"; the resolver then falls back to <c>appsettings.yaml</c> and finally to a built-in default.
/// </summary>
/// <param name="Seeds">Number of distinct seeds to run per mode.</param>
/// <param name="BaseSeed">First seed; the run uses <c>BaseSeed, BaseSeed+1, ... BaseSeed+Seeds-1</c>.</param>
/// <param name="Steps">Number of simulation steps per run.</param>
/// <param name="SampleInterval">Record a time-series sample every this many steps.</param>
/// <param name="OutputDirectory">Directory the CSV files are written to.</param>
/// <param name="ConfigPath">Path to an <c>appsettings.yaml</c> to load instead of the discovered one.</param>
/// <param name="LogLevel">Serilog minimum-level override.</param>
internal sealed record HarnessOptions(
    int? Seeds,
    ulong? BaseSeed,
    int? Steps,
    int? SampleInterval,
    string? OutputDirectory,
    string? ConfigPath,
    string? LogLevel);

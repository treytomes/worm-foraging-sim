using System;
using System.CommandLine;
using Avalonia;
using WormForaging.App.Hosting;

namespace WormForaging.App;

/// <summary>
/// Application entry point. Parses the command line, stashes the resulting
/// <see cref="StartupOptions"/> for the host to read, then starts the Avalonia desktop lifetime.
/// </summary>
internal static class Program
{
    // Avalonia needs an STA thread on Windows.
    [STAThread]
    public static int Main(string[] args)
    {
        Option<ulong?> seedOption = new("--seed", "-s")
        {
            Description = "RNG seed for a reproducible run. Overrides Simulation:Seed in configuration.",
        };
        Option<string?> configOption = new("--config", "-c")
        {
            Description = "Path to an appsettings.yaml to use instead of the repository-root default.",
        };
        Option<string?> logLevelOption = new("--log-level")
        {
            Description = "Serilog minimum level (Verbose, Debug, Information, Warning, Error, Fatal).",
        };

        RootCommand root = new("Worm-inspired foraging simulation (learning without backpropagation).")
        {
            seedOption,
            configOption,
            logLevelOption,
        };

        // Parse-only: on a successful parse, launch the UI; --help, --version, and parse errors are
        // handled by System.CommandLine and short-circuit before the action runs.
        root.SetAction(parseResult =>
        {
            App.Startup = new StartupOptions(
                parseResult.GetValue(seedOption),
                parseResult.GetValue(configOption),
                parseResult.GetValue(logLevelOption));

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        });

        return root.Parse(args).Invoke();
    }

    /// <summary>Configures the Avalonia app builder. Used by <see cref="Main"/> and the visual designer.</summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}

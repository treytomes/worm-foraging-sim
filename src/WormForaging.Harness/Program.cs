using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using WormForaging.Harness.Experiment;
using WormForaging.Harness.Hosting;

namespace WormForaging.Harness;

/// <summary>
/// Harness entry point. Parses the command line into <see cref="HarnessOptions"/>, builds the
/// Generic Host (same shape as the Avalonia app's), resolves the <see cref="ExperimentRunner"/>, and
/// runs the Phase 1 baseline experiment to completion.
/// </summary>
internal static class Program
{
    /// <summary>Parses arguments and runs the experiment.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>0 on success, 1 if the run threw.</returns>
    public static int Main(string[] args)
    {
        Option<int?> seedsOption = new("--seeds", "-n")
        {
            Description = "Number of seeds to run per mode. Overrides Harness:Seeds in configuration.",
        };
        Option<ulong?> baseSeedOption = new("--base-seed", "-s")
        {
            Description = "First seed; the run uses base-seed .. base-seed+seeds-1. Overrides Harness:BaseSeed.",
        };
        Option<int?> stepsOption = new("--steps")
        {
            Description = "Simulation steps per run. Overrides Harness:Steps in configuration.",
        };
        Option<int?> sampleOption = new("--sample")
        {
            Description = "Record a time-series sample every N steps. Overrides Harness:SampleInterval.",
        };
        Option<string?> outOption = new("--out", "-o")
        {
            Description = "Directory for the CSV output. Overrides Harness:OutputDirectory.",
        };
        Option<string?> configOption = new("--config", "-c")
        {
            Description = "Path to an appsettings.yaml to use instead of the repository-root default.",
        };
        Option<string?> logLevelOption = new("--log-level")
        {
            Description = "Serilog minimum level (Verbose, Debug, Information, Warning, Error, Fatal).",
        };

        RootCommand root = new("Headless baseline runner for the worm foraging simulation (full vs. Hebbian vs. frozen).")
        {
            seedsOption,
            baseSeedOption,
            stepsOption,
            sampleOption,
            outOption,
            configOption,
            logLevelOption,
        };

        int exitCode = 0;
        root.SetAction(parseResult =>
        {
            var options = new HarnessOptions(
                Seeds: parseResult.GetValue(seedsOption),
                BaseSeed: parseResult.GetValue(baseSeedOption),
                Steps: parseResult.GetValue(stepsOption),
                SampleInterval: parseResult.GetValue(sampleOption),
                OutputDirectory: parseResult.GetValue(outOption),
                ConfigPath: parseResult.GetValue(configOption),
                LogLevel: parseResult.GetValue(logLevelOption));

            // 'using' disposes the host, which flushes and closes the Serilog sinks.
            using var host = HarnessHostBuilder.Build(options);
            host.Services.GetRequiredService<ExperimentRunner>().Run();
            return 0;
        });

        exitCode = root.Parse(args).Invoke();
        return exitCode;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using WormForaging.Harness.Experiment;

namespace WormForaging.Harness.Hosting;

/// <summary>
/// Builds the harness's Generic Host — the single composition root.
/// </summary>
/// <remarks>
/// Deliberately the same shape as the Avalonia app's <c>AppHostBuilder</c>: Microsoft dependency
/// injection, the repo-root <c>appsettings.yaml</c> (located by walking up from the executable),
/// and Microsoft.Extensions.Logging fronting Serilog (console + daily rolling file). The experiment
/// knobs resolve command line over configuration over defaults, mirroring how the app resolves its
/// seed. The two host builders are not shared because that would pull a Hosting/Serilog dependency
/// into something common; Core stays pure, so each executable keeps its own thin builder.
/// </remarks>
internal static class HarnessHostBuilder
{
    #region Constants

    /// <summary>The log file name stem; Serilog appends the rolling date and extension.</summary>
    private const string LOG_FILE_STEM = "harness-.log";

    /// <summary>The shared Serilog output template for both the console and file sinks.</summary>
    private const string OUTPUT_TEMPLATE = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>Default number of seeds per mode when neither the CLI nor configuration sets it.</summary>
    private const int DEFAULT_SEEDS = 10;

    /// <summary>Default first seed.</summary>
    private const ulong DEFAULT_BASE_SEED = 1;

    /// <summary>Default steps per run.</summary>
    private const int DEFAULT_STEPS = 20_000;

    /// <summary>Default steps between time-series samples.</summary>
    private const int DEFAULT_SAMPLE_INTERVAL = 100;

    /// <summary>Default output directory (relative to the config directory / repo root).</summary>
    private const string DEFAULT_OUTPUT_DIR = "results";

    #endregion

    #region Methods

    /// <summary>
    /// Builds (but does not start) the host from the given command-line options.
    /// </summary>
    /// <param name="options">The options resolved by <c>Program.Main</c>.</param>
    /// <returns>A configured, not-yet-started <see cref="IHost"/>.</returns>
    public static IHost Build(HarnessOptions options)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        var configFile = ResolveConfigFile(options.ConfigPath);
        var configDir = configFile is null ? AppContext.BaseDirectory : Path.GetDirectoryName(configFile)!;
        if (configFile is not null)
        {
            builder.Configuration.AddYamlFile(configFile, optional: false, reloadOnChange: true);
        }

        var serilog = CreateSerilogLogger(builder.Configuration, options.LogLevel, configDir);
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilog, dispose: true);

        var settings = ResolveExperimentSettings(options, builder.Configuration, configDir);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<Core.SimConfig>();
        builder.Services.AddSingleton<ExperimentRunner>();

        return builder.Build();
    }

    /// <summary>
    /// Resolves the configuration file: an explicit override if given (which must exist), otherwise
    /// the nearest <c>appsettings.yaml</c> found by walking up from the executable to the repo root.
    /// </summary>
    private static string? ResolveConfigFile(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var full = Path.GetFullPath(overridePath);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException($"Configuration file not found: {full}", full);
            }

            return full;
        }

        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "appsettings.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates the Serilog logger with a console sink and a daily rolling file sink. The minimum
    /// level comes from the command-line override, then configuration, then an Information default
    /// (quieter than the app's Debug default — a batch run should not drown the headline numbers).
    /// </summary>
    private static Serilog.ILogger CreateSerilogLogger(IConfiguration configuration, string? logLevelOverride, string configDir)
    {
        var minimumLevel =
            Enum.TryParse(logLevelOverride, ignoreCase: true, out LogEventLevel overrideLevel) ? overrideLevel
            : Enum.TryParse(configuration["Serilog:MinimumLevel"], ignoreCase: true, out LogEventLevel configLevel) ? configLevel
            : LogEventLevel.Information;

        var logDirSetting = configuration["Serilog:LogDirectory"] ?? "logs";
        var logDir = Path.IsPathRooted(logDirSetting) ? logDirSetting : Path.Combine(configDir, logDirSetting);
        Directory.CreateDirectory(logDir);

        var retainedFileCountLimit =
            int.TryParse(configuration["Serilog:RetainedFileCountLimit"], out int retained) ? retained : 14;

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Console(outputTemplate: OUTPUT_TEMPLATE)
            .WriteTo.File(
                Path.Combine(logDir, LOG_FILE_STEM),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedFileCountLimit,
                outputTemplate: OUTPUT_TEMPLATE)
            .CreateLogger();
    }

    /// <summary>
    /// Resolves each experiment knob in priority order (command line, then the <c>Harness:</c>
    /// configuration section, then a built-in default), validates it, and makes the output directory
    /// absolute (relative paths resolve against the config directory, i.e. the repo root).
    /// </summary>
    private static ExperimentSettings ResolveExperimentSettings(HarnessOptions options, IConfiguration configuration, string configDir)
    {
        int seeds = Resolve(options.Seeds, configuration["Harness:Seeds"], DEFAULT_SEEDS, int.TryParse);
        ulong baseSeed = Resolve(options.BaseSeed, configuration["Harness:BaseSeed"], DEFAULT_BASE_SEED, ulong.TryParse);
        int steps = Resolve(options.Steps, configuration["Harness:Steps"], DEFAULT_STEPS, int.TryParse);
        int sample = Resolve(options.SampleInterval, configuration["Harness:SampleInterval"], DEFAULT_SAMPLE_INTERVAL, int.TryParse);

        var outSetting = options.OutputDirectory ?? configuration["Harness:OutputDirectory"] ?? DEFAULT_OUTPUT_DIR;
        var outputDir = Path.IsPathRooted(outSetting) ? outSetting : Path.Combine(configDir, outSetting);

        if (seeds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), seeds, "Seeds must be at least 1.");
        }

        if (steps < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), steps, "Steps must be at least 1.");
        }

        if (sample < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), sample, "Sample interval must be at least 1.");
        }

        return new ExperimentSettings(seeds, baseSeed, steps, sample, Path.GetFullPath(outputDir));
    }

    /// <summary>A parser delegate matching <c>int.TryParse</c> / <c>ulong.TryParse</c>.</summary>
    private delegate bool TryParse<T>(string? text, out T value);

    /// <summary>Picks the first available value: the CLI override, then config, then the default.</summary>
    private static T Resolve<T>(T? cli, string? configValue, T fallback, TryParse<T> parse) where T : struct
    {
        if (cli is T supplied)
        {
            return supplied;
        }

        return parse(configValue, out T parsed) ? parsed : fallback;
    }

    #endregion
}

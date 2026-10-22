using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using WormForaging.App.ViewModels;
using WormForaging.Core.Rng;

namespace WormForaging.App.Hosting;

/// <summary>
/// Builds the application's Generic Host — the single composition root.
/// </summary>
/// <remarks>
/// Mirrors the HyperSpace shell: the host owns Microsoft dependency injection, YAML configuration,
/// and Microsoft.Extensions.Logging fronting Serilog. Keeping this in one place makes the startup
/// sequence easy to read and lets the seed/config/logging resolution be reasoned about on its own.
/// </remarks>
public static class AppHostBuilder
{
    /// <summary>The log file name stem; Serilog appends the rolling date and extension.</summary>
    private const string LogFileStem = "wormforaging-.log";

    /// <summary>The shared Serilog output template for both the console and file sinks.</summary>
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Builds (but does not start) the host from the given startup options.
    /// </summary>
    /// <param name="options">The command-line options resolved by <c>Program.Main</c>.</param>
    /// <returns>A configured, not-yet-started <see cref="IHost"/>.</returns>
    public static IHost Build(StartupOptions options)
    {
        // Content root is the executable directory so relative defaults are stable regardless of cwd.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        string? configFile = ResolveConfigFile(options.ConfigPath);
        string configDir = configFile is null ? AppContext.BaseDirectory : Path.GetDirectoryName(configFile)!;
        if (configFile is not null)
        {
            builder.Configuration.AddYamlFile(configFile, optional: false, reloadOnChange: true);
        }

        Serilog.ILogger serilog = CreateSerilogLogger(builder.Configuration, options.LogLevel, configDir);
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilog, dispose: true);

        SimulationStartup simulation = ResolveSimulationStartup(options, builder.Configuration);
        builder.Services.AddSingleton(simulation);
        builder.Services.AddSingleton<IRandom>(_ => new Pcg32Random(simulation.Seed));

        builder.Services.AddSingleton<MainWindowViewModel>();

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
            string full = Path.GetFullPath(overridePath);
            if (!File.Exists(full))
            {
                throw new FileNotFoundException($"Configuration file not found: {full}", full);
            }

            return full;
        }

        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "appsettings.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates the Serilog logger with a console sink and a daily rolling file sink. The minimum
    /// level comes from the command-line override, then configuration, then a Debug default.
    /// </summary>
    private static Serilog.ILogger CreateSerilogLogger(IConfiguration configuration, string? logLevelOverride, string configDir)
    {
        LogEventLevel minimumLevel =
            Enum.TryParse(logLevelOverride, ignoreCase: true, out LogEventLevel overrideLevel) ? overrideLevel
            : Enum.TryParse(configuration["Serilog:MinimumLevel"], ignoreCase: true, out LogEventLevel configLevel) ? configLevel
            : LogEventLevel.Debug;

        string logDirSetting = configuration["Serilog:LogDirectory"] ?? "logs";
        string logDir = Path.IsPathRooted(logDirSetting) ? logDirSetting : Path.Combine(configDir, logDirSetting);
        Directory.CreateDirectory(logDir);

        int retainedFileCountLimit =
            int.TryParse(configuration["Serilog:RetainedFileCountLimit"], out int retained) ? retained : 14;

        return new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Console(outputTemplate: OutputTemplate)
            .WriteTo.File(
                Path.Combine(logDir, LogFileStem),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: retainedFileCountLimit,
                outputTemplate: OutputTemplate)
            .CreateLogger();
    }

    /// <summary>
    /// Resolves the seed in priority order: command line, then configuration, then a fresh random
    /// seed (recorded as <see cref="SeedSource.Random"/> so the run can be reproduced from the log).
    /// </summary>
    private static SimulationStartup ResolveSimulationStartup(StartupOptions options, IConfiguration configuration)
    {
        if (options.Seed is ulong cliSeed)
        {
            return new SimulationStartup(cliSeed, SeedSource.CommandLine);
        }

        if (ulong.TryParse(configuration["Simulation:Seed"], out ulong configSeed))
        {
            return new SimulationStartup(configSeed, SeedSource.Configuration);
        }

        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return new SimulationStartup(BitConverter.ToUInt64(bytes), SeedSource.Random);
    }
}

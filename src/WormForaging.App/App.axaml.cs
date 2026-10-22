using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WormForaging.App.Hosting;
using WormForaging.App.ViewModels;
using WormForaging.App.Views;

namespace WormForaging.App;

/// <summary>
/// The Avalonia application and composition root.
/// </summary>
/// <remarks>
/// Builds and starts the Generic Host (Microsoft DI, YAML configuration, Serilog), then shows the
/// main window with a DI-resolved view model. The host is stopped and disposed on desktop shutdown.
/// The pattern follows the HyperSpace shell.
/// </remarks>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>
    /// The command-line options, set by <c>Program.Main</c> before the app starts.
    /// </summary>
    public static StartupOptions Startup { get; set; } = new(Seed: null, ConfigPath: null, LogLevel: null);

    /// <summary>
    /// The application's service provider (composition root). Valid once startup has completed.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Loads the application's XAML (theme and styles). Called by the Avalonia runtime during startup.
    /// </summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Builds and starts the host, then creates the main window with its DI-resolved view model.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        _host = AppHostBuilder.Build(Startup);
        _host.Start();
        Services = _host.Services;

        SimulationStartup simulation = Services.GetRequiredService<SimulationStartup>();
        Services.GetRequiredService<ILogger<App>>().LogInformation(
            "WormForaging started. Seed {Seed} (from {Source}).", simulation.Seed, simulation.Source);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                // Stop hosted services, dispose singletons, and flush Serilog.
                _host?.StopAsync().GetAwaiter().GetResult();
                _host?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

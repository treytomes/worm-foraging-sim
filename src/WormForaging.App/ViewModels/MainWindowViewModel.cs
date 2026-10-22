using System.Globalization;
using Microsoft.Extensions.Logging;
using WormForaging.App.Hosting;
using WormForaging.Core.Rng;

namespace WormForaging.App.ViewModels;

/// <summary>
/// View model for the main window.
/// </summary>
/// <remarks>
/// For now it proves the host wiring end to end: it reports the resolved RNG seed and the first few
/// draws from the injected <see cref="IRandom"/>, confirming that dependency injection,
/// configuration, and the deterministic generator are all connected. It will grow into the
/// simulation's control surface as the port proceeds.
/// </remarks>
public sealed class MainWindowViewModel : ViewModelBase
{
    private string _status;

    /// <summary>
    /// Initializes the view model from the injected services and records a short startup summary.
    /// </summary>
    /// <param name="random">The deterministic generator for the active run.</param>
    /// <param name="simulation">The resolved seed and where it came from.</param>
    /// <param name="logger">Logger for startup diagnostics.</param>
    public MainWindowViewModel(IRandom random, SimulationStartup simulation, ILogger<MainWindowViewModel> logger)
    {
        double first = random.NextDouble();
        double second = random.NextDouble();
        double third = random.NextDouble();

        _status = string.Format(
            CultureInfo.InvariantCulture,
            "Seed {0} (from {1}). First three draws: {2:F4}, {3:F4}, {4:F4}.",
            simulation.Seed, simulation.Source, first, second, third);

        logger.LogInformation("Main view model initialized. {Status}", _status);
    }

    /// <summary>
    /// A human-readable summary of the active seed and the generator's first draws.
    /// </summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }
}

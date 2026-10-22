using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using System.Globalization;
using WormForaging.App.Hosting;
using WormForaging.Core;
using WormForaging.Core.Rng;

namespace WormForaging.App.ViewModels;

/// <summary>
/// View model for the main window.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    #region Fields

    private readonly SimConfig _config;
    private readonly SimulationStartup _simulationStartup;
    private readonly ILogger<MainWindowViewModel> _logger;
    private Simulation _simulation;
    private IRandom _random;
    private DispatcherTimer _timer;
    private bool _isRunning = true;
    private int _speed = 8;
    private double _exploreTemperature = 1.0;
    private LearningMode _mode = LearningMode.Full;
    private bool _homeostasisEnabled = true;
    private SimulationSnapshot? _currentSnapshot;
    private List<(double X, double Y)> _trail = new();
    private readonly List<(double Energy, double BodyTemp)> _historyBuffer = new();
    private IReadOnlyList<(double Energy, double BodyTemp)> _history = Array.Empty<(double, double)>();
    private string _statusText = string.Empty;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes the view model from the injected services.
    /// </summary>
    public MainWindowViewModel(
        SimConfig config,
        SimulationStartup simulationStartup,
        IRandom random,
        ILogger<MainWindowViewModel> logger
    )
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _simulationStartup = simulationStartup ?? throw new ArgumentNullException(nameof(simulationStartup));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _simulation = new Simulation(_config, new Pcg32Random(_simulationStartup.Seed));
        _exploreTemperature = _simulation.ExploreTemperature;
        _homeostasisEnabled = _simulation.HomeostasisEnabled;
        _mode = _simulation.Mode;

        ToggleRunCommand = new DelegateCommand(ToggleRun);
        ResetCommand = new DelegateCommand(Reset);
        SwapSensesCommand = new DelegateCommand(SwapSenses);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        UpdateSnapshot();
        AppendHistorySample();
        _logger.LogInformation("Main view model initialized with seed {Seed} from {Source}", _simulationStartup.Seed, _simulationStartup.Source);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the command to toggle running state.
    /// </summary>
    public DelegateCommand ToggleRunCommand { get; }

    /// <summary>
    /// Gets the command to reset the simulation.
    /// </summary>
    public DelegateCommand ResetCommand { get; }

    /// <summary>
    /// Gets the command to swap senses.
    /// </summary>
    public DelegateCommand SwapSensesCommand { get; }

    /// <summary>
    /// Gets or sets whether the simulation is running.
    /// </summary>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                RaisePropertyChanged(nameof(RunButtonText));
            }
        }
    }

    /// <summary>
    /// Gets the text for the run/pause button.
    /// </summary>
    public string RunButtonText => IsRunning ? "Pause" : "Run";

    /// <summary>
    /// Gets or sets the simulation speed (steps per frame).
    /// </summary>
    public int Speed
    {
        get => _speed;
        set => SetProperty(ref _speed, value);
    }

    /// <summary>
    /// Gets or sets the exploration temperature.
    /// </summary>
    public double ExploreTemperature
    {
        get => _exploreTemperature;
        set
        {
            if (SetProperty(ref _exploreTemperature, value))
            {
                if (_simulation != null)
                {
                    _simulation.ExploreTemperature = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets the learning mode index.
    /// </summary>
    public int ModeIndex
    {
        get => _mode switch
        {
            LearningMode.Full => 0,
            LearningMode.Hebbian => 1,
            LearningMode.Frozen => 2,
            _ => 0
        };
        set
        {
            LearningMode newMode = value switch
            {
                0 => LearningMode.Full,
                1 => LearningMode.Hebbian,
                2 => LearningMode.Frozen,
                _ => LearningMode.Full
            };
            if (SetProperty(ref _mode, newMode) && _simulation != null)
            {
                _simulation.Mode = newMode;
            }
        }
    }

    /// <summary>
    /// Gets or sets whether homeostasis is enabled.
    /// </summary>
    public bool HomeostasisEnabled
    {
        get => _homeostasisEnabled;
        set
        {
            if (SetProperty(ref _homeostasisEnabled, value))
            {
                if (_simulation != null)
                {
                    _simulation.HomeostasisEnabled = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets the current simulation snapshot.
    /// </summary>
    public SimulationSnapshot? CurrentSnapshot
    {
        get => _currentSnapshot;
        set => SetProperty(ref _currentSnapshot, value);
    }

    /// <summary>
    /// Gets the worm trail.
    /// </summary>
    public IReadOnlyList<(double X, double Y)> Trail => _trail;

    /// <summary>
    /// Gets the rolling history of energy and body temperature.
    /// </summary>
    /// <remarks>
    /// Each update replaces this with a fresh array reference rather than mutating one in place:
    /// a bound control only re-renders when its styled property changes value, and a stable
    /// mutable reference never looks changed.
    /// </remarks>
    public IReadOnlyList<(double Energy, double BodyTemp)> History
    {
        get => _history;
        private set => SetProperty(ref _history, value);
    }

    /// <summary>
    /// Gets the comfortable body temperature the plots use to mark thermal comfort.
    /// </summary>
    public double ComfortTemperature => _config.ComfortTemperature;

    /// <summary>
    /// Gets the arena side length in cells, used by the arena control to derive its drawing scale.
    /// </summary>
    public int ArenaSize => _config.ArenaSize;

    /// <summary>
    /// Gets the status text.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    #endregion

    #region Methods

    private void ToggleRun()
    {
        IsRunning = !IsRunning;
    }

    private void Reset()
    {
        _trail.Clear();
        _historyBuffer.Clear();
        History = Array.Empty<(double, double)>();
        _random.Reset();
        _simulation = new(_config, _random)
        {
            ExploreTemperature = _exploreTemperature,
            HomeostasisEnabled = _homeostasisEnabled,
            Mode = _mode,
        };
        UpdateSnapshot();
        AppendHistorySample();
    }

    private void SwapSenses()
    {
        _simulation?.SwapSenses();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsRunning || _simulation == null)
        {
            return;
        }

        for (var i = 0; i < Speed; i++)
        {
            _simulation.Step();
        }

        UpdateTrail();
        AppendHistorySample();
        UpdateSnapshot();
        RaisePropertyChanged(nameof(Trail));
    }

    private void UpdateTrail()
    {
        if (_simulation == null)
        {
            return;
        }

        var snap = _simulation.Snapshot();
        _trail.Add((snap.WormX, snap.WormY));
        if (_trail.Count > 60)
        {
            _trail.RemoveAt(0);
        }
    }

    private void AppendHistorySample()
    {
        if (_simulation == null)
        {
            return;
        }

        var snap = _simulation.Snapshot();
        _historyBuffer.Add((snap.Energy, snap.BodyTemperature));
        if (_historyBuffer.Count > 240)
        {
            _historyBuffer.RemoveAt(0);
        }

        // Hand the control a fresh reference so its binding actually re-renders.
        History = _historyBuffer.ToArray();
    }

    private void UpdateSnapshot()
    {
        if (_simulation == null)
        {
            return;
        }

        CurrentSnapshot = _simulation.Snapshot();
        var snap = CurrentSnapshot;
        if (snap != null)
        {
            var last5Avg = snap.MealIntervals.Count > 0
                ? snap.MealIntervals.TakeLast(5).Average()
                : 0;
            StatusText = string.Format(
                CultureInfo.InvariantCulture,
                "meals {0} · last-5 avg {1} · energy {2:F2} · gut {3:F2} · body {4:F2}",
                snap.MealsEaten,
                snap.MealIntervals.Count > 0 ? last5Avg.ToString("F0", CultureInfo.InvariantCulture) : "–",
                snap.Energy,
                snap.Gut,
                snap.BodyTemperature);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _timer.Stop();
    }

    #endregion
}

using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using System.Globalization;
using WormForaging.App.Hosting;
using WormForaging.App.Shell;
using WormForaging.Core;
using WormForaging.Core.Rng;

namespace WormForaging.App.ViewModels;

/// <summary>
/// View model for the main window.
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    #region Fields

    /// <summary>The settings file the theme choice is persisted through.</summary>
    private const string SettingsFileName = "shell.json";

    private readonly SimConfig _config;
    private readonly SimulationStartup _simulationStartup;
    private readonly ISettingsStore _settings;
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
    private NetworkTopology _topology;
    private List<(double X, double Y)> _trail = new();
    private readonly List<(double Energy, double BodyTemp)> _historyBuffer = new();
    private IReadOnlyList<(double Energy, double BodyTemp)> _history = Array.Empty<(double, double)>();
    private string _statusText = string.Empty;
    private AppTheme _theme;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes the view model from the injected services.
    /// </summary>
    public MainWindowViewModel(
        SimConfig config,
        SimulationStartup simulationStartup,
        IRandom random,
        ISettingsStore settings,
        ILogger<MainWindowViewModel> logger
    )
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _simulationStartup = simulationStartup ?? throw new ArgumentNullException(nameof(simulationStartup));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _simulation = new Simulation(_config, new Pcg32Random(_simulationStartup.Seed));
        _topology = _simulation.Topology;
        _exploreTemperature = _simulation.ExploreTemperature;
        _homeostasisEnabled = _simulation.HomeostasisEnabled;
        _mode = _simulation.Mode;

        // Restore the saved theme and apply it before the window is shown, so the shell opens in
        // the chosen palette with no light-to-dark flash.
        _theme = LoadSavedTheme();
        ApplyTheme(_theme);

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
    /// Gets the network's fixed wiring, for the live wiring diagram. Replaced on reset, since a new
    /// seed means new wiring.
    /// </summary>
    public NetworkTopology Topology
    {
        get => _topology;
        private set => SetProperty(ref _topology, value);
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
    /// Gets or sets the selected theme. Setting it applies the variant to the application and
    /// persists the choice, so the shell re-themes immediately and reopens the same way.
    /// </summary>
    public AppTheme Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                ApplyTheme(value);
                SaveTheme(value);

                // The toolbar's radio buttons bind to the three booleans below.
                RaisePropertyChanged(nameof(IsLightTheme));
                RaisePropertyChanged(nameof(IsDarkTheme));
                RaisePropertyChanged(nameof(IsSystemTheme));
            }
        }
    }

    /// <summary>True when the light palette is selected (bound to the theme switch).</summary>
    public bool IsLightTheme
    {
        get => Theme == AppTheme.Light;
        set
        {
            if (value)
            {
                Theme = AppTheme.Light;
            }
        }
    }

    /// <summary>True when the dark palette is selected (bound to the theme switch).</summary>
    public bool IsDarkTheme
    {
        get => Theme == AppTheme.Dark;
        set
        {
            if (value)
            {
                Theme = AppTheme.Dark;
            }
        }
    }

    /// <summary>True when the system setting is followed (bound to the theme switch).</summary>
    public bool IsSystemTheme
    {
        get => Theme == AppTheme.System;
        set
        {
            if (value)
            {
                Theme = AppTheme.System;
            }
        }
    }

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
        Topology = _simulation.Topology;
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

    /// <summary>
    /// The saved theme from <c>shell.json</c>, or the application's current theme when nothing was
    /// saved (or the saved name is unrecognized).
    /// </summary>
    private AppTheme LoadSavedTheme()
    {
        var saved = _settings.Load<ShellSettings>(SettingsFileName);
        return Enum.TryParse<AppTheme>(saved?.Theme, ignoreCase: true, out var theme)
            ? theme
            : CurrentTheme();
    }

    /// <summary>Persists the selected theme to <c>shell.json</c> (write-through on every change).</summary>
    private void SaveTheme(AppTheme theme)
    {
        _settings.Save(SettingsFileName, new ShellSettings { Theme = theme.ToString() });
    }

    /// <summary>The theme the application is currently painted with.</summary>
    private static AppTheme CurrentTheme()
    {
        var variant = Avalonia.Application.Current?.RequestedThemeVariant;

        // ThemeVariant is a record, so the variants compare by equality (they can't be switch
        // patterns: those need constants).
        if (variant == ThemeVariant.Light)
        {
            return AppTheme.Light;
        }

        return variant == ThemeVariant.Dark ? AppTheme.Dark : AppTheme.System;
    }

    /// <summary>Asks the application for a theme variant.</summary>
    private static void ApplyTheme(AppTheme theme)
    {
        var application = Avalonia.Application.Current;
        if (application is null)
        {
            return;
        }

        application.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _timer.Stop();
    }

    #endregion
}

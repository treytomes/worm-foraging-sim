using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WormForaging.Core;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders the simulation arena.
/// </summary>
/// <remarks>
/// Renders the worm, food, heat lamp, trails, and glow effects using Avalonia's
/// <see cref="DrawingContext"/>. No simulation logic lives here - it only draws
/// state provided by the view model.
/// </remarks>
public sealed class ArenaControl : Control
{
    #region Constants

    /// <summary>Thermal deviation below which the discomfort ring is not drawn.</summary>
    private const double RingDeadband = 0.08;

    /// <summary>
    /// Defines the <see cref="SimulationSnapshot"/> property.
    /// </summary>
    public static readonly StyledProperty<SimulationSnapshot?> SimulationSnapshotProperty =
        AvaloniaProperty.Register<ArenaControl, SimulationSnapshot?>(nameof(SimulationSnapshot), coerce: CoerceSnapshot);

    /// <summary>
    /// Defines the <see cref="Trail"/> property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<(double X, double Y)>?> TrailProperty =
        AvaloniaProperty.Register<ArenaControl, IReadOnlyList<(double X, double Y)>?>(nameof(Trail), coerce: CoerceTrail);

    /// <summary>
    /// Defines the <see cref="Scale"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<ArenaControl, double>(nameof(Scale), 9.0);

    /// <summary>
    /// Defines the <see cref="ComfortTemperature"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ComfortTemperatureProperty =
        AvaloniaProperty.Register<ArenaControl, double>(nameof(ComfortTemperature), double.NaN);

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="ArenaControl"/> class.
    /// </summary>
    public ArenaControl()
    {
        Width = 360;
        Height = 360;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the current simulation snapshot.
    /// </summary>
    public SimulationSnapshot? SimulationSnapshot
    {
        get => GetValue(SimulationSnapshotProperty);
        set => SetValue(SimulationSnapshotProperty, value);
    }

    /// <summary>
    /// Gets or sets the worm trail.
    /// </summary>
    public IReadOnlyList<(double X, double Y)>? Trail
    {
        get => GetValue(TrailProperty);
        set => SetValue(TrailProperty, value);
    }

    /// <summary>
    /// Gets or sets the pixel scale for rendering.
    /// </summary>
    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the comfortable body temperature, used to flag thermal discomfort with a ring.
    /// When unset (<see cref="double.NaN"/>), the ring is not drawn.
    /// </summary>
    public double ComfortTemperature
    {
        get => GetValue(ComfortTemperatureProperty);
        set => SetValue(ComfortTemperatureProperty, value);
    }

    #endregion

    #region Methods

    private static SimulationSnapshot? CoerceSnapshot(AvaloniaObject obj, SimulationSnapshot? value)
    {
        if (obj is ArenaControl control)
        {
            control.InvalidateVisual();
        }
        return value;
    }

    private static IReadOnlyList<(double X, double Y)>? CoerceTrail(AvaloniaObject obj, IReadOnlyList<(double X, double Y)>? value)
    {
        if (obj is ArenaControl control)
        {
            control.InvalidateVisual();
        }
        return value;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SimulationSnapshotProperty
            || change.Property == TrailProperty
            || change.Property == ScaleProperty
            || change.Property == ComfortTemperatureProperty)
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var snapshot = SimulationSnapshot;
        if (snapshot == null)
        {
            return;
        }

        var scale = Scale;
        Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);

        // Background
        context.FillRectangle(new SolidColorBrush(Color.Parse("#1f1f1c")), bounds);

        // Lamp glow
        DrawRadialGlow(context, snapshot.LampX * scale, snapshot.LampY * scale, 140, Color.Parse("#f08a4b"), 0.42);

        // Food glow
        DrawRadialGlow(context, snapshot.FoodX * scale, snapshot.FoodY * scale, 150, Color.Parse("#4fb88a"), 0.42);

        // Lamp
        context.FillRectangle(
            new SolidColorBrush(Color.Parse("#c2410c")),
            new Rect(snapshot.LampX * scale - 6, snapshot.LampY * scale - 6, 12, 12));

        // Food
        context.DrawEllipse(
            new SolidColorBrush(Color.Parse("#2f7d5b")),
            null,
            new Point(snapshot.FoodX * scale, snapshot.FoodY * scale),
            6,
            6);

        // Trail
        var trail = Trail;
        if (trail != null && trail.Count > 1)
        {
            var trailBrush = new SolidColorBrush(Color.Parse("#9a9a92"));
            var trailPen = new Pen(trailBrush, 1);
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(trail[0].X * scale, trail[0].Y * scale), false);
                for (int i = 1; i < trail.Count; i++)
                {
                    ctx.LineTo(new Point(trail[i].X * scale, trail[i].Y * scale));
                }
                ctx.EndFigure(false);
            }
            context.DrawGeometry(null, trailPen, geometry);
        }

        // Worm
        context.DrawEllipse(
            new SolidColorBrush(Color.Parse("#ecece6")),
            null,
            new Point(snapshot.WormX * scale, snapshot.WormY * scale),
            5,
            5);

        // Temperature ring
        var comfortTemp = ComfortTemperature;
        var dev = snapshot.BodyTemperature - comfortTemp;
        if (!double.IsNaN(comfortTemp) && Math.Abs(dev) > RingDeadband)
        {
            var ringColor = dev > 0 ? Color.Parse("#f08a4b") : Color.Parse("#4f8fd8");
            context.DrawEllipse(
                null,
                new Pen(new SolidColorBrush(ringColor), 3),
                new Point(snapshot.WormX * scale, snapshot.WormY * scale),
                9,
                9);
        }
    }

    private static void DrawRadialGlow(DrawingContext context, double x, double y, double radius, Color color, double opacity)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(x, y, RelativeUnit.Absolute),
            Center = new RelativePoint(x, y, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B), 0),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
            }
        };
        context.FillRectangle(brush, new Rect(0, 0, 360, 360));
    }

    #endregion
}

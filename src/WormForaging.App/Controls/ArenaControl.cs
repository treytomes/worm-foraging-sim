using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WormForaging.Core;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders the simulation arena.
/// </summary>
/// <remarks>
/// <para>
/// Renders the worm, food, heat lamp, trails, and glow effects using Avalonia's
/// <see cref="DrawingContext"/>. No simulation logic lives here - it only draws
/// state provided by the view model.
/// </para>
/// <para>
/// The control has no intrinsic size: it fills its container and fits a centered, square arena
/// inside (letterboxing any excess on the longer axis). Everything in the arena - positions,
/// glyph sizes, pen widths, and glow radii - is scaled by a single factor derived from the fitted
/// square and <see cref="ArenaSize"/>, so the drawing stays proportional at any size. Its own
/// background is painted as a distinct square so the arena reads as separate from its host.
/// </para>
/// </remarks>
public sealed class ArenaControl : Control
{
    #region Constants

    /// <summary>Thermal deviation below which the discomfort ring is not drawn.</summary>
    private const double RingDeadband = 0.08;

    /// <summary>Background color of the control (distinguishes the arena from its host).</summary>
    private static readonly Color BackgroundColor = Color.Parse("#121210");

    /// <summary>Color of the (square) arena field within the control.</summary>
    private static readonly Color ArenaColor = Color.Parse("#1f1f1c");

    /// <summary>Lamp half-size, in cells.</summary>
    private const double LampHalfSizeCells = 6.0 / 9.0;

    /// <summary>Food dot radius, in cells.</summary>
    private const double FoodRadiusCells = 6.0 / 9.0;

    /// <summary>Worm dot radius, in cells.</summary>
    private const double WormRadiusCells = 5.0 / 9.0;

    /// <summary>Discomfort ring radius, in cells.</summary>
    private const double RingRadiusCells = 9.0 / 9.0;

    /// <summary>Ring stroke width, in cells (floored to one pixel when drawn).</summary>
    private const double RingStrokeCells = 3.0 / 9.0;

    /// <summary>Trail stroke width, in cells (floored to one pixel when drawn).</summary>
    private const double TrailStrokeCells = 1.0 / 9.0;

    /// <summary>Lamp glow radius, in cells.</summary>
    private const double LampGlowRadiusCells = 140.0 / 9.0;

    /// <summary>Food glow radius, in cells.</summary>
    private const double FoodGlowRadiusCells = 150.0 / 9.0;

    /// <summary>Opacity of the radial glows.</summary>
    private const double GlowOpacity = 0.42;

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
    /// Defines the <see cref="ArenaSize"/> property.
    /// </summary>
    public static readonly StyledProperty<int> ArenaSizeProperty =
        AvaloniaProperty.Register<ArenaControl, int>(nameof(ArenaSize), 40);

    /// <summary>
    /// Defines the <see cref="Padding"/> property.
    /// </summary>
    public static readonly StyledProperty<double> PaddingProperty =
        AvaloniaProperty.Register<ArenaControl, double>(nameof(Padding), 6.0);

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
        MinWidth = 120;
        MinHeight = 120;
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
    /// Gets or sets the arena side length in cells, used to derive the drawing scale.
    /// </summary>
    public int ArenaSize
    {
        get => GetValue(ArenaSizeProperty);
        set => SetValue(ArenaSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the inset between the control edge and the fitted arena square, in pixels.
    /// </summary>
    public double Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
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
            || change.Property == ArenaSizeProperty
            || change.Property == PaddingProperty
            || change.Property == ComfortTemperatureProperty)
        {
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0 || ArenaSize <= 0)
        {
            return;
        }

        // Control background, distinct from the arena field.
        context.FillRectangle(new SolidColorBrush(BackgroundColor), new Rect(0, 0, width, height));

        // Fit a centered square arena inside the padded control box (letterbox the excess).
        double available = Math.Min(width, height) - Padding * 2;
        if (available <= 0)
        {
            return;
        }

        double scale = available / ArenaSize;
        double arenaPx = scale * ArenaSize;
        double originX = (width - arenaPx) / 2;
        double originY = (height - arenaPx) / 2;

        // Arena field.
        var arenaRect = new Rect(originX, originY, arenaPx, arenaPx);
        context.FillRectangle(new SolidColorBrush(ArenaColor), arenaRect);

        var snapshot = SimulationSnapshot;
        if (snapshot == null)
        {
            return;
        }

        double Px(double cells) => cells * scale;
        Point At(double x, double y) => new(originX + x * scale, originY + y * scale);

        // Lamp glow.
        DrawRadialGlow(context, At(snapshot.LampX, snapshot.LampY), Px(LampGlowRadiusCells), Color.Parse("#f08a4b"), width, height);

        // Food glow.
        DrawRadialGlow(context, At(snapshot.FoodX, snapshot.FoodY), Px(FoodGlowRadiusCells), Color.Parse("#4fb88a"), width, height);

        // Lamp.
        double lampHalf = Px(LampHalfSizeCells);
        Point lampCenter = At(snapshot.LampX, snapshot.LampY);
        context.FillRectangle(
            new SolidColorBrush(Color.Parse("#c2410c")),
            new Rect(lampCenter.X - lampHalf, lampCenter.Y - lampHalf, lampHalf * 2, lampHalf * 2));

        // Food.
        double foodRadius = Px(FoodRadiusCells);
        context.DrawEllipse(
            new SolidColorBrush(Color.Parse("#2f7d5b")),
            null,
            At(snapshot.FoodX, snapshot.FoodY),
            foodRadius,
            foodRadius);

        // Trail.
        var trail = Trail;
        if (trail != null && trail.Count > 1)
        {
            var trailPen = new Pen(new SolidColorBrush(Color.Parse("#9a9a92")), Math.Max(1, Px(TrailStrokeCells)));
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(At(trail[0].X, trail[0].Y), false);
                for (int i = 1; i < trail.Count; i++)
                {
                    ctx.LineTo(At(trail[i].X, trail[i].Y));
                }
                ctx.EndFigure(false);
            }
            context.DrawGeometry(null, trailPen, geometry);
        }

        // Worm.
        double wormRadius = Px(WormRadiusCells);
        Point wormCenter = At(snapshot.WormX, snapshot.WormY);
        context.DrawEllipse(
            new SolidColorBrush(Color.Parse("#ecece6")),
            null,
            wormCenter,
            wormRadius,
            wormRadius);

        // Temperature ring.
        var comfortTemp = ComfortTemperature;
        var dev = snapshot.BodyTemperature - comfortTemp;
        if (!double.IsNaN(comfortTemp) && Math.Abs(dev) > RingDeadband)
        {
            var ringColor = dev > 0 ? Color.Parse("#f08a4b") : Color.Parse("#4f8fd8");
            double ringRadius = Px(RingRadiusCells);
            context.DrawEllipse(
                null,
                new Pen(new SolidColorBrush(ringColor), Math.Max(1, Px(RingStrokeCells))),
                wormCenter,
                ringRadius,
                ringRadius);
        }
    }

    private static void DrawRadialGlow(DrawingContext context, Point center, double radius, Color color, double width, double height)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new RelativePoint(center, RelativeUnit.Absolute),
            Center = new RelativePoint(center, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(radius, RelativeUnit.Absolute),
            RadiusY = new RelativeScalar(radius, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(Color.FromArgb((byte)(GlowOpacity * 255), color.R, color.G, color.B), 0),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
            }
        };
        context.FillRectangle(brush, new Rect(0, 0, width, height));
    }

    #endregion
}

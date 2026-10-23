using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Globalization;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders energy and body temperature history.
/// </summary>
/// <remarks>
/// The control has no intrinsic size: it fills whatever its container allots and derives every
/// drawing coordinate from its own <see cref="Visual.Bounds"/> each render. A fixed-height title
/// band is reserved at the top so the caption never overlays the plotted data.
/// </remarks>
public sealed class HistoryPlotControl : Control
{
    #region Constants

    /// <summary>Half-height of the comfort band, in body-temperature units.</summary>
    private const double ComfortBandHalfWidth = 0.12;

    /// <summary>Height of the reserved title band, in pixels.</summary>
    private const double TitleBandHeight = 16.0;

    /// <summary>Inner padding around the plotted area, in pixels.</summary>
    private const double Padding = 6.0;

    /// <summary>Font size of the title caption.</summary>
    private const double TitleFontSize = 10.0;

    /// <summary>Default plot background brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultBackgroundBrush = new SolidColorBrush(Color.Parse("#1f1f1c"));

    /// <summary>Default text brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultTextBrush = new SolidColorBrush(Color.Parse("#9a9a92"));

    /// <summary>Default energy-line brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultEnergyBrush = new SolidColorBrush(Color.Parse("#4fb88a"));

    /// <summary>Default body-temperature-line brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultBodyTempBrush = new SolidColorBrush(Color.Parse("#f08a4b"));

    /// <summary>Default comfort-band brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultComfortBandBrush = new SolidColorBrush(Color.FromArgb(38, 240, 138, 75));

    /// <summary>
    /// Defines the <see cref="History"/> property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<(double Energy, double BodyTemp)>?> HistoryProperty =
        AvaloniaProperty.Register<HistoryPlotControl, IReadOnlyList<(double Energy, double BodyTemp)>?>(nameof(History));

    /// <summary>
    /// Defines the <see cref="ComfortTemperature"/> property.
    /// </summary>
    public static readonly StyledProperty<double> ComfortTemperatureProperty =
        AvaloniaProperty.Register<HistoryPlotControl, double>(nameof(ComfortTemperature), double.NaN);

    /// <summary>
    /// Defines the <see cref="BackgroundBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<HistoryPlotControl, IBrush?>(nameof(BackgroundBrush), DefaultBackgroundBrush);

    /// <summary>
    /// Defines the <see cref="TextBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<HistoryPlotControl, IBrush?>(nameof(TextBrush), DefaultTextBrush);

    /// <summary>
    /// Defines the <see cref="EnergyBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> EnergyBrushProperty =
        AvaloniaProperty.Register<HistoryPlotControl, IBrush?>(nameof(EnergyBrush), DefaultEnergyBrush);

    /// <summary>
    /// Defines the <see cref="BodyTempBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BodyTempBrushProperty =
        AvaloniaProperty.Register<HistoryPlotControl, IBrush?>(nameof(BodyTempBrush), DefaultBodyTempBrush);

    /// <summary>
    /// Defines the <see cref="ComfortBandBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> ComfortBandBrushProperty =
        AvaloniaProperty.Register<HistoryPlotControl, IBrush?>(nameof(ComfortBandBrush), DefaultComfortBandBrush);

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryPlotControl"/> class.
    /// </summary>
    public HistoryPlotControl()
    {
        MinHeight = 60;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the history data.
    /// </summary>
    public IReadOnlyList<(double Energy, double BodyTemp)>? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    /// <summary>
    /// Gets or sets the comfortable body temperature, used to draw the comfort band. When
    /// unset (<see cref="double.NaN"/>), the band is not drawn.
    /// </summary>
    public double ComfortTemperature
    {
        get => GetValue(ComfortTemperatureProperty);
        set => SetValue(ComfortTemperatureProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush filling the plot background.
    /// </summary>
    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of the title caption.
    /// </summary>
    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of the energy line.
    /// </summary>
    public IBrush? EnergyBrush
    {
        get => GetValue(EnergyBrushProperty);
        set => SetValue(EnergyBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of the body-temperature line.
    /// </summary>
    public IBrush? BodyTempBrush
    {
        get => GetValue(BodyTempBrushProperty);
        set => SetValue(BodyTempBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of the comfort band.
    /// </summary>
    public IBrush? ComfortBandBrush
    {
        get => GetValue(ComfortBandBrushProperty);
        set => SetValue(ComfortBandBrushProperty, value);
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HistoryProperty
            || change.Property == ComfortTemperatureProperty
            || change.Property == BackgroundBrushProperty
            || change.Property == TextBrushProperty
            || change.Property == EnergyBrushProperty
            || change.Property == BodyTempBrushProperty
            || change.Property == ComfortBandBrushProperty)
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
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (BackgroundBrush is { } background)
        {
            context.FillRectangle(background, new Rect(0, 0, width, height));
        }

        if (TextBrush is { } textBrush)
        {
            context.DrawText(
                new FormattedText("energy (green) · body temp (orange)", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, TitleFontSize, textBrush),
                new Point(Padding, 2));
        }

        // Plot area sits below the title band and inside the padding.
        double plotTop = TitleBandHeight + Padding;
        double plotBottom = height - Padding;
        double plotLeft = Padding;
        double plotRight = width - Padding;
        double plotWidth = plotRight - plotLeft;
        double plotHeight = plotBottom - plotTop;
        if (plotWidth <= 0 || plotHeight <= 0)
        {
            return;
        }

        var comfortTemp = ComfortTemperature;
        if (!double.IsNaN(comfortTemp) && ComfortBandBrush is { } bandBrush)
        {
            double bandTop = plotBottom - (comfortTemp + ComfortBandHalfWidth) * plotHeight;
            double bandHeight = ComfortBandHalfWidth * 2 * plotHeight;
            context.FillRectangle(bandBrush, new Rect(plotLeft, bandTop, plotWidth, bandHeight));
        }

        var history = History;
        if (history != null && history.Count > 0)
        {
            if (EnergyBrush is { } energyBrush)
            {
                DrawLine(context, history, h => h.Energy, energyBrush, plotLeft, plotWidth, plotBottom, plotHeight);
            }

            if (BodyTempBrush is { } bodyTempBrush)
            {
                DrawLine(context, history, h => h.BodyTemp, bodyTempBrush, plotLeft, plotWidth, plotBottom, plotHeight);
            }
        }
    }

    private static void DrawLine(
        DrawingContext context,
        IReadOnlyList<(double Energy, double BodyTemp)> history,
        Func<(double Energy, double BodyTemp), double> selector,
        IBrush brush,
        double plotLeft,
        double plotWidth,
        double plotBottom,
        double plotHeight)
    {
        if (history.Count == 0)
        {
            return;
        }

        double denominator = Math.Max(history.Count - 1, 1);
        var pen = new Pen(brush, 2);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < history.Count; i++)
            {
                var value = selector(history[i]);
                double x = plotLeft + (i / denominator) * plotWidth;
                double y = plotBottom - value * plotHeight;
                if (i == 0)
                {
                    ctx.BeginFigure(new Point(x, y), false);
                }
                else
                {
                    ctx.LineTo(new Point(x, y));
                }
            }
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }

    #endregion
}

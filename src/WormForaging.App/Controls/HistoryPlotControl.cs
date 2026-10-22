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

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HistoryProperty || change.Property == ComfortTemperatureProperty)
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

        context.FillRectangle(new SolidColorBrush(Color.Parse("#1f1f1c")), new Rect(0, 0, width, height));

        var textBrush = new SolidColorBrush(Color.Parse("#9a9a92"));
        context.DrawText(
            new FormattedText("energy (green) · body temp (orange)", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, TitleFontSize, textBrush),
            new Point(Padding, 2));

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
        if (!double.IsNaN(comfortTemp))
        {
            double bandTop = plotBottom - (comfortTemp + ComfortBandHalfWidth) * plotHeight;
            double bandHeight = ComfortBandHalfWidth * 2 * plotHeight;
            var bandBrush = new SolidColorBrush(Color.FromArgb(38, 240, 138, 75));
            context.FillRectangle(bandBrush, new Rect(plotLeft, bandTop, plotWidth, bandHeight));
        }

        var history = History;
        if (history != null && history.Count > 0)
        {
            DrawLine(context, history, h => h.Energy, Color.Parse("#4fb88a"), plotLeft, plotWidth, plotBottom, plotHeight);
            DrawLine(context, history, h => h.BodyTemp, Color.Parse("#f08a4b"), plotLeft, plotWidth, plotBottom, plotHeight);
        }
    }

    private static void DrawLine(
        DrawingContext context,
        IReadOnlyList<(double Energy, double BodyTemp)> history,
        Func<(double Energy, double BodyTemp), double> selector,
        Color color,
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
        var pen = new Pen(new SolidColorBrush(color), 2);
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

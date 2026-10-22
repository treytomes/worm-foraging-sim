using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders energy and body temperature history.
/// </summary>
public sealed class HistoryPlotControl : Control
{
    #region Constants

    /// <summary>Half-height of the comfort band, in body-temperature units.</summary>
    private const double ComfortBandHalfWidth = 0.12;

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
        Width = 360;
        Height = 100;
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
        Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);

        context.FillRectangle(new SolidColorBrush(Color.Parse("#1f1f1c")), bounds);

        var comfortTemp = ComfortTemperature;
        if (!double.IsNaN(comfortTemp))
        {
            var bandBrush = new SolidColorBrush(Color.FromArgb(38, 240, 138, 75));
            context.FillRectangle(
                bandBrush,
                new Rect(
                    0,
                    90 - (comfortTemp + ComfortBandHalfWidth) * 80,
                    360,
                    ComfortBandHalfWidth * 2 * 80));
        }

        var history = History;
        if (history != null && history.Count > 0)
        {
            DrawLine(context, history, h => h.Energy, Color.Parse("#4fb88a"));
            DrawLine(context, history, h => h.BodyTemp, Color.Parse("#f08a4b"));
        }

        var textBrush = new SolidColorBrush(Color.Parse("#9a9a92"));
        context.DrawText(new FormattedText("energy (green) · body temp (orange)", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, textBrush), new Point(6, 2));
    }

    private void DrawLine(DrawingContext context, IReadOnlyList<(double Energy, double BodyTemp)> history, Func<(double Energy, double BodyTemp), double> selector, Color color)
    {
        if (history == null || history.Count == 0)
        {
            return;
        }
        var pen = new Pen(new SolidColorBrush(color), 2);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < history.Count; i++)
            {
                var value = selector(history[i]);
                var x = i * 1.5;
                var y = 90 - value * 80;
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

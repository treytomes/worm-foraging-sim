using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WormForaging.Core;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders energy and body temperature history.
/// </summary>
public sealed class HistoryPlotControl : Control
{
    /// <summary>
    /// Defines the <see cref="History"/> property.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<(double Energy, double BodyTemp)>?> HistoryProperty =
        AvaloniaProperty.Register<HistoryPlotControl, IReadOnlyList<(double Energy, double BodyTemp)>?>(nameof(History));

    /// <summary>
    /// Gets or sets the history data.
    /// </summary>
    public IReadOnlyList<(double Energy, double BodyTemp)>? History
    {
        get => GetValue(HistoryProperty);
        set => SetValue(HistoryProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryPlotControl"/> class.
    /// </summary>
    public HistoryPlotControl()
    {
        Width = 360;
        Height = 100;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HistoryProperty)
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

        double comfortTemp = 0.55;
        var bandBrush = new SolidColorBrush(Color.FromArgb(38, 240, 138, 75));
        context.FillRectangle(bandBrush, new Rect(0, 90 - (comfortTemp + 0.12) * 80, 360, 0.24 * 80));

        IReadOnlyList<(double Energy, double BodyTemp)>? history = History;
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
        using (StreamGeometryContext ctx = geometry.Open())
        {
            for (int i = 0; i < history.Count; i++)
            {
                double value = selector(history[i]);
                double x = i * 1.5;
                double y = 90 - value * 80;
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
}

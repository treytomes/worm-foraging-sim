using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders steps per meal.
/// </summary>
public sealed class MealPlotControl : Control
{
    /// <summary>
    /// Defines the <see cref="MealIntervals"/> property.
    /// </summary>
    /// <remarks>
    /// Typed to match <see cref="WormForaging.Core.SimulationSnapshot.MealIntervals"/> exactly
    /// (<c>IReadOnlyList&lt;long&gt;</c>). A mismatched element type makes the binding silently
    /// yield null, which is what previously left this panel blank.
    /// </remarks>
    public static readonly StyledProperty<IReadOnlyList<long>?> MealIntervalsProperty =
        AvaloniaProperty.Register<MealPlotControl, IReadOnlyList<long>?>(nameof(MealIntervals));

    /// <summary>
    /// Gets or sets the steps-per-meal series.
    /// </summary>
    public IReadOnlyList<long>? MealIntervals
    {
        get => GetValue(MealIntervalsProperty);
        set => SetValue(MealIntervalsProperty, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MealPlotControl"/> class.
    /// </summary>
    public MealPlotControl()
    {
        Width = 360;
        Height = 90;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MealIntervalsProperty)
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

        IReadOnlyList<long>? meals = MealIntervals;
        if (meals == null || meals.Count == 0)
        {
            var textBrush = new SolidColorBrush(Color.Parse("#9a9a92"));
            context.DrawText(new FormattedText("steps per meal", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, textBrush), new Point(10, 2));
            return;
        }

        var recent = meals.TakeLast(40).ToList();
        double mxv = Math.Max(100.0, recent.Max());

        var pen = new Pen(new SolidColorBrush(Color.Parse("#4fb88a")), 2);
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            for (int i = 0; i < recent.Count; i++)
            {
                double x = 10 + i * (340.0 / Math.Max(recent.Count - 1, 1));
                double y = 80 - recent[i] / mxv * 65.0;
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

        var textBrush2 = new SolidColorBrush(Color.Parse("#9a9a92"));
        context.DrawText(new FormattedText($"steps per meal (max {mxv:F0})", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, textBrush2), new Point(10, 2));
    }
}

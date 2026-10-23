using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders steps per meal.
/// </summary>
/// <remarks>
/// The control has no intrinsic size: it fills its container and derives every drawing coordinate
/// from its own <see cref="Visual.Bounds"/> each render. A fixed-height title band is reserved at
/// the top so the caption never overlays the plotted series.
/// </remarks>
public sealed class MealPlotControl : Control
{
    #region Constants

    /// <summary>Height of the reserved title band, in pixels.</summary>
    private const double TitleBandHeight = 16.0;

    /// <summary>Inner padding around the plotted area, in pixels.</summary>
    private const double Padding = 6.0;

    /// <summary>Font size of the title caption.</summary>
    private const double TitleFontSize = 10.0;

    /// <summary>Maximum number of recent meals plotted.</summary>
    private const int RecentMealCount = 40;

    /// <summary>Floor for the y-axis maximum so a flat low series still renders.</summary>
    private const double MinimumYRange = 100.0;

    /// <summary>Default plot background brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultBackgroundBrush = new SolidColorBrush(Color.Parse("#1f1f1c"));

    /// <summary>Default text brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultTextBrush = new SolidColorBrush(Color.Parse("#9a9a92"));

    /// <summary>Default series-line brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultLineBrush = new SolidColorBrush(Color.Parse("#4fb88a"));

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
    /// Defines the <see cref="BackgroundBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<MealPlotControl, IBrush?>(nameof(BackgroundBrush), DefaultBackgroundBrush);

    /// <summary>
    /// Defines the <see cref="TextBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<MealPlotControl, IBrush?>(nameof(TextBrush), DefaultTextBrush);

    /// <summary>
    /// Defines the <see cref="LineBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<MealPlotControl, IBrush?>(nameof(LineBrush), DefaultLineBrush);

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="MealPlotControl"/> class.
    /// </summary>
    public MealPlotControl()
    {
        MinHeight = 60;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the steps-per-meal series.
    /// </summary>
    public IReadOnlyList<long>? MealIntervals
    {
        get => GetValue(MealIntervalsProperty);
        set => SetValue(MealIntervalsProperty, value);
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
    /// Gets or sets the brush of the series line.
    /// </summary>
    public IBrush? LineBrush
    {
        get => GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MealIntervalsProperty
            || change.Property == BackgroundBrushProperty
            || change.Property == TextBrushProperty
            || change.Property == LineBrushProperty)
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

        IReadOnlyList<long>? meals = MealIntervals;
        var recent = meals?.TakeLast(RecentMealCount).ToList();
        string title = recent is { Count: > 0 }
            ? $"steps per meal (max {Math.Max(MinimumYRange, recent.Max()):F0})"
            : "steps per meal";

        if (TextBrush is { } textBrush)
        {
            context.DrawText(
                new FormattedText(title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, TitleFontSize, textBrush),
                new Point(Padding, 2));
        }

        if (recent is not { Count: > 0 })
        {
            return;
        }

        double plotTop = TitleBandHeight + Padding;
        double plotBottom = height - Padding;
        double plotLeft = Padding;
        double plotWidth = width - Padding * 2;
        double plotHeight = plotBottom - plotTop;
        if (plotWidth <= 0 || plotHeight <= 0)
        {
            return;
        }

        if (LineBrush is not { } lineBrush)
        {
            return;
        }

        double maxValue = Math.Max(MinimumYRange, recent.Max());
        double denominator = Math.Max(recent.Count - 1, 1);

        var pen = new Pen(lineBrush, 2);
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            for (int i = 0; i < recent.Count; i++)
            {
                double x = plotLeft + (i / denominator) * plotWidth;
                double y = plotBottom - (recent[i] / maxValue) * plotHeight;
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

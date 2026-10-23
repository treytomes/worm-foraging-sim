using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders interneuron activations and readout weights.
/// </summary>
/// <remarks>
/// The control has no intrinsic size: it fills its container and derives every drawing coordinate
/// from its own <see cref="Visual.Bounds"/> each render, so the activation bars and the readout
/// weight heatmap scale with the panel. A fixed-height title band is reserved at the top.
/// </remarks>
public sealed class NetworkPlotControl : Control
{
    #region Constants

    /// <summary>Height of the reserved title band, in pixels.</summary>
    private const double TitleBandHeight = 16.0;

    /// <summary>Inner padding around the plotted area, in pixels.</summary>
    private const double Padding = 6.0;

    /// <summary>Horizontal gap between the activation panel and the weight heatmap, in pixels.</summary>
    private const double PanelGap = 24.0;

    /// <summary>Fraction of the plot width given to the activation bars (rest goes to the heatmap).</summary>
    private const double ActivationPanelFraction = 0.5;

    /// <summary>Font size of the title caption and axis labels.</summary>
    private const double LabelFontSize = 10.0;

    /// <summary>Number of motor readout rows and their N/E/S/W labels.</summary>
    private static readonly string[] MotorLabels = { "N", "E", "S", "W" };

    /// <summary>Default plot background brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultBackgroundBrush = new SolidColorBrush(Color.Parse("#1f1f1c"));

    /// <summary>Default text brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultTextBrush = new SolidColorBrush(Color.Parse("#9a9a92"));

    /// <summary>Default positive-value brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultPositiveBrush = new SolidColorBrush(Color.Parse("#4fb88a"));

    /// <summary>Default negative-value brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultNegativeBrush = new SolidColorBrush(Color.Parse("#f08a4b"));

    /// <summary>Default axis brush (overridden by the theme).</summary>
    private static readonly IBrush DefaultAxisBrush = new SolidColorBrush(Color.Parse("#9a9a92"));

    /// <summary>
    /// Defines the <see cref="Activations"/> property.
    /// </summary>
    public static readonly StyledProperty<double[]?> ActivationsProperty =
        AvaloniaProperty.Register<NetworkPlotControl, double[]?>(nameof(Activations));

    /// <summary>
    /// Defines the <see cref="ReadoutWeights"/> property.
    /// </summary>
    public static readonly StyledProperty<double[]?> ReadoutWeightsProperty =
        AvaloniaProperty.Register<NetworkPlotControl, double[]?>(nameof(ReadoutWeights));

    /// <summary>
    /// Defines the <see cref="BackgroundBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<NetworkPlotControl, IBrush?>(nameof(BackgroundBrush), DefaultBackgroundBrush);

    /// <summary>
    /// Defines the <see cref="TextBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<NetworkPlotControl, IBrush?>(nameof(TextBrush), DefaultTextBrush);

    /// <summary>
    /// Defines the <see cref="PositiveBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> PositiveBrushProperty =
        AvaloniaProperty.Register<NetworkPlotControl, IBrush?>(nameof(PositiveBrush), DefaultPositiveBrush);

    /// <summary>
    /// Defines the <see cref="NegativeBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> NegativeBrushProperty =
        AvaloniaProperty.Register<NetworkPlotControl, IBrush?>(nameof(NegativeBrush), DefaultNegativeBrush);

    /// <summary>
    /// Defines the <see cref="AxisBrush"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> AxisBrushProperty =
        AvaloniaProperty.Register<NetworkPlotControl, IBrush?>(nameof(AxisBrush), DefaultAxisBrush);

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="NetworkPlotControl"/> class.
    /// </summary>
    public NetworkPlotControl()
    {
        MinHeight = 60;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the activations.
    /// </summary>
    public IReadOnlyList<double>? Activations
    {
        get => GetValue(ActivationsProperty);
        set => SetValue(ActivationsProperty, value);
    }

    /// <summary>
    /// Gets or sets the readout weights.
    /// </summary>
    public IReadOnlyList<double>? ReadoutWeights
    {
        get => GetValue(ReadoutWeightsProperty);
        set => SetValue(ReadoutWeightsProperty, value);
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
    /// Gets or sets the brush of the title caption and axis labels.
    /// </summary>
    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of positive activations and weights.
    /// </summary>
    public IBrush? PositiveBrush
    {
        get => GetValue(PositiveBrushProperty);
        set => SetValue(PositiveBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of negative activations and weights.
    /// </summary>
    public IBrush? NegativeBrush
    {
        get => GetValue(NegativeBrushProperty);
        set => SetValue(NegativeBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of the activation baseline.
    /// </summary>
    public IBrush? AxisBrush
    {
        get => GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActivationsProperty
            || change.Property == ReadoutWeightsProperty
            || change.Property == BackgroundBrushProperty
            || change.Property == TextBrushProperty
            || change.Property == PositiveBrushProperty
            || change.Property == NegativeBrushProperty
            || change.Property == AxisBrushProperty)
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

        var textBrush = TextBrush;
        if (textBrush is not null)
        {
            context.DrawText(
                new FormattedText("interneurons (bars) · readout weights (grid)", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush),
                new Point(Padding, 2));
        }

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

        double activationWidth = Math.Max(0, plotWidth * ActivationPanelFraction);
        double heatmapLeft = plotLeft + activationWidth + PanelGap;
        double heatmapWidth = Math.Max(0, plotRight - heatmapLeft);

        DrawActivations(context, plotLeft, activationWidth, plotTop, plotBottom, plotHeight);
        DrawWeights(context, heatmapLeft, heatmapWidth, plotTop, plotHeight, textBrush);
    }

    private void DrawActivations(DrawingContext context, double left, double panelWidth, double plotTop, double plotBottom, double plotHeight)
    {
        var activations = Activations;
        if (activations == null || activations.Count == 0 || panelWidth <= 0)
        {
            return;
        }

        int count = Math.Min(activations.Count, 16);
        double slotWidth = panelWidth / count;
        double barWidth = Math.Max(1, slotWidth * 0.8);
        double baseline = (plotTop + plotBottom) / 2;
        double halfHeight = plotHeight / 2;

        var positiveBrush = PositiveBrush;
        var negativeBrush = NegativeBrush;
        for (int i = 0; i < count; i++)
        {
            double v = activations[i];
            double magnitude = Math.Min(1.0, Math.Abs(v)) * halfHeight;
            double x = left + i * slotWidth + (slotWidth - barWidth) / 2;
            var brush = v >= 0 ? positiveBrush : negativeBrush;
            if (brush is null)
            {
                continue;
            }

            var rect = v >= 0
                ? new Rect(x, baseline - magnitude, barWidth, magnitude)
                : new Rect(x, baseline, barWidth, magnitude);
            context.FillRectangle(brush, rect);
        }

        if (AxisBrush is { } lineBrush)
        {
            context.FillRectangle(lineBrush, new Rect(left, baseline, panelWidth, 1));
        }
    }

    private void DrawWeights(DrawingContext context, double left, double panelWidth, double plotTop, double plotHeight, IBrush? textBrush)
    {
        var weights = ReadoutWeights;
        if (weights == null || panelWidth <= 0)
        {
            return;
        }

        const int columns = 16;
        int rows = MotorLabels.Length;
        double labelWidth = 16;
        double gridLeft = left + labelWidth;
        double gridWidth = panelWidth - labelWidth;
        if (gridWidth <= 0)
        {
            return;
        }

        double cellWidth = gridWidth / columns;
        double cellHeight = plotHeight / rows;

        // The heatmap is a single-hue ramp: each cell keeps the positive/negative brush's color and
        // scales its alpha by the weight's magnitude.
        Color positiveColor = ToColor(PositiveBrush, Color.Parse("#4fb88a"));
        Color negativeColor = ToColor(NegativeBrush, Color.Parse("#f08a4b"));

        for (int k = 0; k < rows; k++)
        {
            for (int j = 0; j < columns; j++)
            {
                int idx = k * columns + j;
                if (idx >= weights.Count)
                {
                    continue;
                }

                double w = weights[idx] / 2.0;
                double al = Math.Min(1.0, Math.Abs(w));
                Color color = w > 0 ? positiveColor : negativeColor;
                context.FillRectangle(
                    new SolidColorBrush(Color.FromArgb((byte)(al * 255), color.R, color.G, color.B)),
                    new Rect(gridLeft + j * cellWidth, plotTop + k * cellHeight, Math.Max(1, cellWidth - 1), Math.Max(1, cellHeight - 1)));
            }

            if (textBrush is null)
            {
                continue;
            }

            double labelY = plotTop + k * cellHeight + (cellHeight - LabelFontSize) / 2;
            context.DrawText(
                new FormattedText(MotorLabels[k], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush),
                new Point(left, labelY));
        }
    }

    /// <summary>
    /// The opaque color behind a brush (falls back when the brush is null or not a solid color).
    /// </summary>
    private static Color ToColor(IBrush? brush, Color fallback) =>
        brush is ISolidColorBrush solid ? solid.Color : fallback;

    #endregion
}

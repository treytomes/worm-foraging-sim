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

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActivationsProperty || change.Property == ReadoutWeightsProperty)
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
            new FormattedText("interneurons (bars) · readout weights (grid)", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush),
            new Point(Padding, 2));

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

        var positiveBrush = new SolidColorBrush(Color.Parse("#4fb88a"));
        var negativeBrush = new SolidColorBrush(Color.Parse("#f08a4b"));
        for (int i = 0; i < count; i++)
        {
            double v = activations[i];
            double magnitude = Math.Min(1.0, Math.Abs(v)) * halfHeight;
            double x = left + i * slotWidth + (slotWidth - barWidth) / 2;
            if (v >= 0)
            {
                context.FillRectangle(positiveBrush, new Rect(x, baseline - magnitude, barWidth, magnitude));
            }
            else
            {
                context.FillRectangle(negativeBrush, new Rect(x, baseline, barWidth, magnitude));
            }
        }

        var lineBrush = new SolidColorBrush(Color.Parse("#9a9a92"));
        context.FillRectangle(lineBrush, new Rect(left, baseline, panelWidth, 1));
    }

    private void DrawWeights(DrawingContext context, double left, double panelWidth, double plotTop, double plotHeight, IBrush textBrush)
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
                Color color = w > 0
                    ? Color.FromArgb((byte)(al * 255), 79, 184, 138)
                    : Color.FromArgb((byte)(al * 255), 240, 138, 75);
                context.FillRectangle(
                    new SolidColorBrush(color),
                    new Rect(gridLeft + j * cellWidth, plotTop + k * cellHeight, Math.Max(1, cellWidth - 1), Math.Max(1, cellHeight - 1)));
            }

            double labelY = plotTop + k * cellHeight + (cellHeight - LabelFontSize) / 2;
            context.DrawText(
                new FormattedText(MotorLabels[k], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush),
                new Point(left, labelY));
        }
    }

    #endregion
}

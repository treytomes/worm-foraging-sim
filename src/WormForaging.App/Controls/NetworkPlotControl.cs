using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WormForaging.Core;

namespace WormForaging.App.Controls;

/// <summary>
/// Custom control that renders interneuron activations and readout weights.
/// </summary>
public sealed class NetworkPlotControl : Control
{
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
    /// Initializes a new instance of the <see cref="NetworkPlotControl"/> class.
    /// </summary>
    public NetworkPlotControl()
    {
        Width = 360;
        Height = 110;
    }

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
        Rect bounds = new(0, 0, Bounds.Width, Bounds.Height);

        context.FillRectangle(new SolidColorBrush(Color.Parse("#1f1f1c")), bounds);

        var activations = Activations;
        if (activations != null)
        {
            for (int i = 0; i < activations.Count && i < 16; i++)
            {
                double v = activations[i];
                if (v >= 0)
                {
                    context.FillRectangle(
                        new SolidColorBrush(Color.Parse("#4fb88a")),
                        new Rect(i * 11 + 4, 55 - Math.Abs(v) * 45, 9, Math.Abs(v) * 45));
                }
                else
                {
                    context.FillRectangle(
                        new SolidColorBrush(Color.Parse("#f08a4b")),
                        new Rect(i * 11 + 4, 55, 9, Math.Abs(v) * 45));
                }
            }
        }

        var lineBrush = new SolidColorBrush(Color.Parse("#9a9a92"));
        context.FillRectangle(lineBrush, new Rect(0, 55, 180, 1));

        var weights = ReadoutWeights;
        if (weights != null)
        {
            for (int k = 0; k < 4; k++)
            {
                for (int j = 0; j < 16; j++)
                {
                    int idx = k * 16 + j;
                    if (idx >= weights.Count)
                    {
                        continue;
                    }
                    double w = weights[idx] / 2.0;
                    double al = Math.Min(1.0, Math.Abs(w));
                    Color color = w > 0 ? Color.FromArgb((byte)(al * 255), 79, 184, 138) : Color.FromArgb((byte)(al * 255), 240, 138, 75);
                    context.FillRectangle(new SolidColorBrush(color), new Rect(194 + j * 10, 10 + k * 24, 9, 22));
                }
            }
        }

        var textBrush = new SolidColorBrush(Color.Parse("#9a9a92"));
        string[] labels = { "N", "E", "S", "W" };
        for (int k = 0; k < labels.Length; k++)
        {
            context.DrawText(new FormattedText(labels[k], System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, textBrush), new Point(182, 17 + k * 24));
        }
    }
}

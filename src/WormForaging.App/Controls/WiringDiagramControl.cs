using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using WormForaging.Core;

namespace WormForaging.App.Controls;

/// <summary>
/// A live node-link diagram of the recurrent network: sensor and drive inputs on the left, the
/// interneuron reservoir in the middle, and the four motors on the right. Interneuron nodes glow with
/// their live activation; the learned readout edges (<c>Wo</c>) are drawn boldly and update every
/// step, while the fixed reservoir wiring (sensor inputs, recurrence) is drawn faintly as context.
/// </summary>
/// <remarks>
/// <para>
/// This is the "option A" view: high information per pixel rather than a faithful full connectome.
/// Only the readout weights and the activations change over time, so those are the stars — you watch
/// the readout edges re-colour as the agent learns (and get overwritten on a sense-swap), and the
/// motor nodes light up toward the direction the current activity favours. The dense input wiring is
/// thresholded and dimmed so it frames the picture instead of drowning it.
/// </para>
/// <para>
/// Like the other panels, the control redraws only when one of its own styled properties changes
/// value. <see cref="Topology"/> is set once (it never changes for a seed); the per-tick redraw comes
/// from <see cref="Activations"/> and <see cref="ReadoutWeights"/>, which are fresh arrays off each
/// <see cref="SimulationSnapshot"/>.
/// </para>
/// </remarks>
public sealed class WiringDiagramControl : Control
{
    #region Constants

    /// <summary>Height of the reserved title band, in pixels.</summary>
    private const double TitleBandHeight = 16.0;

    /// <summary>Height of the reserved signal band (the neuromodulator LEDs) below the title.</summary>
    private const double SignalBandHeight = 20.0;

    /// <summary>Height of the reserved legend band at the bottom, in pixels.</summary>
    private const double LegendBandHeight = 15.0;

    /// <summary>Soft scale for LED brightness: the value at which an LED is ~76% of full brightness.</summary>
    private const double SignalBrightnessScale = 2.0;

    /// <summary>Inner padding around the drawn area, in pixels.</summary>
    private const double Padding = 8.0;

    /// <summary>Font size of the title caption and node labels.</summary>
    private const double LabelFontSize = 9.0;

    /// <summary>Number of drive inputs drawn below the sensors (hunger, thermal deviation).</summary>
    private const int DriveCount = 2;

    /// <summary>Fraction of a reservoir edge's magnitude (relative to the group max) below which it is not drawn.</summary>
    private const double ReservoirThreshold = 0.30;

    /// <summary>Fraction of a readout edge's magnitude below which it is not drawn.</summary>
    private const double ReadoutThreshold = 0.12;

    /// <summary>N/E/S/W motor labels.</summary>
    private static readonly string[] MotorLabels = { "N", "E", "S", "W" };

    #endregion

    #region Styled properties

    /// <summary>Defines the <see cref="Topology"/> property.</summary>
    public static readonly StyledProperty<NetworkTopology?> TopologyProperty =
        AvaloniaProperty.Register<WiringDiagramControl, NetworkTopology?>(nameof(Topology));

    /// <summary>Defines the <see cref="Activations"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ActivationsProperty =
        AvaloniaProperty.Register<WiringDiagramControl, IReadOnlyList<double>?>(nameof(Activations));

    /// <summary>Defines the <see cref="ReadoutWeights"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ReadoutWeightsProperty =
        AvaloniaProperty.Register<WiringDiagramControl, IReadOnlyList<double>?>(nameof(ReadoutWeights));

    /// <summary>Defines the <see cref="BackgroundBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<WiringDiagramControl, IBrush?>(nameof(BackgroundBrush), new SolidColorBrush(Color.Parse("#1f1f1c")));

    /// <summary>Defines the <see cref="TextBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<WiringDiagramControl, IBrush?>(nameof(TextBrush), new SolidColorBrush(Color.Parse("#9a9a92")));

    /// <summary>Defines the <see cref="PositiveBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> PositiveBrushProperty =
        AvaloniaProperty.Register<WiringDiagramControl, IBrush?>(nameof(PositiveBrush), new SolidColorBrush(Color.Parse("#4fb88a")));

    /// <summary>Defines the <see cref="NegativeBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> NegativeBrushProperty =
        AvaloniaProperty.Register<WiringDiagramControl, IBrush?>(nameof(NegativeBrush), new SolidColorBrush(Color.Parse("#f08a4b")));

    /// <summary>Defines the <see cref="Modulator"/> property.</summary>
    public static readonly StyledProperty<double> ModulatorProperty =
        AvaloniaProperty.Register<WiringDiagramControl, double>(nameof(Modulator));

    /// <summary>Defines the <see cref="FoodDrive"/> property.</summary>
    public static readonly StyledProperty<double> FoodDriveProperty =
        AvaloniaProperty.Register<WiringDiagramControl, double>(nameof(FoodDrive));

    /// <summary>Defines the <see cref="ThermalDrive"/> property.</summary>
    public static readonly StyledProperty<double> ThermalDriveProperty =
        AvaloniaProperty.Register<WiringDiagramControl, double>(nameof(ThermalDrive));

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance of the <see cref="WiringDiagramControl"/> class.</summary>
    public WiringDiagramControl()
    {
        MinHeight = 80;
    }

    #endregion

    #region Properties

    /// <summary>Gets or sets the fixed network wiring to draw (set once per simulation).</summary>
    public NetworkTopology? Topology
    {
        get => GetValue(TopologyProperty);
        set => SetValue(TopologyProperty, value);
    }

    /// <summary>Gets or sets the live interneuron activations (fills the middle nodes).</summary>
    public IReadOnlyList<double>? Activations
    {
        get => GetValue(ActivationsProperty);
        set => SetValue(ActivationsProperty, value);
    }

    /// <summary>Gets or sets the live readout weights (the bold, learning edges to the motors).</summary>
    public IReadOnlyList<double>? ReadoutWeights
    {
        get => GetValue(ReadoutWeightsProperty);
        set => SetValue(ReadoutWeightsProperty, value);
    }

    /// <summary>Gets or sets the brush filling the diagram background.</summary>
    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of the caption, labels, and node outlines.</summary>
    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    /// <summary>Gets or sets the brush for positive activations and weights.</summary>
    public IBrush? PositiveBrush
    {
        get => GetValue(PositiveBrushProperty);
        set => SetValue(PositiveBrushProperty, value);
    }

    /// <summary>Gets or sets the brush for negative activations and weights.</summary>
    public IBrush? NegativeBrush
    {
        get => GetValue(NegativeBrushProperty);
        set => SetValue(NegativeBrushProperty, value);
    }

    /// <summary>Gets or sets the live broadcast neuromodulator (the big "NT" LED).</summary>
    public double Modulator
    {
        get => GetValue(ModulatorProperty);
        set => SetValue(ModulatorProperty, value);
    }

    /// <summary>Gets or sets the live food (hunger) reward channel (an LED).</summary>
    public double FoodDrive
    {
        get => GetValue(FoodDriveProperty);
        set => SetValue(FoodDriveProperty, value);
    }

    /// <summary>Gets or sets the live thermal reward channel (an LED).</summary>
    public double ThermalDrive
    {
        get => GetValue(ThermalDriveProperty);
        set => SetValue(ThermalDriveProperty, value);
    }

    #endregion

    #region Methods

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TopologyProperty
            || change.Property == ActivationsProperty
            || change.Property == ReadoutWeightsProperty
            || change.Property == BackgroundBrushProperty
            || change.Property == TextBrushProperty
            || change.Property == PositiveBrushProperty
            || change.Property == NegativeBrushProperty
            || change.Property == ModulatorProperty
            || change.Property == FoodDriveProperty
            || change.Property == ThermalDriveProperty)
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
                new FormattedText(
                    "network · nodes glow with activity · gray = fixed reservoir · colored = learned readout",
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush),
                new Point(Padding, 2));
        }

        Color positive = ToColor(PositiveBrush, Color.Parse("#4fb88a"));
        Color negative = ToColor(NegativeBrush, Color.Parse("#f08a4b"));

        // The neuromodulator LEDs sit in a band just below the title; brightness = magnitude, colour =
        // sign (green reinforces, orange suppresses). Drawn whether or not the topology is present.
        DrawSignalBand(context, Padding, TitleBandHeight, width - 2 * Padding, textBrush, positive, negative);

        var topology = Topology;
        if (topology is null)
        {
            return;
        }

        double plotTop = TitleBandHeight + SignalBandHeight + Padding;
        double plotBottom = height - Padding - LegendBandHeight;
        double plotLeft = Padding;
        double plotRight = width - Padding;
        double plotWidth = plotRight - plotLeft;
        double plotHeight = plotBottom - plotTop;
        if (plotWidth <= 0 || plotHeight <= 0)
        {
            return;
        }

        Color reservoir = ToColor(TextBrush, Color.Parse("#9a9a92"));

        // ---- Node positions ----
        int inputCount = topology.SensorCount + DriveCount;
        var inputPts = ColumnPoints(plotLeft + plotWidth * 0.06, plotTop, plotBottom, inputCount);
        var neuronPts = GridPoints(plotLeft + plotWidth * 0.34, plotLeft + plotWidth * 0.66, plotTop, plotBottom, topology.NeuronCount);
        var motorPts = ColumnPoints(plotLeft + plotWidth * 0.94, plotTop, plotBottom, topology.MotorCount);

        double neuronRadius = Math.Clamp(Math.Min(plotWidth, plotHeight) * 0.035, 4.0, 9.0);
        double ioRadius = Math.Max(3.0, neuronRadius * 0.62);

        var activations = Activations;
        var readout = ReadoutWeights;

        // ---- Edges (drawn first, so nodes sit on top). The fixed reservoir is neutral gray; only
        // the learned readout carries the sign colors, so "colored" reads as "this is what learns". ----
        DrawInputEdges(context, topology, inputPts, neuronPts, reservoir);
        DrawRecurrentEdges(context, topology, neuronPts, reservoir);
        DrawReadoutEdges(context, topology, neuronPts, motorPts, readout, positive, negative);

        // ---- Nodes ----
        DrawInputNodes(context, topology, inputPts, ioRadius, textBrush, positive, negative);
        DrawNeuronNodes(context, neuronPts, neuronRadius, activations, textBrush, positive, negative);
        DrawMotorNodes(context, topology, motorPts, neuronRadius, activations, readout, textBrush, positive, negative);

        // ---- Legend ----
        DrawLegend(context, plotLeft, height - LegendBandHeight, textBrush, positive, negative, reservoir);
    }

    /// <summary>Evenly spaced points down a single vertical column.</summary>
    private static Point[] ColumnPoints(double x, double top, double bottom, int count)
    {
        var points = new Point[count];
        double usable = bottom - top;
        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? 0.5 : (i + 0.5) / count;
            points[i] = new Point(x, top + t * usable);
        }

        return points;
    }

    /// <summary>Points on a roughly square grid spanning the given x band and the full height.</summary>
    private static Point[] GridPoints(double left, double right, double top, double bottom, int count)
    {
        var points = new Point[count];
        int cols = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / cols);
        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            double tx = cols == 1 ? 0.5 : (col + 0.5) / cols;
            double ty = rows == 1 ? 0.5 : (row + 0.5) / rows;
            points[i] = new Point(left + tx * (right - left), top + ty * (bottom - top));
        }

        return points;
    }

    /// <summary>Weight of the edge from input node <paramref name="input"/> into neuron <paramref name="neuron"/>.</summary>
    private static double InputWeight(NetworkTopology t, int neuron, int input)
    {
        if (input < t.SensorCount)
        {
            return t.InputWeights[neuron * t.SensorCount + input];
        }

        return input == t.SensorCount ? t.HungerWeights[neuron] : t.ThermalWeights[neuron];
    }

    private void DrawInputEdges(DrawingContext context, NetworkTopology t, Point[] inputPts, Point[] neuronPts, Color reservoir)
    {
        double max = 0.0;
        for (int i = 0; i < neuronPts.Length; i++)
        {
            for (int l = 0; l < inputPts.Length; l++)
            {
                max = Math.Max(max, Math.Abs(InputWeight(t, i, l)));
            }
        }

        for (int i = 0; i < neuronPts.Length; i++)
        {
            for (int l = 0; l < inputPts.Length; l++)
            {
                DrawEdge(context, inputPts[l], neuronPts[i], InputWeight(t, i, l), max,
                    ReservoirThreshold, maxAlpha: 0.18, maxWidth: 1.2, reservoir, reservoir);
            }
        }
    }

    private void DrawRecurrentEdges(DrawingContext context, NetworkTopology t, Point[] neuronPts, Color reservoir)
    {
        int n = t.NeuronCount;
        double max = 0.0;
        for (int k = 0; k < t.RecurrentWeights.Count; k++)
        {
            max = Math.Max(max, Math.Abs(t.RecurrentWeights[k]));
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i == j)
                {
                    continue; // self-recurrence has nowhere to draw
                }

                DrawEdge(context, neuronPts[j], neuronPts[i], t.RecurrentWeights[i * n + j], max,
                    ReservoirThreshold, maxAlpha: 0.22, maxWidth: 1.4, reservoir, reservoir);
            }
        }
    }

    /// <summary>
    /// Draws the neuromodulator readout band: the big broadcast "mod" LED plus the two reward channels
    /// (food, thermal), each an LED whose brightness tracks the magnitude and whose colour tracks the
    /// sign, with the signed value beside it.
    /// </summary>
    private void DrawSignalBand(DrawingContext context, double left, double top, double bandWidth, IBrush? textBrush, Color positive, Color negative)
    {
        if (textBrush is null || bandWidth <= 0)
        {
            return;
        }

        double midY = top + SignalBandHeight / 2;
        double x = left;

        x = DrawLed(context, x, midY, "mod", Modulator, radius: 5.0, textBrush, positive, negative);
        x = DrawLed(context, x, midY, "food", FoodDrive, radius: 4.0, textBrush, positive, negative);
        DrawLed(context, x, midY, "therm", ThermalDrive, radius: 4.0, textBrush, positive, negative);
    }

    /// <summary>Draws one LED (label · glowing dot · signed value) and returns the x to continue from.</summary>
    private static double DrawLed(DrawingContext context, double x, double midY, string label, double value, double radius, IBrush textBrush, Color positive, Color negative)
    {
        var labelText = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush);
        context.DrawText(labelText, new Point(x, midY - LabelFontSize / 2 - 1));
        double cx = x + labelText.Width + 4 + radius;

        // Brightness saturates with magnitude so both tiny and large swings stay readable.
        double brightness = Math.Tanh(Math.Abs(value) / SignalBrightnessScale);
        Color c = value >= 0 ? positive : negative;
        var fill = new SolidColorBrush(Color.FromArgb((byte)(35 + brightness * 220), c.R, c.G, c.B));
        var ring = new Pen(new SolidColorBrush(Color.FromArgb(90, 150, 150, 140)), 1.0);
        context.DrawEllipse(fill, ring, new Point(cx, midY), radius, radius);

        var valueText = new FormattedText(
            value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture),
            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush);
        double valX = cx + radius + 3;
        context.DrawText(valueText, new Point(valX, midY - LabelFontSize / 2 - 1));
        return valX + valueText.Width + 12;
    }

    /// <summary>Draws the bottom legend: what the edge colours mean (fixed reservoir vs learned readout).</summary>
    private static void DrawLegend(DrawingContext context, double left, double y, IBrush? textBrush, Color positive, Color negative, Color reservoir)
    {
        if (textBrush is null)
        {
            return;
        }

        double cx = left;
        double midY = y + LegendBandHeight / 2;

        cx = LegendItem(context, cx, midY, reservoir, "fixed reservoir", textBrush);
        cx = LegendItem(context, cx, midY, positive, "readout +", textBrush);
        LegendItem(context, cx, midY, negative, "−", textBrush);
    }

    /// <summary>Draws one legend swatch (a short line) and its label, returning the x to continue from.</summary>
    private static double LegendItem(DrawingContext context, double x, double midY, Color color, string label, IBrush textBrush)
    {
        const double swatch = 14.0;
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(230, color.R, color.G, color.B)), 2.0);
        context.DrawLine(pen, new Point(x, midY), new Point(x + swatch, midY));

        var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush);
        context.DrawText(text, new Point(x + swatch + 3, midY - LabelFontSize / 2 - 1));
        return x + swatch + 3 + text.Width + 12;
    }

    private void DrawReadoutEdges(DrawingContext context, NetworkTopology t, Point[] neuronPts, Point[] motorPts, IReadOnlyList<double>? readout, Color positive, Color negative)
    {
        if (readout is null)
        {
            return;
        }

        int n = t.NeuronCount;
        double max = 0.0;
        for (int k = 0; k < readout.Count; k++)
        {
            max = Math.Max(max, Math.Abs(readout[k]));
        }

        for (int k = 0; k < motorPts.Length; k++)
        {
            for (int i = 0; i < n; i++)
            {
                int idx = k * n + i;
                if (idx >= readout.Count)
                {
                    continue;
                }

                DrawEdge(context, neuronPts[i], motorPts[k], readout[idx], max,
                    ReadoutThreshold, maxAlpha: 0.95, maxWidth: 3.2, positive, negative);
            }
        }
    }

    /// <summary>Draws one weighted edge, thresholded and scaled by magnitude; skips near-zero weights.</summary>
    private static void DrawEdge(DrawingContext context, Point a, Point b, double weight, double max, double threshold, double maxAlpha, double maxWidth, Color positive, Color negative)
    {
        if (max <= 0.0)
        {
            return;
        }

        double m = Math.Abs(weight) / max;
        if (m < threshold)
        {
            return;
        }

        Color baseColor = weight >= 0 ? positive : negative;
        byte alpha = (byte)(Math.Min(1.0, m) * maxAlpha * 255);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B)), 0.6 + (maxWidth - 0.6) * m);
        context.DrawLine(pen, a, b);
    }

    private void DrawInputNodes(DrawingContext context, NetworkTopology t, Point[] inputPts, double radius, IBrush? textBrush, Color positive, Color negative)
    {
        // Sensors are neutral dots; the two drive inputs are tinted and labelled, since they are the
        // internal needs (hunger, thermal) rather than external sensing.
        var sensorBrush = new SolidColorBrush(Color.FromArgb(150, 150, 150, 140));
        for (int l = 0; l < t.SensorCount; l++)
        {
            context.DrawEllipse(sensorBrush, null, inputPts[l], radius * 0.7, radius * 0.7);
        }

        if (t.SensorCount < inputPts.Length)
        {
            DrawLabelledNode(context, inputPts[t.SensorCount], radius, new SolidColorBrush(positive), "H", textBrush);
        }

        if (t.SensorCount + 1 < inputPts.Length)
        {
            DrawLabelledNode(context, inputPts[t.SensorCount + 1], radius, new SolidColorBrush(negative), "T", textBrush);
        }

        if (textBrush is not null && t.SensorCount > 0)
        {
            context.DrawText(
                new FormattedText("in", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush),
                new Point(inputPts[0].X - radius, inputPts[0].Y - radius - LabelFontSize - 1));
        }
    }

    private void DrawNeuronNodes(DrawingContext context, Point[] neuronPts, double radius, IReadOnlyList<double>? activations, IBrush? textBrush, Color positive, Color negative)
    {
        var outline = textBrush is null ? null : new Pen(new SolidColorBrush(Color.FromArgb(90, 150, 150, 140)), 1.0);
        for (int i = 0; i < neuronPts.Length; i++)
        {
            double a = activations is not null && i < activations.Count ? activations[i] : 0.0;
            double mag = Math.Min(1.0, Math.Abs(a));
            Color c = a >= 0 ? positive : negative;
            var fill = new SolidColorBrush(Color.FromArgb((byte)(40 + mag * 215), c.R, c.G, c.B));
            context.DrawEllipse(fill, outline, neuronPts[i], radius, radius);
        }
    }

    private void DrawMotorNodes(DrawingContext context, NetworkTopology t, Point[] motorPts, double radius, IReadOnlyList<double>? activations, IReadOnlyList<double>? readout, IBrush? textBrush, Color positive, Color negative)
    {
        // Each motor fills toward the sign/strength of its current drive: logit_k = sum_i Wo[k,i]·x_i,
        // so the direction the activity currently favours lights up.
        double[] logits = new double[motorPts.Length];
        double maxLogit = 0.0;
        if (activations is not null && readout is not null)
        {
            int n = t.NeuronCount;
            for (int k = 0; k < motorPts.Length; k++)
            {
                double sum = 0.0;
                for (int i = 0; i < n && i < activations.Count; i++)
                {
                    int idx = k * n + i;
                    if (idx < readout.Count)
                    {
                        sum += readout[idx] * activations[i];
                    }
                }

                logits[k] = sum;
                maxLogit = Math.Max(maxLogit, Math.Abs(sum));
            }
        }

        for (int k = 0; k < motorPts.Length; k++)
        {
            double mag = maxLogit > 0 ? Math.Min(1.0, Math.Abs(logits[k]) / maxLogit) : 0.0;
            Color c = logits[k] >= 0 ? positive : negative;
            var fill = new SolidColorBrush(Color.FromArgb((byte)(40 + mag * 215), c.R, c.G, c.B));
            var outline = textBrush is null ? null : new Pen(new SolidColorBrush(Color.FromArgb(150, 150, 150, 140)), 1.0);
            context.DrawEllipse(fill, outline, motorPts[k], radius * 1.15, radius * 1.15);

            if (textBrush is not null && k < MotorLabels.Length)
            {
                var label = new FormattedText(MotorLabels[k], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush);
                context.DrawText(label, new Point(motorPts[k].X + radius * 1.3, motorPts[k].Y - LabelFontSize / 2));
            }
        }
    }

    private static void DrawLabelledNode(DrawingContext context, Point p, double radius, IBrush fill, string label, IBrush? textBrush)
    {
        context.DrawEllipse(fill, null, p, radius * 0.85, radius * 0.85);
        if (textBrush is not null)
        {
            var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, textBrush);
            context.DrawText(text, new Point(p.X - radius - LabelFontSize * 0.7, p.Y - LabelFontSize / 2));
        }
    }

    /// <summary>The opaque color behind a brush (falls back when null or not a solid color).</summary>
    private static Color ToColor(IBrush? brush, Color fallback) =>
        brush is ISolidColorBrush solid ? solid.Color : fallback;

    #endregion
}

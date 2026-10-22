using System;
using System.Linq;
using WormForaging.Core;
using WormForaging.Core.Rng;
using Xunit;

namespace WormForaging.Core.Tests;

/// <summary>
/// Parity tests for <see cref="LearningRule"/> — eligibility, reward prediction, the modulator and
/// its modes, and the local readout update. Includes the project's hard-rule guard: the update is
/// local (only <c>e</c>, <c>mod</c>, <c>Wo</c>), never a gradient.
/// </summary>
public class LearningRuleTests
{
    private static readonly SimConfig Config = new();

    private static double[] Activations(ulong seed = 42UL)
    {
        var rng = new Pcg32Random(seed);
        var x = new double[Config.InterneuronCount];
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = Math.Tanh(rng.NextGaussian());
        }

        return x;
    }

    private static MotorDecision Decision(int choice, params double[] probabilities)
        => new(choice, probabilities);

    private static double Clip(double v, double m) => Math.Max(-m, Math.Min(m, v));

    // ---- Initial state ----

    [Fact]
    public void InitialState_MatchesPrototypeInit()
    {
        var rule = new LearningRule(Config);

        Assert.Equal(LearningMode.Full, rule.Mode);
        Assert.All(rule.EligibilityTrace, e => Assert.Equal(0.0, e));
        Assert.Equal(4 * 16, rule.EligibilityTrace.Count);
        Assert.Equal(0.0, rule.OdorBaseline);
        Assert.Equal(0.01, rule.OdorScale);
        Assert.Equal(0.0, rule.ThermalBaseline);
        Assert.Equal(0.002, rule.ThermalScale);
    }

    // ---- Eligibility trace ----

    [Fact]
    public void AccumulateEligibility_MatchesFormula_FromZero()
    {
        var rule = new LearningRule(Config);
        double[] x = Activations();
        var decision = Decision(1, 0.1, 0.4, 0.2, 0.3);

        rule.AccumulateEligibility(decision, x);

        for (int k = 0; k < Config.MotorCount; k++)
        {
            double y = (k == 1 ? 1.0 : 0.0) - decision.Probabilities[k];
            for (int j = 0; j < Config.InterneuronCount; j++)
            {
                Assert.Equal(y * x[j], rule.EligibilityTrace[k * Config.InterneuronCount + j], precision: 12);
            }
        }
    }

    [Fact]
    public void AccumulateEligibility_DecaysThenAccumulates()
    {
        var rule = new LearningRule(Config);
        double[] x = Activations();
        var decision = Decision(0, 0.4, 0.2, 0.2, 0.2);

        rule.AccumulateEligibility(decision, x);
        double[] afterFirst = rule.EligibilityTrace.ToArray();

        // A second call with zero activations contributes nothing, so the trace just decays by 0.7.
        rule.AccumulateEligibility(decision, new double[Config.InterneuronCount]);

        for (int i = 0; i < afterFirst.Length; i++)
        {
            Assert.Equal(0.7 * afterFirst[i], rule.EligibilityTrace[i], precision: 12);
        }
    }

    // ---- Reward prediction ----

    [Fact]
    public void UpdateRewardPrediction_MatchesFormula()
    {
        var rule = new LearningRule(Config);
        const double dc = 0.05, dt = -0.02;

        // Expected: baselines update first, then surprise uses the updated values.
        double bo = 0.0 + 0.01 * (dc - 0.0);
        double ro = 0.01 + 0.01 * (Math.Abs(dc - bo) - 0.01);
        double bt = 0.0 + 0.01 * (dt - 0.0);
        double rt = 0.002 + 0.01 * (Math.Abs(dt - bt) - 0.002);
        double expectedOdor = Clip((dc - bo) / (ro + 1e-4), 3);
        double expectedThermal = Clip((dt - bt) / (rt + 0.002), 3);

        (double odor, double thermal) = rule.UpdateRewardPrediction(dc, dt);

        Assert.Equal(bo, rule.OdorBaseline, precision: 12);
        Assert.Equal(ro, rule.OdorScale, precision: 12);
        Assert.Equal(bt, rule.ThermalBaseline, precision: 12);
        Assert.Equal(rt, rule.ThermalScale, precision: 12);
        Assert.Equal(expectedOdor, odor, precision: 12);
        Assert.Equal(expectedThermal, thermal, precision: 12);
    }

    [Fact]
    public void UpdateRewardPrediction_ClipsExtremeSurprise()
    {
        var rule = new LearningRule(Config);

        // A large positive odor change far exceeds the tiny initial scale -> clipped to +3.
        (double odor, _) = rule.UpdateRewardPrediction(odorChange: 100.0, discomfortReduction: -100.0);

        Assert.Equal(3.0, odor, precision: 12);
    }

    // ---- Modulator modes ----

    [Fact]
    public void Modulator_Full_IsTheCompetingNeedsReward()
    {
        var rule = new LearningRule(Config) { Mode = LearningMode.Full };
        const double rOd = 1.5, rTh = -0.5, hunger = 0.6, d1 = 0.1, ab = 0.01;

        double expected = (0.2 + hunger) * (rOd + ab * 50) + (0.2 + 4 * d1) * rTh;

        Assert.Equal(expected, rule.Modulator(rOd, rTh, hunger, d1, ab), precision: 12);
    }

    [Fact]
    public void Modulator_Hebbian_IsFixedConstantRegardlessOfArgs()
    {
        var rule = new LearningRule(Config) { Mode = LearningMode.Hebbian };

        Assert.Equal(0.6, rule.Modulator(99, -99, 1, 1, 1), precision: 12);
        Assert.Equal(0.6, rule.Modulator(0, 0, 0, 0, 0), precision: 12);
    }

    [Fact]
    public void Modulator_Frozen_IsZero()
    {
        var rule = new LearningRule(Config) { Mode = LearningMode.Frozen };

        Assert.Equal(0.0, rule.Modulator(99, -99, 1, 1, 1));
    }

    // ---- Readout update ----

    [Fact]
    public void ApplyReadoutUpdate_MatchesLocalFormula()
    {
        var rule = new LearningRule(Config) { Mode = LearningMode.Full };
        var motor = new MotorLayer(Config, new Pcg32Random(3));
        rule.AccumulateEligibility(Decision(2, 0.25, 0.25, 0.25, 0.25), Activations());

        double[] wBefore = motor.ReadoutWeights.ToArray();
        double[] e = rule.EligibilityTrace.ToArray();
        const double mod = 0.8;

        rule.ApplyReadoutUpdate(motor, mod);

        for (int i = 0; i < motor.ReadoutWeightCount; i++)
        {
            double expected = Clip(wBefore[i] + 0.04 * mod * e[i] - 1e-4 * wBefore[i], 4);
            Assert.Equal(expected, motor.GetReadoutWeight(i), precision: 12);
        }
    }

    [Fact]
    public void ApplyReadoutUpdate_IsPurelyLocal_NoCrossWeightCoupling()
    {
        // The hard rule: each weight's change depends only on its own e[i] and Wo[i] and the one
        // shared scalar. Zeroing all eligibility but one entry must change exactly one weight.
        var rule = new LearningRule(Config) { Mode = LearningMode.Full };
        var motor = new MotorLayer(Config, new Pcg32Random(8));

        // Make exactly one interneuron active, so eligibility is nonzero only in that neuron's
        // column (one entry per motor row whose policy term y != 0).
        var oneActive = new double[Config.InterneuronCount];
        oneActive[5] = 1.0;
        rule.AccumulateEligibility(Decision(0, 0.5, 0.5, 0.5, 0.5), oneActive);

        double[] e = rule.EligibilityTrace.ToArray();
        int nonzeroCount = e.Count(v => v != 0.0);

        double[] wBefore = motor.ReadoutWeights.ToArray();
        rule.ApplyReadoutUpdate(motor, modulator: 1.0);

        int changedByModulator = 0;
        for (int i = 0; i < wBefore.Length; i++)
        {
            double delta = motor.GetReadoutWeight(i) - wBefore[i];
            double decayOnly = -1e-4 * wBefore[i];
            if (Math.Abs(delta - decayOnly) > 1e-12)
            {
                changedByModulator++;
            }
        }

        // Exactly the entries with nonzero eligibility feel the modulated term; nothing else does.
        Assert.Equal(nonzeroCount, changedByModulator);
    }

    [Fact]
    public void ApplyReadoutUpdate_Frozen_LeavesWeightsExactlyUnchanged()
    {
        var rule = new LearningRule(Config) { Mode = LearningMode.Frozen };
        var motor = new MotorLayer(Config, new Pcg32Random(3));
        rule.AccumulateEligibility(Decision(2, 0.25, 0.25, 0.25, 0.25), Activations());
        double[] wBefore = motor.ReadoutWeights.ToArray();

        rule.ApplyReadoutUpdate(motor, modulator: 5.0); // large modulator must be ignored

        Assert.Equal(wBefore, motor.ReadoutWeights); // not even weight decay
    }

    [Fact]
    public void ApplyReadoutUpdate_ClampsWeights()
    {
        var rule = new LearningRule(Config) { Mode = LearningMode.Full };
        var motor = new MotorLayer(Config, new Pcg32Random(3));

        // Drive a huge positive update; every weight with positive eligibility should pin at +4,
        // negative at -4. Build strong eligibility and a big modulator, then iterate.
        rule.AccumulateEligibility(Decision(0, 0.0, 0.33, 0.33, 0.34), Enumerable.Repeat(1.0, Config.InterneuronCount).ToArray());
        for (int step = 0; step < 50; step++)
        {
            rule.ApplyReadoutUpdate(motor, modulator: 1000.0);
        }

        Assert.All(motor.ReadoutWeights, w => Assert.InRange(w, -4.0, 4.0));
        Assert.Contains(motor.ReadoutWeights, w => Math.Abs(Math.Abs(w) - 4.0) < 1e-9); // some pinned at the clamp
    }

    // ---- Determinism ----

    [Fact]
    public void SameInputs_ProduceIdenticalState()
    {
        var a = new LearningRule(Config);
        var b = new LearningRule(Config);
        double[] x = Activations();
        var decision = Decision(1, 0.2, 0.3, 0.25, 0.25);

        a.AccumulateEligibility(decision, x);
        b.AccumulateEligibility(decision, x);
        a.UpdateRewardPrediction(0.03, -0.01);
        b.UpdateRewardPrediction(0.03, -0.01);

        Assert.Equal(a.EligibilityTrace, b.EligibilityTrace);
        Assert.Equal(a.OdorBaseline, b.OdorBaseline);
        Assert.Equal(a.ThermalScale, b.ThermalScale);
    }

    // ---- Guards ----

    [Fact]
    public void AccumulateEligibility_WrongActivationLength_Throws()
        => Assert.Throws<ArgumentException>(
            () => new LearningRule(Config).AccumulateEligibility(Decision(0, 0.25, 0.25, 0.25, 0.25), new double[15]));

    [Fact]
    public void AccumulateEligibility_WrongProbabilityLength_Throws()
        => Assert.Throws<ArgumentException>(
            () => new LearningRule(Config).AccumulateEligibility(Decision(0, 0.5, 0.5), Activations()));

    [Fact]
    public void AccumulateEligibility_NullDecision_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new LearningRule(Config).AccumulateEligibility(null!, Activations()));

    [Fact]
    public void AccumulateEligibility_NullActivations_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new LearningRule(Config).AccumulateEligibility(Decision(0, 0.25, 0.25, 0.25, 0.25), null!));

    [Fact]
    public void ApplyReadoutUpdate_NullMotor_Throws()
        => Assert.Throws<ArgumentNullException>(
            () => new LearningRule(Config).ApplyReadoutUpdate(null!, 1.0));

    [Fact]
    public void Constructor_NullConfig_Throws()
        => Assert.Throws<ArgumentNullException>(() => new LearningRule(null!));
}

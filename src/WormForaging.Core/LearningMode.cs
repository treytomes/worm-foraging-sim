namespace WormForaging.Core;

/// <summary>
/// The three readout-learning modes, mirroring the prototype's mode dropdown. They differ only in
/// the broadcast modulator they feed the (otherwise identical) local update.
/// </summary>
public enum LearningMode
{
    /// <summary>
    /// Three-factor learning: the modulator is the neuromodulatory reward signal
    /// (the prototype's <c>full</c>). This is the mechanism under study.
    /// </summary>
    Full,

    /// <summary>
    /// Plain Hebbian: the modulator is a fixed positive constant, so the update is reward-blind
    /// correlation only (the prototype's <c>hebb</c>, a baseline to beat).
    /// </summary>
    Hebbian,

    /// <summary>
    /// Frozen: no readout update at all — not even weight decay (the prototype's <c>frozen</c>,
    /// the no-learning control).
    /// </summary>
    Frozen,
}

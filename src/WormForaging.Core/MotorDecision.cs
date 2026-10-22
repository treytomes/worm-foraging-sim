namespace WormForaging.Core;

/// <summary>
/// The outcome of one action selection: which direction the worm chose and the policy
/// probabilities it was drawn from.
/// </summary>
/// <param name="Choice">The chosen direction index (0–3, into <see cref="Directions.All"/>).</param>
/// <param name="Probabilities">
/// The normalized action probabilities this step (length <see cref="SimConfig.MotorCount"/>). The
/// learning rule reads these to form the policy-gradient eligibility term <c>(onehot − p)</c>.
/// </param>
public sealed record MotorDecision(int Choice, IReadOnlyList<double> Probabilities);

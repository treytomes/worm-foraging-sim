namespace WormForaging.Harness.Hosting;

/// <summary>
/// The resolved, validated experiment parameters — command line over configuration over defaults —
/// registered as a singleton so the runner and the logs read one authoritative copy.
/// </summary>
/// <param name="Seeds">Number of distinct seeds run per mode (at least 1).</param>
/// <param name="BaseSeed">The first seed; the run uses <c>BaseSeed .. BaseSeed+Seeds-1</c>.</param>
/// <param name="Steps">Number of simulation steps per run (at least 1).</param>
/// <param name="SampleInterval">Steps between time-series samples (at least 1).</param>
/// <param name="OutputDirectory">Absolute directory the CSV files are written to.</param>
internal sealed record ExperimentSettings(
    int Seeds,
    ulong BaseSeed,
    int Steps,
    int SampleInterval,
    string OutputDirectory)
{
    /// <summary>The seeds this experiment will run, in order.</summary>
    public IEnumerable<ulong> SeedSequence()
    {
        for (int i = 0; i < Seeds; i++)
        {
            yield return BaseSeed + (ulong)i;
        }
    }
}

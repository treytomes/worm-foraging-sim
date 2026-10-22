namespace WormForaging.App.Hosting;

/// <summary>
/// Identifies where the active RNG seed was resolved from, for logging and display.
/// </summary>
public enum SeedSource
{
    /// <summary>The seed was supplied on the command line (<c>--seed</c>).</summary>
    CommandLine,

    /// <summary>The seed was read from configuration (<c>Simulation:Seed</c> in appsettings.yaml).</summary>
    Configuration,

    /// <summary>No seed was supplied, so one was generated and logged for reproducibility.</summary>
    Random,
}

/// <summary>
/// The resolved simulation startup parameters: the seed that will drive every stochastic path this
/// run, and where it came from. Registered as a singleton so the UI and logs can report it.
/// </summary>
/// <param name="Seed">The seed handed to the deterministic generator.</param>
/// <param name="Source">Where <paramref name="Seed"/> was resolved from.</param>
public sealed record SimulationStartup(ulong Seed, SeedSource Source);

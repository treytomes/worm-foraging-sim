namespace WormForaging.Harness.Hosting;

/// <summary>
/// The resolved parameters for the Phase 2 swap-back / savings experiment.
/// </summary>
/// <remarks>
/// The run is: warm up under the identity mapping, then toggle the sensor-to-direction mapping every
/// <see cref="SegmentSteps"/> for <see cref="Swaps"/> cycles. Recovery after each swap is measured in
/// fixed epochs of <see cref="EpochSteps"/>; <see cref="RecoveryWindowSteps"/> is the early window
/// whose meal count is the headline "how fast did it bounce back" scalar. The step counts are all
/// multiples of <see cref="EpochSteps"/> so swap and window boundaries land on epoch boundaries.
/// </remarks>
/// <param name="Seeds">Number of seeds (at least 1).</param>
/// <param name="BaseSeed">First seed; the run uses <c>BaseSeed .. BaseSeed+Seeds-1</c>.</param>
/// <param name="WarmupSteps">Initial steps under the identity mapping before the first swap.</param>
/// <param name="SegmentSteps">Steps between consecutive swaps.</param>
/// <param name="Swaps">Number of swap events (cycles) after warm-up.</param>
/// <param name="RecoveryWindowSteps">Early post-swap window whose meal count is the recovery scalar.</param>
/// <param name="EpochSteps">Resolution of the recovery curve; also the stepping granularity.</param>
/// <param name="OutputDirectory">Absolute directory the CSV files are written to.</param>
internal sealed record SwapSettings(
    int Seeds,
    ulong BaseSeed,
    int WarmupSteps,
    int SegmentSteps,
    int Swaps,
    int RecoveryWindowSteps,
    int EpochSteps,
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

    /// <summary>Number of epochs in one segment.</summary>
    public int EpochsPerSegment => SegmentSteps / EpochSteps;

    /// <summary>Number of epochs in the early recovery window.</summary>
    public int WindowEpochs => RecoveryWindowSteps / EpochSteps;
}

namespace WormForaging.Harness.Experiment;

/// <summary>
/// Summary statistics for a set of values: the count of finite values that contributed, their mean,
/// and their sample standard deviation. Non-finite values (NaN/Infinity — e.g. a run that ate no
/// meal has no steps-per-meal) are ignored, and <see cref="Count"/> reports how many remained so a
/// reader can see when some seeds dropped out.
/// </summary>
/// <param name="Count">Number of finite values that contributed.</param>
/// <param name="Mean">Arithmetic mean of the finite values, or <see cref="double.NaN"/> if none.</param>
/// <param name="StdDev">
/// Sample standard deviation (Bessel's n-1), or 0 when fewer than two finite values contributed.
/// </param>
internal readonly record struct Stats(int Count, double Mean, double StdDev)
{
    /// <summary>
    /// Computes the mean and sample standard deviation over the finite values in <paramref name="values"/>.
    /// </summary>
    /// <param name="values">The values to summarize; non-finite entries are skipped.</param>
    /// <returns>The computed <see cref="Stats"/>.</returns>
    public static Stats Compute(IEnumerable<double> values)
    {
        var finite = values.Where(double.IsFinite).ToList();
        if (finite.Count == 0)
        {
            return new Stats(0, double.NaN, 0.0);
        }

        double mean = finite.Average();
        if (finite.Count < 2)
        {
            return new Stats(finite.Count, mean, 0.0);
        }

        double sumSq = finite.Sum(v => (v - mean) * (v - mean));
        double std = Math.Sqrt(sumSq / (finite.Count - 1));
        return new Stats(finite.Count, mean, std);
    }
}

namespace PortfolioManagement;

/// <summary>
/// The target share of the portfolio's total value that one stock should represent.
/// </summary>
/// <remarks>
/// <see cref="Weight"/> is a fraction between 0 and 1 (0.40 means 40%). Fractions avoid
/// mixing up 40 and 0.40 in the calculations. The allocation references a
/// <see cref="Stock"/> rather than a plain symbol because a rebalance may need to buy a
/// stock the portfolio does not hold yet, and buying requires that stock's price.
/// </remarks>
public sealed record AllocatedStock
{
    public Stock Stock { get; }
    public decimal Weight { get; }

    public AllocatedStock(Stock stock, decimal weight)
    {
        ArgumentNullException.ThrowIfNull(stock);

        if (weight is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight must be between 0 and 1.");

        Stock = stock;
        Weight = weight;
    }
}

namespace PortfolioManagement;

/// <summary>
/// A set of stock holdings plus the target allocation they should follow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rebalancing idea.</b> The current total value is redistributed according to the
/// target weights. For every stock:
/// <code>
///   targetValue = totalValue × weight
///   difference  = targetValue − currentValue
///   shares      = difference / price      (positive → buy, negative → sell)
/// </code>
/// No money comes in or goes out, so the value bought always equals the value sold.
/// </para>
/// <para>
/// <b>Design decisions.</b>
/// <list type="bullet">
///   <item><see cref="Rebalance"/> only <i>suggests</i> orders and never changes the
///   portfolio. Executing trades belongs to another layer (broker, settlement, etc.),
///   and a side-effect-free method is easier to test and safer to call.</item>
///   <item>Money and quantities use <c>decimal</c>, never <c>double</c>, to avoid binary
///   floating-point errors such as 0.1 + 0.2 ≠ 0.3.</item>
///   <item>Fractional shares are allowed, so results are not rounded. Rounding to a
///   broker's lot size would leave a cash remainder that would then need its own policy.</item>
///   <item>Cash is out of scope: the portfolio holds only stocks.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class Portfolio
{
    /// <summary>
    /// How far the weights may be from summing to exactly 1. This allows allocations
    /// such as 0.3333 / 0.3333 / 0.3334 that cannot be written exactly in decimal.
    /// </summary>
    public const decimal WeightSumTolerance = 0.0001m;

    private readonly IReadOnlyList<Position> _positions;
    private readonly IReadOnlyList<AllocatedStock> _allocation;

    public Portfolio(IEnumerable<Position> positions, IEnumerable<AllocatedStock> allocation)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(allocation);

        // Copy the inputs so the caller cannot change the portfolio afterwards.
        _positions = positions.ToList();
        _allocation = allocation.ToList();

        ValidatePositions(_positions);
        ValidateAllocation(_allocation);
        ValidateConsistentStocks(_positions, _allocation);
    }

    public IReadOnlyList<Position> Positions => _positions;
    public IReadOnlyList<AllocatedStock> Allocation => _allocation;

    /// <summary>Current market value of all holdings.</summary>
    public decimal TotalValue() => _positions.Sum(p => p.MarketValue());

    /// <summary>
    /// Computes the buy and sell orders that would bring the portfolio to its target
    /// allocation at current prices.
    /// </summary>
    /// <param name="minTradeAmount">
    /// Trades worth this amount or less are skipped. Real portfolios are almost never
    /// exactly balanced, and tiny trades cost more in fees than they fix. The default
    /// of 0 returns every non-zero trade.
    /// </param>
    /// <returns>
    /// Sells first, then buys, each group sorted by symbol. Sells come first because
    /// they free the cash the buys need. An empty list means nothing has to change.
    /// </returns>
    public IReadOnlyList<Order> Rebalance(decimal minTradeAmount = 0m)
    {
        if (minTradeAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(minTradeAmount), minTradeAmount, "Minimum trade amount cannot be negative.");

        // Read each price once. If the price feed updated during the loop, values computed
        // with different prices would not add up and the orders would not net to zero.
        var prices = AllStocks().ToDictionary(s => s.Symbol, s => s.CurrentPrice());

        var quantities = _positions.ToDictionary(p => p.Stock.Symbol, p => p.Quantity);
        var weights = _allocation.ToDictionary(a => a.Stock.Symbol, a => a.Weight);

        var totalValue = quantities.Sum(q => q.Value * prices[q.Key]);

        // With nothing invested there is nothing to redistribute. Investing new money is a
        // different operation (a deposit) and is out of scope.
        if (totalValue == 0)
            return [];

        var orders = new List<Order>();

        // Walk the union of held and allocated symbols:
        //  - held but not allocated → target weight 0 → sell everything;
        //  - allocated but not held → current quantity 0 → buy from zero.
        foreach (var symbol in prices.Keys)
        {
            var price = prices[symbol];
            var currentQuantity = quantities.GetValueOrDefault(symbol);
            var weight = weights.GetValueOrDefault(symbol);

            var currentValue = currentQuantity * price;
            var targetValue = totalValue * weight;
            var difference = targetValue - currentValue;

            if (Math.Abs(difference) <= minTradeAmount)
                continue;

            // When the target is zero, sell the exact quantity held instead of value / price.
            // Dividing can leave a tiny leftover such as 0.0000000001 shares.
            var shares = weight == 0
                ? currentQuantity
                : Math.Abs(difference) / price;

            var side = difference > 0 ? OrderSide.Buy : OrderSide.Sell;
            orders.Add(new Order(symbol, side, shares, price, Math.Abs(difference)));
        }

        return orders
            .OrderBy(o => o.Side == OrderSide.Sell ? 0 : 1)
            .ThenBy(o => o.Symbol, StringComparer.Ordinal)
            .ToList();
    }

    private IEnumerable<Stock> AllStocks() =>
        _positions.Select(p => p.Stock)
            .Concat(_allocation.Select(a => a.Stock))
            .DistinctBy(s => s.Symbol);

    private static void ValidatePositions(IReadOnlyList<Position> positions)
    {
        if (positions.Any(p => p is null))
            throw new ArgumentException("Positions cannot contain null entries.", nameof(positions));

        // Two positions for the same stock make it unclear which quantity is real.
        var duplicate = FirstDuplicate(positions.Select(p => p.Stock.Symbol));
        if (duplicate is not null)
            throw new ArgumentException($"Stock '{duplicate}' appears in more than one position.", nameof(positions));
    }

    private static void ValidateAllocation(IReadOnlyList<AllocatedStock> allocation)
    {
        if (allocation.Count == 0)
            throw new ArgumentException("Allocation cannot be empty.", nameof(allocation));

        if (allocation.Any(a => a is null))
            throw new ArgumentException("Allocation cannot contain null entries.", nameof(allocation));

        var duplicate = FirstDuplicate(allocation.Select(a => a.Stock.Symbol));
        if (duplicate is not null)
            throw new ArgumentException($"Stock '{duplicate}' appears more than once in the allocation.", nameof(allocation));

        // A wrong sum is rejected rather than normalized. Silently scaling 40% / 50% up to
        // 100% would hide a configuration mistake in an investment decision.
        var sum = allocation.Sum(a => a.Weight);
        if (Math.Abs(sum - 1m) > WeightSumTolerance)
            throw new ArgumentException($"Allocation weights must add up to 1 (100%), but they add up to {sum}.", nameof(allocation));
    }

    /// <summary>
    /// A symbol must refer to the same <see cref="Stock"/> object everywhere; otherwise a
    /// rebalance could value one stock at two different prices.
    /// </summary>
    private static void ValidateConsistentStocks(IReadOnlyList<Position> positions, IReadOnlyList<AllocatedStock> allocation)
    {
        var stocksBySymbol = positions.ToDictionary(p => p.Stock.Symbol, p => p.Stock);

        foreach (var allocated in allocation)
        {
            if (stocksBySymbol.TryGetValue(allocated.Stock.Symbol, out var held) && !ReferenceEquals(held, allocated.Stock))
                throw new ArgumentException(
                    $"Stock '{allocated.Stock.Symbol}' refers to different Stock instances in positions and allocation.",
                    nameof(allocation));
        }
    }

    private static string? FirstDuplicate(IEnumerable<string> symbols) =>
        symbols.GroupBy(s => s).FirstOrDefault(g => g.Count() > 1)?.Key;
}

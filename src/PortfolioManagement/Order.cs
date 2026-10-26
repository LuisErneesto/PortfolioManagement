namespace PortfolioManagement;

public enum OrderSide
{
    Buy,
    Sell,
}

/// <summary>
/// One trade suggested by <see cref="Portfolio.Rebalance"/>.
/// </summary>
/// <param name="Symbol">Ticker of the stock to trade.</param>
/// <param name="Side">Whether to buy or sell.</param>
/// <param name="Shares">Number of shares to trade. Always positive; the direction is given by <paramref name="Side"/>.</param>
/// <param name="Price">Price used for the calculation (snapshot taken at rebalance time).</param>
/// <param name="Amount">Money value of the trade (<c>Shares × Price</c>). Always positive.</param>
public sealed record Order(string Symbol, OrderSide Side, decimal Shares, decimal Price, decimal Amount);

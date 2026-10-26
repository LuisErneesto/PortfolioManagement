namespace PortfolioManagement;

/// <summary>
/// A tradable stock identified by its ticker symbol.
/// </summary>
/// <remarks>
/// The exercise only says that a stock "has a Current Price method that returns the
/// last available price". In a real app that price would come from a market-data feed;
/// here it is kept in memory and refreshed through <see cref="UpdatePrice"/>, which keeps
/// the model simple and deterministic for tests.
/// </remarks>
public sealed class Stock
{
    private decimal _currentPrice;

    public string Symbol { get; }

    public Stock(string symbol, decimal currentPrice)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));

        // Normalize so "aapl" and "AAPL " are treated as the same stock.
        Symbol = symbol.Trim().ToUpperInvariant();
        UpdatePrice(currentPrice);
    }

    /// <summary>Returns the last available price for this stock.</summary>
    public decimal CurrentPrice() => _currentPrice;

    /// <summary>Records a new market price.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The price is zero or negative. A rebalance divides by the price to turn money into
    /// shares, so a non-positive price is never valid.
    /// </exception>
    public void UpdatePrice(decimal price)
    {
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), price, "Price must be greater than zero.");

        _currentPrice = price;
    }

    public override string ToString() => $"{Symbol} @ {_currentPrice}";
}

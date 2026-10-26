namespace PortfolioManagement;

/// <summary>
/// How many shares of a stock the portfolio currently holds.
/// </summary>
/// <remarks>
/// A <see cref="Stock"/> alone says nothing about ownership, so holdings are modeled as
/// (stock, quantity) pairs. Quantity is a <c>decimal</c> because fractional shares are
/// supported, as in many retail investment apps.
/// </remarks>
public sealed record Position
{
    public Stock Stock { get; }
    public decimal Quantity { get; }

    public Position(Stock stock, decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(stock);

        // Short positions are out of scope for this module.
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity cannot be negative.");

        Stock = stock;
        Quantity = quantity;
    }

    public decimal MarketValue() => Quantity * Stock.CurrentPrice();
}

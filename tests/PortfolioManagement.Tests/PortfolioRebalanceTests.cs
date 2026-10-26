namespace PortfolioManagement.Tests;

public class PortfolioRebalanceTests
{
    private readonly Stock _meta = new("META", 500m);
    private readonly Stock _aapl = new("AAPL", 200m);
    private readonly Stock _tsla = new("TSLA", 250m);

    [Fact]
    public void Rebalance_SellsOverweightAndBuysUnderweight()
    {
        // 10 META ($5,000) + 10 AAPL ($2,000) = $7,000.
        // Target 40/60 → META $2,800 (sell $2,200), AAPL $4,200 (buy $2,200).
        var portfolio = new Portfolio(
            [new Position(_meta, 10), new Position(_aapl, 10)],
            [new AllocatedStock(_meta, 0.40m), new AllocatedStock(_aapl, 0.60m)]);

        var orders = portfolio.Rebalance();

        Assert.Equal(
            [
                new Order("META", OrderSide.Sell, 4.4m, 500m, 2200m),
                new Order("AAPL", OrderSide.Buy, 11m, 200m, 2200m),
            ],
            orders);
    }

    [Fact]
    public void Rebalance_AlreadyBalanced_ReturnsNoOrders()
    {
        // $2,000 META + $3,000 AAPL is already 40/60.
        var portfolio = new Portfolio(
            [new Position(_meta, 4), new Position(_aapl, 15)],
            [new AllocatedStock(_meta, 0.40m), new AllocatedStock(_aapl, 0.60m)]);

        Assert.Empty(portfolio.Rebalance());
    }

    [Fact]
    public void Rebalance_HeldStockNotInAllocation_IsSoldCompletely()
    {
        var portfolio = new Portfolio(
            [new Position(_meta, 2), new Position(_tsla, 3.7m)],
            [new AllocatedStock(_meta, 1m)]);

        var orders = portfolio.Rebalance();

        var tslaOrder = Assert.Single(orders, o => o.Symbol == "TSLA");
        Assert.Equal(OrderSide.Sell, tslaOrder.Side);
        Assert.Equal(3.7m, tslaOrder.Shares);
    }

    [Fact]
    public void Rebalance_AllocatedStockNotHeld_IsBoughtFromZero()
    {
        // $1,000 in META only; target 50/50 with TSLA.
        var portfolio = new Portfolio(
            [new Position(_meta, 2)],
            [new AllocatedStock(_meta, 0.5m), new AllocatedStock(_tsla, 0.5m)]);

        var orders = portfolio.Rebalance();

        Assert.Equal(
            [
                new Order("META", OrderSide.Sell, 1m, 500m, 500m),
                new Order("TSLA", OrderSide.Buy, 2m, 250m, 500m),
            ],
            orders);
    }

    [Fact]
    public void Rebalance_BoughtValueEqualsSoldValue()
    {
        var portfolio = new Portfolio(
            [new Position(_meta, 3.3m), new Position(_aapl, 7.1m), new Position(_tsla, 1.9m)],
            [new AllocatedStock(_meta, 0.3333m), new AllocatedStock(_aapl, 0.3333m), new AllocatedStock(_tsla, 0.3334m)]);

        var orders = portfolio.Rebalance();

        var bought = orders.Where(o => o.Side == OrderSide.Buy).Sum(o => o.Amount);
        var sold = orders.Where(o => o.Side == OrderSide.Sell).Sum(o => o.Amount);
        Assert.Equal(sold, bought);
    }

    [Fact]
    public void Rebalance_ListsSellsBeforeBuys()
    {
        var portfolio = new Portfolio(
            [new Position(_aapl, 10), new Position(_tsla, 10)],
            [new AllocatedStock(_meta, 0.5m), new AllocatedStock(_aapl, 0.5m)]);

        var sides = portfolio.Rebalance().Select(o => o.Side).ToList();

        Assert.Equal([OrderSide.Sell, OrderSide.Buy], sides.Distinct());
    }

    [Fact]
    public void Rebalance_SkipsTradesAtOrBelowMinimumAmount()
    {
        // $5,050 META + $4,950 AAPL vs 50/50 → only $50 off each side.
        var portfolio = new Portfolio(
            [new Position(_meta, 10.1m), new Position(_aapl, 24.75m)],
            [new AllocatedStock(_meta, 0.5m), new AllocatedStock(_aapl, 0.5m)]);

        Assert.Empty(portfolio.Rebalance(minTradeAmount: 50m));
        Assert.Equal(2, portfolio.Rebalance(minTradeAmount: 49.99m).Count);
    }

    [Fact]
    public void Rebalance_UsesLatestPrices()
    {
        var portfolio = new Portfolio(
            [new Position(_meta, 4), new Position(_aapl, 15)],
            [new AllocatedStock(_meta, 0.40m), new AllocatedStock(_aapl, 0.60m)]);
        Assert.Empty(portfolio.Rebalance());

        // META doubles: $4,000 META + $3,000 AAPL is now overweight in META.
        _meta.UpdatePrice(1000m);

        var orders = portfolio.Rebalance();

        Assert.Equal(OrderSide.Sell, orders.Single(o => o.Symbol == "META").Side);
        Assert.Equal(OrderSide.Buy, orders.Single(o => o.Symbol == "AAPL").Side);
    }

    [Fact]
    public void Rebalance_DoesNotModifyPortfolio()
    {
        var portfolio = new Portfolio(
            [new Position(_meta, 10), new Position(_aapl, 10)],
            [new AllocatedStock(_meta, 0.40m), new AllocatedStock(_aapl, 0.60m)]);

        portfolio.Rebalance();

        Assert.Equal([10m, 10m], portfolio.Positions.Select(p => p.Quantity));
    }

    [Fact]
    public void Rebalance_EmptyPortfolio_ReturnsNoOrders()
    {
        var portfolio = new Portfolio([], [new AllocatedStock(_meta, 1m)]);

        Assert.Empty(portfolio.Rebalance());
    }

    [Fact]
    public void Rebalance_ZeroQuantityPositions_ReturnsNoOrders()
    {
        var portfolio = new Portfolio([new Position(_aapl, 0)], [new AllocatedStock(_meta, 1m)]);

        Assert.Empty(portfolio.Rebalance());
    }

    [Fact]
    public void Rebalance_NegativeMinimumAmount_Throws()
    {
        var portfolio = new Portfolio([new Position(_meta, 1)], [new AllocatedStock(_meta, 1m)]);

        Assert.Throws<ArgumentOutOfRangeException>(() => portfolio.Rebalance(-1m));
    }
}

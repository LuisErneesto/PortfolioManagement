namespace PortfolioManagement.Tests;

public class ValidationTests
{
    private readonly Stock _meta = new("META", 500m);
    private readonly Stock _aapl = new("AAPL", 200m);

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Stock_NonPositivePrice_Throws(decimal price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Stock("META", price));
        Assert.Throws<ArgumentOutOfRangeException>(() => _meta.UpdatePrice(price));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Stock_BlankSymbol_Throws(string symbol)
    {
        Assert.Throws<ArgumentException>(() => new Stock(symbol, 1m));
    }

    [Fact]
    public void Stock_SymbolIsNormalized()
    {
        Assert.Equal("AAPL", new Stock(" aapl ", 1m).Symbol);
    }

    [Fact]
    public void Position_NegativeQuantity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Position(_meta, -1));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void AllocatedStock_WeightOutOfRange_Throws(decimal weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AllocatedStock(_meta, weight));
    }

    [Theory]
    [InlineData(0.4, 0.5)]  // 90%
    [InlineData(0.6, 0.6)]  // 120%
    public void Portfolio_WeightsNotSummingToOne_Throws(decimal metaWeight, decimal aaplWeight)
    {
        Assert.Throws<ArgumentException>(() => new Portfolio(
            [new Position(_meta, 1)],
            [new AllocatedStock(_meta, metaWeight), new AllocatedStock(_aapl, aaplWeight)]));
    }

    [Fact]
    public void Portfolio_WeightsWithinTolerance_AreAccepted()
    {
        var portfolio = new Portfolio(
            [new Position(_meta, 1)],
            [new AllocatedStock(_meta, 0.3333m), new AllocatedStock(_aapl, 0.6666m)]);

        Assert.NotNull(portfolio);
    }

    [Fact]
    public void Portfolio_EmptyAllocation_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Portfolio([new Position(_meta, 1)], []));
    }

    [Fact]
    public void Portfolio_DuplicateAllocation_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Portfolio(
            [],
            [new AllocatedStock(_meta, 0.5m), new AllocatedStock(_meta, 0.5m)]));
    }

    [Fact]
    public void Portfolio_DuplicatePosition_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Portfolio(
            [new Position(_meta, 1), new Position(_meta, 2)],
            [new AllocatedStock(_meta, 1m)]));
    }

    [Fact]
    public void Portfolio_SameSymbolWithDifferentStockInstances_Throws()
    {
        var otherMeta = new Stock("META", 999m);

        Assert.Throws<ArgumentException>(() => new Portfolio(
            [new Position(_meta, 1)],
            [new AllocatedStock(otherMeta, 1m)]));
    }
}

using LocalCrypto.Core;

namespace LocalCrypto.Tests;

public sealed class PortfolioCalculatorTests
{
    [Fact]
    public void BuyTransactionsBuildAverageCostIncludingQuoteFees()
    {
        var transactions = new[]
        {
            Transaction("1", TradeSide.Buy, "BTC", 1m, 100m, 2m),
            Transaction("2", TradeSide.Buy, "BTC", 1m, 200m, 4m)
        };

        var portfolio = PortfolioCalculator.Calculate(transactions);
        var position = Assert.Single(portfolio.Positions);

        Assert.Equal(2m, position.Quantity);
        Assert.Equal(306m, position.InvestedCost);
        Assert.Equal(153m, position.AverageCost);
        Assert.Equal(6m, portfolio.TotalFees);
    }

    [Fact]
    public void SellTransactionCalculatesRealizedPnlFromAverageCost()
    {
        var transactions = new[]
        {
            Transaction("1", TradeSide.Buy, "ETH", 2m, 100m, 0m),
            Transaction("2", TradeSide.Sell, "ETH", 1m, 150m, 3m)
        };

        var portfolio = PortfolioCalculator.Calculate(transactions);
        var position = Assert.Single(portfolio.Positions);

        Assert.Equal(1m, position.Quantity);
        Assert.Equal(100m, position.InvestedCost);
        Assert.Equal(47m, portfolio.RealizedPnlTotal);
        Assert.Equal(3m, portfolio.TotalFees);
    }

    [Fact]
    public void OversellDoesNotMakeNegativePosition()
    {
        var transactions = new[]
        {
            Transaction("1", TradeSide.Buy, "SOL", 1m, 20m, 0m),
            Transaction("2", TradeSide.Sell, "SOL", 3m, 25m, 0m)
        };

        var portfolio = PortfolioCalculator.Calculate(transactions);

        Assert.Empty(portfolio.Positions);
        Assert.Contains(portfolio.Warnings, warning => warning.Contains("depasse la position", StringComparison.OrdinalIgnoreCase));
    }

    private static LedgerTransaction Transaction(
        string id,
        TradeSide side,
        string symbol,
        decimal quantity,
        decimal unitPrice,
        decimal feeAmount) =>
        new(
            id,
            DateTimeOffset.Parse("2026-01-01T00:00:00+00:00").AddMinutes(int.Parse(id)),
            side,
            symbol,
            symbol,
            quantity,
            unitPrice,
            "USDT",
            feeAmount,
            "USDT",
            "TEST",
            string.Empty);
}

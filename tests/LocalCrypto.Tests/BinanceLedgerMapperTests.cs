using LocalCrypto.Core;
using LocalCrypto.Data;

namespace LocalCrypto.Tests;

public sealed class BinanceLedgerMapperTests
{
    [Fact]
    public void MapCreatesLedgerTransactionForImportableBuy()
    {
        var importEvent = Event("BUY");

        var candidate = Assert.Single(new BinanceLedgerMapper().Map([importEvent]));

        Assert.True(candidate.CanWrite);
        Assert.NotNull(candidate.Transaction);
        Assert.Equal(TradeSide.Buy, candidate.Transaction.Side);
        Assert.Equal("ETH", candidate.Transaction.Symbol);
        Assert.Equal(0.5m, candidate.Transaction.Quantity);
        Assert.Equal(2000m, candidate.Transaction.UnitPrice);
        Assert.Equal("EUR", candidate.Transaction.QuoteCurrency);
        Assert.Equal(1m, candidate.Transaction.FeeAmount);
    }

    [Fact]
    public void MapBlocksConvertAndRewards()
    {
        var convert = Event("CONVERT");
        var reward = Event("REWARD") with { Category = BinanceImportCategory.Reward };

        var candidates = new BinanceLedgerMapper().Map([convert, reward]);

        Assert.All(candidates, candidate => Assert.False(candidate.CanWrite));
        Assert.All(candidates, candidate => Assert.Null(candidate.Transaction));
    }

    [Fact]
    public void MapKeepsStableIdsForDuplicateDetection()
    {
        var importEvent = Event("SELL");
        var mapper = new BinanceLedgerMapper();

        var first = Assert.Single(mapper.Map([importEvent])).Transaction;
        var second = Assert.Single(mapper.Map([importEvent])).Transaction;

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
    }

    private static BinanceImportEvent Event(string kind) =>
        new(
            1,
            new DateTimeOffset(2026, 4, 20, 11, 37, 45, TimeSpan.Zero),
            kind,
            "ETH",
            0.5m,
            "EUR",
            1000m,
            2000m,
            1m,
            "EUR",
            1,
            BinanceImportCategory.TradeLeg,
            BinanceImportStatus.Importable,
            "test");
}

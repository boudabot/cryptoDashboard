using LocalCrypto.Core;
using LocalCrypto.Data;

namespace LocalCrypto.Tests;

public sealed class BinanceLedgerReconcilerTests
{
    [Fact]
    public void CompareMarksMatchingLedgerAndBinanceQuantityAsOk()
    {
        var ledger = PortfolioCalculator.Calculate(
        [
            Buy("ETH", 0.5m)
        ]);
        var binance = Snapshot(new BinanceCachedAssetSnapshot("Spot", "LDETH", "ETH", 0.5m, 0m, 0.5m, 2000m, 1000m, "LD mappe"));

        var comparison = Assert.Single(BinanceLedgerReconciler.Compare(ledger, binance));

        Assert.Equal("ETH", comparison.Asset);
        Assert.Equal("OK", comparison.Status);
        Assert.Equal(0m, comparison.Difference);
    }

    [Fact]
    public void CompareIgnoresLdMirrorWhenEarnRowExistsForSameAsset()
    {
        var ledger = PortfolioCalculator.Calculate(
        [
            Buy("ETH", 0.66m)
        ]);
        var rawRows = new[]
        {
            new BinanceCachedAssetSnapshot("Spot", "LDETH", "ETH", 0.64m, 0m, 0.64m, 2000m, 1280m, "LD mappe"),
            new BinanceCachedAssetSnapshot("Earn flexible", "ETH", "ETH", 0.66m, 0m, 0.66m, 2000m, 1320m, "Auto")
        };
        var markedRows = BinanceSourceConsolidator.MarkLdMirrors(rawRows);
        var binance = Snapshot(markedRows.ToArray());

        var comparison = Assert.Single(BinanceLedgerReconciler.Compare(ledger, binance));

        Assert.Equal("OK", comparison.Status);
        Assert.Equal(0.66m, comparison.BinanceQuantity);
        Assert.Contains(markedRows, row => row.Asset == "LDETH" && BinanceSourceConsolidator.IsIgnoredLdMirror(row));
    }

    [Fact]
    public void CompareMarksDifferenceBetweenLedgerAndBinance()
    {
        var ledger = PortfolioCalculator.Calculate(
        [
            Buy("ETH", 0.66m)
        ]);
        var binance = Snapshot(new BinanceCachedAssetSnapshot("Spot", "ETH", "ETH", 0.64m, 0m, 0.64m, 2000m, 1280m, "Prix public OK"));

        var comparison = Assert.Single(BinanceLedgerReconciler.Compare(ledger, binance));

        Assert.Equal("Ecart", comparison.Status);
        Assert.Equal(-0.02m, comparison.Difference);
    }

    [Fact]
    public void CompareSeparatesAssetsMissingFromOneSource()
    {
        var ledger = PortfolioCalculator.Calculate(
        [
            Buy("BTC", 0.01m)
        ]);
        var binance = Snapshot(new BinanceCachedAssetSnapshot("Earn flexible", "USDC", "USDC", 10m, 0m, 10m, 1m, 10m, "Holding"));

        var comparisons = BinanceLedgerReconciler.Compare(ledger, binance);

        Assert.Contains(comparisons, row => row.Asset == "BTC" && row.Status == "Absent Binance");
        Assert.Contains(comparisons, row => row.Asset == "USDC" && row.Status == "Absent ledger");
    }

    private static BinanceLatestSnapshot Snapshot(params BinanceCachedAssetSnapshot[] rows) =>
        new(DateTimeOffset.Parse("2026-04-26T10:00:00Z"), rows, []);

    private static LedgerTransaction Buy(string symbol, decimal quantity) =>
        new(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.Parse("2026-04-01T10:00:00Z"),
            TradeSide.Buy,
            symbol,
            symbol,
            quantity,
            100m,
            "USDT",
            0m,
            "USDT",
            "TEST",
            string.Empty);
}

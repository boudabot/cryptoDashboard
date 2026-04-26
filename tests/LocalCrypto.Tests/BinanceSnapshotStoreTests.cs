using LocalCrypto.Data;

namespace LocalCrypto.Tests;

public sealed class BinanceSnapshotStoreTests
{
    [Fact]
    public void SaveSnapshotStoresGraphCandlesInLocalCache()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"localcrypto-binance-cache-{Guid.NewGuid():N}.sqlite");
        try
        {
            var store = new BinanceSnapshotStore(databasePath);
            store.EnsureCreated();

            store.SaveSnapshot(
                DateTimeOffset.Parse("2026-04-26T10:00:00Z"),
                [
                    new BinanceCachedAssetSnapshot(
                        "Spot",
                        "LDETH",
                        "ETH",
                        0.5m,
                        0m,
                        0.5m,
                        2000m,
                        1000m,
                        "LD mappe vers sous-jacent")
                ],
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ETH"] = 2000m
                },
                [
                    new BinanceOpenOrder(
                        "ETHUSDT",
                        1,
                        "client",
                        "BUY",
                        "LIMIT",
                        "NEW",
                        1900m,
                        0.1m,
                        0m,
                        DateTimeOffset.Parse("2026-04-26T09:00:00Z"),
                        DateTimeOffset.Parse("2026-04-26T09:01:00Z"))
                ],
                [
                    new BinanceKline(
                        "ETHUSDT",
                        "1d",
                        DateTimeOffset.Parse("2026-04-25T00:00:00Z"),
                        DateTimeOffset.Parse("2026-04-25T23:59:59Z"),
                        1900m,
                        2100m,
                        1850m,
                        2050m,
                        100m)
                ]);

            Assert.Equal(1, store.CountKlines());
            Assert.Equal(1, store.CountCurrentOpenOrders());
            var latest = store.LoadLatestSnapshot();
            Assert.NotNull(latest);
            var asset = Assert.Single(latest!.Assets);
            Assert.Equal("ETH", asset.UnderlyingAsset);
            Assert.Equal("ETHUSDT", Assert.Single(latest.OpenOrders).Symbol);

            store.SaveSnapshot(
                DateTimeOffset.Parse("2026-04-26T10:05:00Z"),
                [],
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
                [
                    new BinanceOpenOrder(
                        "BTCUSDT",
                        2,
                        "client-2",
                        "SELL",
                        "LIMIT",
                        "NEW",
                        80000m,
                        0.01m,
                        0m,
                        DateTimeOffset.Parse("2026-04-26T09:03:00Z"),
                        DateTimeOffset.Parse("2026-04-26T09:04:00Z"))
                ],
                []);

            Assert.Equal(1, store.CountCurrentOpenOrders());

            var purged = store.PurgeCache();

            Assert.True(purged > 0);
            Assert.Equal(0, store.CountKlines());
            Assert.Equal(0, store.CountCurrentOpenOrders());
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}

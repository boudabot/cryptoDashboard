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

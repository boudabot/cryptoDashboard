using LocalCrypto.Data;

namespace LocalCrypto.App;

public static class BinanceApiUiBuilder
{
    private static readonly HashSet<string> StableUsdtAssets = new(StringComparer.OrdinalIgnoreCase)
    {
        "USDT",
        "USDC",
        "FDUSD",
        "BUSD",
        "TUSD"
    };

    public static IReadOnlyList<BinanceLiveBalanceRow> BuildRows(
        BinanceAccountSnapshot snapshot,
        IReadOnlyDictionary<string, decimal> pricesByAsset)
    {
        return snapshot.Balances
            .Select(balance =>
            {
                var price = PriceFor(balance.Asset, pricesByAsset);
                var hasPrice = price > 0;
                var value = hasPrice ? balance.Total * price : 0m;
                return new
                {
                    Value = value,
                    Row = new BinanceLiveBalanceRow(
                        balance.Asset,
                        UiFormatting.FormatNumber(balance.Free),
                        UiFormatting.FormatNumber(balance.Locked),
                        UiFormatting.FormatNumber(balance.Total),
                        hasPrice ? $"{UiFormatting.FormatNumber(price)} USDT" : "-",
                        hasPrice ? $"{UiFormatting.FormatNumber(value)} USDT" : "Non cote",
                        StatusFor(balance.Asset, hasPrice))
                };
            })
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Row.Asset, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Row)
            .ToList();
    }

    public static decimal TotalUsdt(BinanceAccountSnapshot snapshot, IReadOnlyDictionary<string, decimal> pricesByAsset)
    {
        return snapshot.Balances.Sum(balance => balance.Total * PriceFor(balance.Asset, pricesByAsset));
    }

    public static string? PriceSymbolFor(string asset)
    {
        if (StableUsdtAssets.Contains(asset))
        {
            return null;
        }

        return asset.Equals("EUR", StringComparison.OrdinalIgnoreCase)
            ? "EURUSDT"
            : $"{asset.ToUpperInvariant()}USDT";
    }

    private static decimal PriceFor(string asset, IReadOnlyDictionary<string, decimal> pricesByAsset)
    {
        if (asset.Equals("USDT", StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        if (StableUsdtAssets.Contains(asset))
        {
            return pricesByAsset.TryGetValue(asset, out var stablePrice) && stablePrice > 0 ? stablePrice : 1m;
        }

        return pricesByAsset.TryGetValue(asset, out var price) ? price : 0m;
    }

    private static string StatusFor(string asset, bool hasPrice)
    {
        if (asset.Equals("USDT", StringComparison.OrdinalIgnoreCase))
        {
            return "Stable reference";
        }

        if (StableUsdtAssets.Contains(asset))
        {
            return "Stable approx";
        }

        return hasPrice ? "Prix public OK" : "Prix indisponible";
    }
}

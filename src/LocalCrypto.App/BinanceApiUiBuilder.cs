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
        BinanceAccountSnapshot spotSnapshot,
        IReadOnlyList<BinanceEarnPosition> earnPositions,
        IReadOnlyDictionary<string, decimal> pricesByAsset)
    {
        var cachedRows = BuildCachedRows(spotSnapshot, earnPositions, pricesByAsset);
        return ToLiveRows(cachedRows);
    }

    public static IReadOnlyList<BinanceCachedAssetSnapshot> BuildCachedRows(
        BinanceAccountSnapshot spotSnapshot,
        IReadOnlyList<BinanceEarnPosition> earnPositions,
        IReadOnlyDictionary<string, decimal> pricesByAsset)
    {
        var rows = new List<BinanceCachedAssetSnapshot>();
        rows.AddRange(spotSnapshot.Balances.Select(balance => ToCachedRow(
            "Spot",
            balance.Asset,
            UnderlyingAssetFor(balance.Asset),
            balance.Free,
            balance.Locked,
            balance.Total,
            pricesByAsset)));

        rows.AddRange(earnPositions.Select(position => ToCachedRow(
            position.Source,
            position.Asset,
            UnderlyingAssetFor(position.Asset),
            position.Amount,
            0m,
            position.Amount,
            pricesByAsset,
            position.Status)));

        return BinanceSourceConsolidator.MarkLdMirrors(rows);
    }

    public static IReadOnlyList<BinanceLiveBalanceRow> ToLiveRows(IReadOnlyList<BinanceCachedAssetSnapshot> cachedRows) =>
        cachedRows
            .Select(ToLiveRow)
            .OrderByDescending(item => item.ValueUsdtValue ?? 0m)
            .ThenBy(item => SourceRank(item.Source))
            .ThenBy(item => item.UnderlyingAsset, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<BinanceOpenOrderRow> BuildOpenOrderRows(IReadOnlyList<BinanceOpenOrder> orders) =>
        orders.Select(order => new BinanceOpenOrderRow(
                order.Symbol,
                order.Side,
                order.Type,
                order.Status,
                UiFormatting.Money(order.Price, QuoteAssetFromSymbol(order.Symbol)),
                UiFormatting.FormatNumber(order.OriginalQuantity),
                UiFormatting.FormatNumber(order.ExecutedQuantity),
                order.UpdatedAt.ToLocalTime().ToString("dd/MM HH:mm", UiFormatting.FrenchCulture)))
            .ToList();

    public static IReadOnlyList<BinanceLedgerComparisonRow> BuildComparisonRows(
        IReadOnlyList<BinanceLedgerComparison> comparisons) =>
        comparisons.Select(comparison => new BinanceLedgerComparisonRow(
                comparison.Asset,
                UiFormatting.FormatNumber(comparison.LedgerQuantity),
                UiFormatting.FormatNumber(comparison.BinanceQuantity),
                UiFormatting.FormatNumber(comparison.Difference),
                comparison.Status,
                BrushFor(comparison.StatusTone)))
            .ToList();

    public static decimal TotalUsdt(IReadOnlyList<BinanceCachedAssetSnapshot> rows) =>
        BinanceSourceConsolidator.Consolidate(rows).Sum(row => row.ValueUsdt ?? 0m);

    public static IReadOnlyList<string> AssetsNeedingMarketData(IReadOnlyList<BinanceLiveBalanceRow> rows) =>
        rows.Select(row => row.UnderlyingAsset)
            .Where(asset => PriceSymbolFor(asset) is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string? PriceSymbolFor(string asset)
    {
        var underlying = UnderlyingAssetFor(asset);
        if (StableUsdtAssets.Contains(underlying))
        {
            return null;
        }

        return underlying.Equals("EUR", StringComparison.OrdinalIgnoreCase)
            ? "EURUSDT"
            : $"{underlying.ToUpperInvariant()}USDT";
    }

    public static string UnderlyingAssetFor(string asset)
        => BinanceAssetNormalizer.UnderlyingAssetFor(asset);

    private static BinanceCachedAssetSnapshot ToCachedRow(
        string source,
        string asset,
        string underlyingAsset,
        decimal free,
        decimal locked,
        decimal total,
        IReadOnlyDictionary<string, decimal> pricesByAsset,
        string? sourceStatus = null)
    {
        var price = PriceFor(underlyingAsset, pricesByAsset);
        var hasPrice = price > 0;
        var value = hasPrice ? total * price : (decimal?)null;
        var status = StatusFor(asset, underlyingAsset, hasPrice, sourceStatus);

        return new BinanceCachedAssetSnapshot(
            source,
            asset.ToUpperInvariant(),
            underlyingAsset.ToUpperInvariant(),
            free,
            locked,
            total,
            hasPrice ? price : null,
            value,
            status);
    }

    private static BinanceLiveBalanceRow ToLiveRow(BinanceCachedAssetSnapshot row) =>
        new(
            row.Source,
            row.Asset,
            row.UnderlyingAsset,
            UiFormatting.FormatNumber(row.FreeAmount),
            UiFormatting.FormatNumber(row.LockedAmount),
            UiFormatting.FormatNumber(row.TotalAmount),
            row.PriceUsdt is not null ? $"{UiFormatting.FormatNumber(row.PriceUsdt.Value)} USDT" : "-",
            row.ValueUsdt is not null ? $"{UiFormatting.FormatNumber(row.ValueUsdt.Value)} USDT" : "Non consolide",
            row.Status,
            row.FreeAmount,
            row.LockedAmount,
            row.TotalAmount,
            row.PriceUsdt,
            row.ValueUsdt);

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

    private static string StatusFor(string asset, string underlyingAsset, bool hasPrice, string? sourceStatus)
    {
        if (asset.StartsWith("LD", StringComparison.OrdinalIgnoreCase))
        {
            return hasPrice ? "LD mappe vers sous-jacent" : "LD sans prix public";
        }

        if (!string.IsNullOrWhiteSpace(sourceStatus) && !sourceStatus.Equals("Holding", StringComparison.OrdinalIgnoreCase))
        {
            return sourceStatus;
        }

        if (underlyingAsset.Equals("USDT", StringComparison.OrdinalIgnoreCase))
        {
            return "Stable reference";
        }

        if (StableUsdtAssets.Contains(underlyingAsset))
        {
            return "Stable approx";
        }

        return hasPrice ? "Prix public OK" : "Prix indisponible";
    }

    private static int SourceRank(string source) => source switch
    {
        "Spot" => 0,
        "Earn flexible" => 1,
        "Earn locked" => 2,
        _ => 3
    };

    private static string QuoteAssetFromSymbol(string symbol)
    {
        if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
        {
            return "USDT";
        }

        if (symbol.EndsWith("USDC", StringComparison.OrdinalIgnoreCase))
        {
            return "USDC";
        }

        if (symbol.EndsWith("EUR", StringComparison.OrdinalIgnoreCase))
        {
            return "EUR";
        }

        return string.Empty;
    }

    private static string BrushFor(string tone) => tone switch
    {
        "Good" => "#22C55E",
        "Warning" => "#FBBF24",
        "Danger" => "#F87171",
        _ => "#94A3B8"
    };
}

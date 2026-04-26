namespace LocalCrypto.Data;

public static class BinanceSourceConsolidator
{
    public static IReadOnlyList<BinanceCachedAssetSnapshot> MarkLdMirrors(
        IReadOnlyList<BinanceCachedAssetSnapshot> rows)
    {
        var earnAssets = rows
            .Where(IsEarnRow)
            .Select(row => row.UnderlyingAsset)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows
            .Select(row => IsLdMirror(row, earnAssets)
                ? row with
                {
                    ValueUsdt = null,
                    Status = "Miroir LD ignore du total"
                }
                : row)
            .ToList();
    }

    public static IReadOnlyList<BinanceCachedAssetSnapshot> Consolidate(
        IReadOnlyList<BinanceCachedAssetSnapshot> rows)
    {
        var markedRows = MarkLdMirrors(rows);
        return markedRows
            .Where(row => !IsIgnoredLdMirror(row))
            .ToList();
    }

    public static bool IsIgnoredLdMirror(BinanceCachedAssetSnapshot row) =>
        row.Status.Equals("Miroir LD ignore du total", StringComparison.OrdinalIgnoreCase);

    private static bool IsLdMirror(BinanceCachedAssetSnapshot row, HashSet<string> earnAssets) =>
        row.Source.Equals("Spot", StringComparison.OrdinalIgnoreCase) &&
        row.Asset.StartsWith("LD", StringComparison.OrdinalIgnoreCase) &&
        earnAssets.Contains(row.UnderlyingAsset);

    private static bool IsEarnRow(BinanceCachedAssetSnapshot row) =>
        row.Source.StartsWith("Earn", StringComparison.OrdinalIgnoreCase);
}

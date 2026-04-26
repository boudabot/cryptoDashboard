namespace LocalCrypto.Data;

public static class BinanceAssetNormalizer
{
    public static string UnderlyingAssetFor(string asset)
    {
        var normalized = asset.Trim().ToUpperInvariant();
        return normalized.StartsWith("LD", StringComparison.OrdinalIgnoreCase) && normalized.Length > 2
            ? normalized[2..]
            : normalized;
    }
}

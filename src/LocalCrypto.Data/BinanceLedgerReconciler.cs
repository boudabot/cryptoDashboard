using LocalCrypto.Core;

namespace LocalCrypto.Data;

public static class BinanceLedgerReconciler
{
    private const decimal QuantityTolerance = 0.00000001m;

    public static IReadOnlyList<BinanceLedgerComparison> Compare(
        PortfolioSnapshot ledger,
        BinanceLatestSnapshot? binanceSnapshot)
    {
        var ledgerByAsset = ledger.Positions
            .GroupBy(position => NormalizeAsset(position.Symbol), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(position => position.Quantity),
                StringComparer.OrdinalIgnoreCase);

        var binanceByAsset = BinanceSourceConsolidator.Consolidate(binanceSnapshot?.Assets ?? [])
            .GroupBy(row => NormalizeAsset(row.UnderlyingAsset), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(row => row.TotalAmount),
                StringComparer.OrdinalIgnoreCase);

        return ledgerByAsset.Keys
            .Concat(binanceByAsset.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase)
            .Select(asset =>
            {
                ledgerByAsset.TryGetValue(asset, out var ledgerQuantity);
                binanceByAsset.TryGetValue(asset, out var binanceQuantity);
                var difference = binanceQuantity - ledgerQuantity;
                var status = StatusFor(ledgerQuantity, binanceQuantity, difference);
                return new BinanceLedgerComparison(
                    asset,
                    ledgerQuantity,
                    binanceQuantity,
                    difference,
                    status,
                    StatusTone(status));
            })
            .ToList();
    }

    private static string StatusFor(decimal ledgerQuantity, decimal binanceQuantity, decimal difference)
    {
        if (ledgerQuantity == 0m && binanceQuantity > 0m)
        {
            return "Absent ledger";
        }

        if (ledgerQuantity > 0m && binanceQuantity == 0m)
        {
            return "Absent Binance";
        }

        return Math.Abs(difference) <= QuantityTolerance ? "OK" : "Ecart";
    }

    private static string StatusTone(string status) => status switch
    {
        "OK" => "Good",
        "Ecart" => "Warning",
        "Absent ledger" => "Warning",
        "Absent Binance" => "Danger",
        _ => "Neutral"
    };

    private static string NormalizeAsset(string asset) => BinanceAssetNormalizer.UnderlyingAssetFor(asset);
}

public sealed record BinanceLedgerComparison(
    string Asset,
    decimal LedgerQuantity,
    decimal BinanceQuantity,
    decimal Difference,
    string Status,
    string StatusTone);

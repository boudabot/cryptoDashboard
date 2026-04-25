using System.Globalization;

namespace LocalCrypto.Data;

public sealed class BinanceImportReconciler
{
    public BinanceImportReconciliation Reconcile(IReadOnlyList<BinanceImportEvent> events)
    {
        var accepted = new Dictionary<string, BinanceImportEvent>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<BinanceImportDuplicate>();

        foreach (var importEvent in events.OrderByDescending(SourcePriority).ThenBy(item => item.EventNumber))
        {
            var key = MovementKey(importEvent);
            if (accepted.TryGetValue(key, out var existing))
            {
                if (SourcePriority(importEvent) > SourcePriority(existing))
                {
                    accepted[key] = importEvent;
                    duplicates.Add(new BinanceImportDuplicate(existing, importEvent, "Remplace par une source Binance plus precise."));
                }
                else
                {
                    duplicates.Add(new BinanceImportDuplicate(importEvent, existing, "Doublon probable deja couvert par un autre export."));
                }

                continue;
            }

            accepted.Add(key, importEvent);
        }

        return new BinanceImportReconciliation(
            accepted.Values.OrderBy(item => item.ExecutedAt).ThenBy(item => item.EventNumber).ToList(),
            duplicates);
    }

    public static string MovementKey(BinanceImportEvent importEvent)
    {
        if (!string.IsNullOrWhiteSpace(importEvent.ExternalId))
        {
            return $"order|{Normalize(importEvent.ExternalId)}";
        }

        return string.Join("|", new[]
        {
            "movement",
            importEvent.ExecutedAt?.ToUniversalTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) ?? "",
            Normalize(importEvent.Kind),
            Normalize(importEvent.Asset),
            Format(importEvent.Quantity),
            Normalize(importEvent.QuoteCurrency),
            Format(importEvent.QuoteAmount)
        });
    }

    public static int SourcePriority(BinanceImportEvent importEvent) =>
        importEvent.SourceKind switch
        {
            "SpotTrade" => 100,
            "SpotOrder" => 90,
            "AlphaOrder" => 90,
            "AutoInvest" => 85,
            "TransactionHistory" => 60,
            _ => 10
        };

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static string Format(decimal? value) =>
        value?.ToString("0.########", CultureInfo.InvariantCulture) ?? "";
}

public sealed record BinanceImportReconciliation(
    IReadOnlyList<BinanceImportEvent> Accepted,
    IReadOnlyList<BinanceImportDuplicate> Duplicates);

public sealed record BinanceImportDuplicate(
    BinanceImportEvent Duplicate,
    BinanceImportEvent Kept,
    string Reason);

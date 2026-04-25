using System.Security.Cryptography;
using System.Text;
using LocalCrypto.Core;

namespace LocalCrypto.Data;

public sealed class BinanceLedgerMapper
{
    public IReadOnlyList<BinanceLedgerCandidate> Map(IReadOnlyList<BinanceImportEvent> events)
    {
        return events
            .OrderBy(importEvent => importEvent.ExecutedAt)
            .ThenBy(importEvent => importEvent.EventNumber)
            .Select(MapEvent)
            .ToList();
    }

    private static BinanceLedgerCandidate MapEvent(BinanceImportEvent importEvent)
    {
        if (importEvent.Category != BinanceImportCategory.TradeLeg)
        {
            return Block(importEvent, "Seuls les trades BUY/SELL sont ecrits automatiquement dans le ledger.");
        }

        if (importEvent.Kind is not ("BUY" or "SELL"))
        {
            return Block(importEvent, "Convert/reward/interne a confirmer avant ecriture ledger.");
        }

        if (importEvent.Status != BinanceImportStatus.Importable)
        {
            return Block(importEvent, "Evenement Binance non importable sans verification.");
        }

        if (importEvent.ExecutedAt is null)
        {
            return Block(importEvent, "Date execution manquante.");
        }

        if (string.IsNullOrWhiteSpace(importEvent.Asset) || importEvent.Asset == "-")
        {
            return Block(importEvent, "Actif manquant.");
        }

        if (importEvent.Quantity is not > 0m)
        {
            return Block(importEvent, "Quantite manquante ou invalide.");
        }

        if (importEvent.UnitPrice is not > 0m)
        {
            return Block(importEvent, "Prix moyen manquant ou invalide.");
        }

        if (string.IsNullOrWhiteSpace(importEvent.QuoteCurrency) || importEvent.QuoteCurrency == "-")
        {
            return Block(importEvent, "Devise de contrepartie manquante.");
        }

        var transaction = new LedgerTransaction(
            StableId(importEvent),
            importEvent.ExecutedAt.Value,
            importEvent.Kind == "SELL" ? TradeSide.Sell : TradeSide.Buy,
            importEvent.Asset.ToUpperInvariant(),
            AssetDescription(importEvent.Asset),
            importEvent.Quantity.Value,
            importEvent.UnitPrice.Value,
            importEvent.QuoteCurrency.ToUpperInvariant(),
            importEvent.FeeAmount ?? 0m,
            string.IsNullOrWhiteSpace(importEvent.FeeCurrency) || importEvent.FeeCurrency == "-"
                ? importEvent.QuoteCurrency.ToUpperInvariant()
                : importEvent.FeeCurrency.ToUpperInvariant(),
            "BINANCE",
            $"Import Binance {importEvent.Kind} - {importEvent.Reason}");

        return new BinanceLedgerCandidate(importEvent, transaction, true, "Pret pour ecriture ledger.");
    }

    private static BinanceLedgerCandidate Block(BinanceImportEvent importEvent, string reason) =>
        new(importEvent, null, false, reason);

    private static string StableId(BinanceImportEvent importEvent)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(BinanceImportReconciler.MovementKey(importEvent)));
        return "binance-" + Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static string AssetDescription(string asset) =>
        asset.ToUpperInvariant() switch
        {
            "BTC" => "Bitcoin",
            "ETH" => "Ethereum",
            "USDC" => "USD Coin",
            "USDT" => "Tether USD",
            "SOL" => "Solana",
            "EUR" => "Euro cash",
            _ => asset.ToUpperInvariant()
        };
}

public sealed record BinanceLedgerCandidate(
    BinanceImportEvent ImportEvent,
    LedgerTransaction? Transaction,
    bool CanWrite,
    string Reason);

using System.Globalization;
using LocalCrypto.Core;
using LocalCrypto.Data;

namespace LocalCrypto.App;

public static class UiFormatting
{
    public static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public static string Money(decimal value, string currency) =>
        $"{FormatNumber(value)} {currency}";

    public static string FormatNumber(decimal value) =>
        value.ToString("0.########", FrenchCulture);

    public static string MoneyBreakdown<T>(IEnumerable<T> rows, Func<T, string> currencySelector, Func<T, decimal> valueSelector)
    {
        var groups = rows
            .GroupBy(currencySelector, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Key != "-")
            .Select(group => Money(group.Sum(valueSelector), group.Key.ToUpperInvariant()))
            .ToList();

        return groups.Count == 0 ? "0" : string.Join(" / ", groups);
    }

    public static string CurrencyList(IEnumerable<string> currencies)
    {
        var list = currencies
            .Where(currency => !string.IsNullOrWhiteSpace(currency) && currency != "-")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(currency => currency, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return list.Count == 0 ? "-" : string.Join("/", list);
    }

    public static IReadOnlyList<string> QuoteCurrencies(IEnumerable<LedgerTransaction> transactions) =>
        transactions
            .Select(transaction => transaction.QuoteCurrency.ToUpperInvariant())
            .Where(currency => !string.IsNullOrWhiteSpace(currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(currency => currency, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string FeeBreakdown(IReadOnlyList<LedgerTransaction> transactions)
    {
        var groups = transactions
            .Where(transaction => transaction.FeeAmount != 0m)
            .GroupBy(transaction => transaction.FeeCurrency, StringComparer.OrdinalIgnoreCase)
            .Select(group => Money(group.Sum(transaction => transaction.FeeAmount), group.Key.ToUpperInvariant()))
            .ToList();

        return groups.Count == 0 ? "0" : string.Join(" / ", groups);
    }

    public static string LogoFor(string asset) =>
        string.IsNullOrWhiteSpace(asset) || asset == "-"
            ? "?"
            : asset.Length <= 3 ? asset : asset[..3];

    public static string AccentFor(string asset) =>
        asset.ToUpperInvariant() switch
        {
            "BTC" => "#F7931A",
            "ETH" => "#8B9CFF",
            "USDC" => "#2775CA",
            "USDT" => "#26A17B",
            "SOL" => "#14F195",
            "OPN" or "OPG" => "#FF5A1F",
            "EUR" => "#F0B90B",
            _ => "#38BDF8"
        };

    public static string AssetDescription(string asset) =>
        asset.ToUpperInvariant() switch
        {
            "BTC" => "Bitcoin",
            "ETH" => "Ethereum",
            "USDC" => "USD Coin",
            "USDT" => "Tether USD",
            "SOL" => "Solana",
            "EUR" => "Euro cash",
            _ => "Actif Binance"
        };

    public static string StatusText(BinanceImportStatus status) =>
        status switch
        {
            BinanceImportStatus.Importable => "Nouveau",
            BinanceImportStatus.Pending => "A confirmer",
            BinanceImportStatus.Ignored => "Ignore",
            BinanceImportStatus.Rejected => "Rejet",
            _ => status.ToString()
        };

    public static decimal SignedQuantity(BinanceImportEvent importEvent)
    {
        var quantity = importEvent.Quantity ?? 0m;
        return importEvent.Kind == "SELL" ? -quantity : quantity;
    }
}

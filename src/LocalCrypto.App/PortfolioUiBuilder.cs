using LocalCrypto.Core;

namespace LocalCrypto.App;

public static class PortfolioUiBuilder
{
    public static DashboardMetrics BuildDashboardMetrics(PortfolioSnapshot portfolio, IReadOnlyList<LedgerTransaction> transactions)
    {
        var currencies = UiFormatting.QuoteCurrencies(transactions);
        var mixed = currencies.Count > 1;
        var hasLedger = transactions.Count > 0;
        var confidence = !hasLedger
            ? "Vide"
            : mixed
                ? "Devise mixte"
                : portfolio.Warnings.Count == 0 ? "OK ledger" : "A verifier";

        return new DashboardMetrics(
            portfolio.Positions.Count.ToString(UiFormatting.FrenchCulture),
            portfolio.Transactions.Count.ToString(UiFormatting.FrenchCulture),
            !hasLedger ? "0" : mixed ? "Non consolide" : UiFormatting.Money(portfolio.InvestedTotal, currencies.FirstOrDefault() ?? portfolio.BaseCurrency),
            !hasLedger ? "Aucun trade valide dans SQLite" : mixed ? $"Devises: {UiFormatting.CurrencyList(currencies)}" : "hors prix live",
            !hasLedger ? "0" : mixed ? "Non consolide" : UiFormatting.Money(portfolio.RealizedPnlTotal, currencies.FirstOrDefault() ?? portfolio.BaseCurrency),
            mixed ? "PnL par devise a venir" : string.Empty,
            !hasLedger ? "0" : UiFormatting.FeeBreakdown(transactions),
            mixed ? "frais par devise" : string.Empty,
            confidence,
            ConfidenceHint(portfolio, transactions, mixed),
            confidence switch
            {
                "OK ledger" => "#22C55E",
                "Vide" => "#94A3B8",
                _ => "#FBBF24"
            });
    }

    public static PositionCardRow ToPositionCardRow(PositionSnapshot position, PortfolioSnapshot portfolio, IReadOnlyList<LedgerTransaction> allTransactions)
    {
        var transactions = allTransactions
            .Where(transaction => transaction.Symbol.Equals(position.Symbol, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var currencies = UiFormatting.QuoteCurrencies(transactions);
        var mixed = currencies.Count > 1;
        var hasWarning = portfolio.Warnings.Any(warning => warning.StartsWith($"{position.Symbol}:", StringComparison.OrdinalIgnoreCase));
        var confidence = mixed ? "Devise mixte" : hasWarning ? "A verifier" : "OK ledger";

        return new PositionCardRow(
            position.Symbol,
            position.AssetName,
            UiFormatting.LogoFor(position.Symbol),
            UiFormatting.AccentFor(position.Symbol),
            UiFormatting.FormatNumber(position.Quantity),
            mixed ? "Non consolide" : UiFormatting.Money(position.AverageCost, position.QuoteCurrency),
            mixed ? $"Mixte {UiFormatting.CurrencyList(currencies)}" : UiFormatting.Money(position.InvestedCost, position.QuoteCurrency),
            mixed ? "Non consolide" : UiFormatting.Money(position.RealizedPnl, position.QuoteCurrency),
            UiFormatting.FeeBreakdown(transactions),
            position.RealizedPnl switch
            {
                < 0m => "#F87171",
                > 0m => "#22C55E",
                _ => "#CBD5E1"
            },
            confidence,
            confidence == "OK ledger" ? "#22C55E" : "#FBBF24",
            mixed ? "devises mixtes" : string.Empty);
    }

    public static AssetXrayModel ToAssetXrayModel(PositionSnapshot position, PortfolioSnapshot portfolio, IReadOnlyList<LedgerTransaction> transactions)
    {
        var warnings = portfolio.Warnings
            .Where(warning => warning.StartsWith($"{position.Symbol}:", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var currencies = UiFormatting.QuoteCurrencies(transactions);
        var mixed = currencies.Count > 1;
        var confidence = mixed ? "Devise mixte" : warnings.Count == 0 ? "OK ledger" : "A verifier";

        return new AssetXrayModel(
            $"{position.Symbol} X-Ray",
            $"{position.AssetName} - preuve ledger avec {transactions.Count.ToString(UiFormatting.FrenchCulture)} transaction(s).",
            confidence,
            confidence == "OK ledger" ? "#22C55E" : "#FBBF24",
            mixed ? $"Cet actif melange {UiFormatting.CurrencyList(currencies)}: pas de prix moyen consolide sans conversion." : warnings.Count == 0 ? "Calcul base sur le ledger valide." : string.Join(" ", warnings),
            UiFormatting.FormatNumber(position.Quantity),
            mixed ? "Non consolide" : UiFormatting.Money(position.AverageCost, position.QuoteCurrency),
            mixed ? $"Mixte {UiFormatting.CurrencyList(currencies)}" : UiFormatting.Money(position.InvestedCost, position.QuoteCurrency),
            mixed ? "Non consolide" : UiFormatting.Money(position.RealizedPnl, position.QuoteCurrency),
            UiFormatting.FeeBreakdown(transactions),
            BuildTransactionRows(transactions));
    }

    public static IReadOnlyList<TransactionRow> BuildTransactionRows(IReadOnlyList<LedgerTransaction> transactions) =>
        transactions
            .OrderByDescending(transaction => transaction.ExecutedAt)
            .Select(transaction => new TransactionRow(
                transaction.Id,
                transaction.ExecutedAt.ToLocalTime().ToString("g", UiFormatting.FrenchCulture),
                transaction.Side.ToString(),
                transaction.Symbol,
                UiFormatting.FormatNumber(transaction.Quantity),
                UiFormatting.Money(transaction.UnitPrice, transaction.QuoteCurrency),
                UiFormatting.Money(transaction.FeeAmount, transaction.FeeCurrency),
                transaction.Source,
                transaction.Note))
            .ToList();

    public static IReadOnlyList<LedgerChartRow> BuildCostChartRows(IReadOnlyList<LedgerTransaction> transactions)
    {
        var rows = transactions
            .Where(transaction => transaction.Side == TradeSide.Buy)
            .GroupBy(transaction => transaction.QuoteCurrency, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ChartValue(group.Key.ToUpperInvariant(), group.Sum(transaction => transaction.GrossAmount + transaction.QuoteFee), "#38BDF8"))
            .ToList();
        return ScaleChartRows(rows);
    }

    public static IReadOnlyList<LedgerChartRow> BuildVolumeChartRows(IReadOnlyList<LedgerTransaction> transactions)
    {
        var rows = transactions
            .GroupBy(transaction => transaction.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ChartValue(group.Key.ToUpperInvariant(), group.Sum(transaction => transaction.Quantity), UiFormatting.AccentFor(group.Key)))
            .ToList();
        return ScaleChartRows(rows);
    }

    public static IReadOnlyList<LedgerChartRow> BuildPnlChartRows(PortfolioSnapshot portfolio, IReadOnlyList<LedgerTransaction> transactions)
    {
        var rows = portfolio.Positions
            .Select(position =>
            {
                var positionTransactions = transactions.Where(transaction => transaction.Symbol.Equals(position.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
                var mixed = UiFormatting.QuoteCurrencies(positionTransactions).Count > 1;
                return new ChartValue(position.Symbol, mixed ? 0m : position.RealizedPnl, mixed ? "#FBBF24" : position.RealizedPnl < 0m ? "#F87171" : "#22C55E", mixed ? "Non consolide" : UiFormatting.Money(position.RealizedPnl, position.QuoteCurrency));
            })
            .ToList();
        return ScaleChartRows(rows);
    }

    private static string ConfidenceHint(PortfolioSnapshot portfolio, IReadOnlyList<LedgerTransaction> transactions, bool mixed)
    {
        if (transactions.Count == 0)
        {
            return "0 transaction ledger.";
        }

        if (mixed)
        {
            return $"Non consolide: {UiFormatting.CurrencyList(UiFormatting.QuoteCurrencies(transactions))}.";
        }

        return $"{portfolio.Transactions.Count.ToString(UiFormatting.FrenchCulture)} transaction(s) ledger.";
    }

    private static IReadOnlyList<LedgerChartRow> ScaleChartRows(IReadOnlyList<ChartValue> values)
    {
        if (values.Count == 0)
        {
            return [new LedgerChartRow("-", "Aucune donnee", 8d, "#334155")];
        }

        var max = Math.Max(1m, values.Max(value => Math.Abs(value.Amount)));
        return values
            .OrderByDescending(value => Math.Abs(value.Amount))
            .Take(8)
            .Select(value => new LedgerChartRow(value.Label, value.DisplayValue ?? UiFormatting.FormatNumber(value.Amount), Math.Max(8d, 210d * (double)(Math.Abs(value.Amount) / max)), value.Color))
            .ToList();
    }

    private sealed record ChartValue(string Label, decimal Amount, string Color, string? DisplayValue = null);
}

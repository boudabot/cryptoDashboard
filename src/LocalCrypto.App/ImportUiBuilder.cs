using LocalCrypto.Data;

namespace LocalCrypto.App;

public static class ImportUiBuilder
{
    public static ImportSummaryModel BuildSummary(IReadOnlyList<BinanceImportEvent> events, IReadOnlyList<BinanceImportDuplicate> duplicates) =>
        new(
            UiFormatting.MoneyBreakdown(events.Where(row => row.Category == BinanceImportCategory.TradeLeg && row.QuoteAmount.HasValue), row => row.QuoteCurrency, row => row.QuoteAmount ?? 0m),
            events.Count.ToString(UiFormatting.FrenchCulture),
            events.Count(row => row.Category == BinanceImportCategory.TradeLeg).ToString(UiFormatting.FrenchCulture),
            events.Count(row => row.Status == BinanceImportStatus.Pending).ToString(UiFormatting.FrenchCulture),
            events.Select(row => row.Asset).Where(asset => asset != "-").Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(UiFormatting.FrenchCulture),
            duplicates.Count.ToString(UiFormatting.FrenchCulture));

    public static IReadOnlyList<AssetChipRow> BuildAssetChips(IReadOnlyList<BinanceImportEvent> events, string assetFilter)
    {
        var rows = events
            .Where(row => row.Asset != "-")
            .GroupBy(row => row.Asset, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AssetChipRow(
                group.Key,
                group.Key,
                group.Count().ToString(UiFormatting.FrenchCulture),
                UiFormatting.AccentFor(group.Key),
                group.Key.Equals(assetFilter, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        rows.Insert(0, new AssetChipRow("*", "Tous", events.Count.ToString(UiFormatting.FrenchCulture), "#64748B", string.IsNullOrWhiteSpace(assetFilter)));
        return rows;
    }

    public static IReadOnlyList<ImportAssetRow> BuildAssetRows(IReadOnlyList<BinanceImportEvent> events) =>
        events
            .Where(row => row.Asset != "-")
            .GroupBy(row => row.Asset, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count(row => row.Category == BinanceImportCategory.TradeLeg))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ImportAssetRow(
                group.Key,
                UiFormatting.LogoFor(group.Key),
                UiFormatting.AccentFor(group.Key),
                UiFormatting.AssetDescription(group.Key),
                UiFormatting.FormatNumber(group.Sum(row => UiFormatting.SignedQuantity(row))),
                group.Count(row => row.Category == BinanceImportCategory.TradeLeg).ToString(UiFormatting.FrenchCulture),
                group.Count(row => row.Status == BinanceImportStatus.Pending).ToString(UiFormatting.FrenchCulture),
                UiFormatting.CurrencyList(group.Select(row => row.QuoteCurrency).Where(currency => !string.IsNullOrWhiteSpace(currency) && currency != "-"))))
            .ToList();

    public static IReadOnlyList<RecentOrderRow> BuildRecentOrders(IReadOnlyList<BinanceImportEvent> events) =>
        events
            .Where(row => row.Category == BinanceImportCategory.TradeLeg)
            .OrderByDescending(row => row.ExecutedAt)
            .Take(8)
            .Select(row => new RecentOrderRow(
                UiFormatting.LogoFor(row.Asset),
                UiFormatting.AccentFor(row.Asset),
                $"{row.Kind} {row.Asset}",
                $"{(row.Quantity.HasValue ? UiFormatting.FormatNumber(row.Quantity.Value) : "-")} contre {(row.QuoteAmount.HasValue ? $"{UiFormatting.FormatNumber(row.QuoteAmount.Value)} {row.QuoteCurrency}" : "-")} | prix {(row.UnitPrice.HasValue ? UiFormatting.FormatNumber(row.UnitPrice.Value) : "-")}",
                UiFormatting.StatusText(row.Status),
                row.Status == BinanceImportStatus.Importable ? "#22C55E" : "#FBBF24"))
            .ToList();

    public static IReadOnlyList<ImportChartRow> BuildChartRows(IReadOnlyList<BinanceImportEvent> events, IReadOnlyList<BinanceImportDuplicate> duplicates)
    {
        var rows = new[]
        {
            ImportChartRow("Trades", events.Count(row => row.Category == BinanceImportCategory.TradeLeg), "#22C55E"),
            ImportChartRow("Earn", events.Count(row => row.Category == BinanceImportCategory.Reward), "#38BDF8"),
            ImportChartRow("Transferts", events.Count(row => row.Category == BinanceImportCategory.InternalMovement), "#94A3B8"),
            ImportChartRow("Autres", events.Count(row => row.Category is BinanceImportCategory.CashMovement or BinanceImportCategory.Unknown), "#F59E0B"),
            ImportChartRow("Doublons", duplicates.Count, "#F97316")
        };
        var max = Math.Max(1, rows.Max(row => row.RawCount));
        return rows.Select(row => row with { Width = 360d * row.RawCount / max }).ToList();
    }

    public static IReadOnlyList<QuarantineRow> BuildQuarantineRows(IReadOnlyList<BinanceImportDuplicate> duplicates) =>
        duplicates
            .Select(duplicate => new QuarantineRow(
                duplicate.Duplicate.Asset,
                duplicate.Duplicate.Kind,
                duplicate.Duplicate.SourceKind,
                duplicate.Kept.SourceKind,
                string.IsNullOrWhiteSpace(duplicate.Duplicate.ExternalId) ? "-" : duplicate.Duplicate.ExternalId,
                duplicate.Reason))
            .ToList();

    public static IReadOnlyList<BinanceImportEvent> FilterEvents(IReadOnlyList<BinanceImportEvent> source, string typeFilter, string statusFilter, string assetFilter)
    {
        if (statusFilter == "Duplicates")
        {
            return [];
        }

        IEnumerable<BinanceImportEvent> rows = source;
        rows = typeFilter switch
        {
            "Trades" => rows.Where(row => row.Category == BinanceImportCategory.TradeLeg),
            "Rewards" => rows.Where(row => row.Category == BinanceImportCategory.Reward),
            "Transfers" => rows.Where(row => row.Category == BinanceImportCategory.InternalMovement),
            "Other" => rows.Where(row => row.Category is BinanceImportCategory.CashMovement or BinanceImportCategory.Unknown),
            _ => rows
        };

        rows = statusFilter switch
        {
            "New" => rows.Where(row => row.Status == BinanceImportStatus.Importable),
            "Pending" => rows.Where(row => row.Status == BinanceImportStatus.Pending),
            "Ignored" => rows.Where(row => row.Status is BinanceImportStatus.Ignored or BinanceImportStatus.Rejected),
            _ => rows
        };

        if (!string.IsNullOrWhiteSpace(assetFilter))
        {
            rows = rows.Where(row => row.Asset.Contains(assetFilter, StringComparison.OrdinalIgnoreCase));
        }

        return rows.ToList();
    }

    public static BinancePreviewRow ToPreviewRow(BinanceImportEvent row) =>
        new(
            row.EventNumber.ToString(UiFormatting.FrenchCulture),
            row.ExecutedAt?.ToLocalTime().ToString("g", UiFormatting.FrenchCulture) ?? "-",
            row.Kind,
            row.Asset,
            row.Quantity.HasValue ? UiFormatting.FormatNumber(row.Quantity.Value) : "-",
            row.QuoteAmount.HasValue ? $"{UiFormatting.FormatNumber(row.QuoteAmount.Value)} {row.QuoteCurrency}" : "-",
            row.UnitPrice.HasValue ? $"{UiFormatting.FormatNumber(row.UnitPrice.Value)} {row.QuoteCurrency}" : "-",
            row.FeeAmount.HasValue ? $"{UiFormatting.FormatNumber(row.FeeAmount.Value)} {row.FeeCurrency}" : "-",
            row.SourceRows.ToString(UiFormatting.FrenchCulture),
            row.SourceKind,
            string.IsNullOrWhiteSpace(row.ExternalId) ? "-" : row.ExternalId,
            UiFormatting.StatusText(row.Status),
            row.Reason);

    private static ImportChartRow ImportChartRow(string label, int count, string color) =>
        new(label, count.ToString(UiFormatting.FrenchCulture), count, 0d, color);
}

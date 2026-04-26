namespace LocalCrypto.App;

public sealed record DashboardMetrics(
    string AssetCount,
    string TransactionCount,
    string TrackedCost,
    string TrackedCostHint,
    string RealizedPnl,
    string RealizedPnlHint,
    string Fees,
    string FeesHint,
    string Confidence,
    string ConfidenceHint,
    string ConfidenceTone);

public sealed record PositionCardRow(
    string Symbol,
    string AssetName,
    string Logo,
    string Accent,
    string Quantity,
    string AverageCost,
    string InvestedCost,
    string RealizedPnl,
    string Fees,
    string RealizedPnlBrush,
    string Confidence,
    string ConfidenceTone,
    string CurrencyHint);

public sealed record AssetXrayModel(
    string Title,
    string Subtitle,
    string Confidence,
    string ConfidenceTone,
    string ConfidenceHint,
    string Quantity,
    string AverageCost,
    string InvestedCost,
    string RealizedPnl,
    string Fees,
    IReadOnlyList<TransactionRow> Transactions);

public sealed record TransactionRow(
    string Id,
    string ExecutedAt,
    string Side,
    string Symbol,
    string Quantity,
    string UnitPrice,
    string Fee,
    string Source,
    string Note);

public sealed record ImportSummaryModel(
    string Volume,
    string EventCount,
    string TradeCount,
    string PendingCount,
    string AssetCount,
    string QuarantineCount);

public sealed record BinancePreviewRow(
    string EventNumber,
    string ExecutedAt,
    string Kind,
    string Asset,
    string Quantity,
    string Quote,
    string UnitPrice,
    string Fee,
    string SourceRows,
    string SourceKind,
    string ExternalId,
    string Status,
    string Reason);

public sealed record ImportChartRow(string Label, string Count, int RawCount, double Width, string Color);

public sealed record ImportAssetRow(
    string Asset,
    string Logo,
    string Accent,
    string Description,
    string NetQuantity,
    string TradeCount,
    string PendingCount,
    string QuoteBreakdown);

public sealed record RecentOrderRow(
    string Logo,
    string Accent,
    string Title,
    string Subtitle,
    string Status,
    string StatusColor);

public sealed record AssetChipRow(
    string Asset,
    string Label,
    string Count,
    string Accent,
    bool IsSelected);

public sealed record QuarantineRow(
    string Asset,
    string Kind,
    string DuplicateSource,
    string KeptSource,
    string ExternalId,
    string Reason);

public sealed record LedgerChartRow(
    string Label,
    string Value,
    double Width,
    string Color);

public sealed record BinanceLiveBalanceRow(
    string Source,
    string Asset,
    string UnderlyingAsset,
    string Free,
    string Locked,
    string Total,
    string PriceUsdt,
    string ValueUsdt,
    string Status,
    decimal FreeAmount,
    decimal LockedAmount,
    decimal TotalAmount,
    decimal? PriceUsdtValue,
    decimal? ValueUsdtValue);

public sealed record BinanceOpenOrderRow(
    string Symbol,
    string Side,
    string Type,
    string Status,
    string Price,
    string Quantity,
    string Executed,
    string UpdatedAt);

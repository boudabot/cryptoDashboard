namespace LocalCrypto.Core;

public sealed record PositionSnapshot(
    string Symbol,
    string AssetName,
    decimal Quantity,
    decimal AverageCost,
    decimal InvestedCost,
    decimal RealizedPnl,
    decimal Fees,
    string QuoteCurrency
);

public sealed record PortfolioSnapshot(
    string BaseCurrency,
    decimal InvestedTotal,
    decimal RealizedPnlTotal,
    decimal TotalFees,
    IReadOnlyList<PositionSnapshot> Positions,
    IReadOnlyList<LedgerTransaction> Transactions,
    IReadOnlyList<string> Warnings
);

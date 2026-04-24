namespace LocalCrypto.Core;

public enum TradeSide
{
    Buy,
    Sell
}

public sealed record LedgerTransaction(
    string Id,
    DateTimeOffset ExecutedAt,
    TradeSide Side,
    string Symbol,
    string AssetName,
    decimal Quantity,
    decimal UnitPrice,
    string QuoteCurrency,
    decimal FeeAmount,
    string FeeCurrency,
    string Source,
    string Note
)
{
    public decimal GrossAmount => Quantity * UnitPrice;

    public decimal QuoteFee => string.Equals(FeeCurrency, QuoteCurrency, StringComparison.OrdinalIgnoreCase)
        ? FeeAmount
        : 0m;
}

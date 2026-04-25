using LocalCrypto.Data;

namespace LocalCrypto.Tests;

public sealed class BinanceImportReconcilerTests
{
    [Fact]
    public void ReconcileKeepsHigherPrioritySourceForSameMovement()
    {
        var transactionHistory = Event("TransactionHistory");
        var spotTrade = Event("SpotTrade");

        var reconciliation = new BinanceImportReconciler().Reconcile([transactionHistory, spotTrade]);

        var accepted = Assert.Single(reconciliation.Accepted);
        Assert.Equal("SpotTrade", accepted.SourceKind);
        Assert.Single(reconciliation.Duplicates);
    }

    [Fact]
    public void ReconcileUsesOrderIdForCsvAndXlsxDuplicates()
    {
        var orderCsv = Event("SpotOrder") with { ExternalId = "4800489741", UnitPrice = 1965m };
        var orderXlsx = Event("SpotOrder") with { ExternalId = "4800489741", UnitPrice = 1965m };

        var reconciliation = new BinanceImportReconciler().Reconcile([orderCsv, orderXlsx]);

        Assert.Single(reconciliation.Accepted);
        Assert.Single(reconciliation.Duplicates);
    }

    [Fact]
    public void ReconcileKeepsDistinctPartialFills()
    {
        var first = Event("AlphaOrder") with { ExternalId = "1", Quantity = 62.54m };
        var second = Event("AlphaOrder") with { ExternalId = "2", Quantity = 125.1m };

        var reconciliation = new BinanceImportReconciler().Reconcile([first, second]);

        Assert.Equal(2, reconciliation.Accepted.Count);
        Assert.Empty(reconciliation.Duplicates);
    }

    private static BinanceImportEvent Event(string sourceKind) =>
        new(
            1,
            new DateTimeOffset(2026, 4, 20, 11, 37, 45, TimeSpan.Zero),
            "BUY",
            "ETH",
            0.0514m,
            "EUR",
            101.001m,
            1965m,
            0.101001m,
            "EUR",
            1,
            BinanceImportCategory.TradeLeg,
            BinanceImportStatus.Importable,
            "test")
        {
            SourceKind = sourceKind,
            Pair = "ETHEUR"
        };
}

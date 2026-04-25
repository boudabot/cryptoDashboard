using LocalCrypto.Core;
using LocalCrypto.Data;

namespace LocalCrypto.Tests;

public sealed class SqliteLedgerStoreTests : IDisposable
{
    private readonly string _directory;

    public SqliteLedgerStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "localcrypto-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void AddTransactionRejectsDuplicateSignature()
    {
        var store = CreateStore("ledger.sqlite");
        var transaction = Transaction("1", TradeSide.Buy, "BTC");

        store.AddTransaction(transaction);

        Assert.True(store.HasDuplicate(transaction));
        Assert.Throws<DuplicateTransactionException>(() => store.AddTransaction(transaction with { Id = "2" }));
    }

    [Fact]
    public void DeleteTransactionRemovesOnlySelectedTransaction()
    {
        var store = CreateStore("ledger.sqlite");
        var first = Transaction("1", TradeSide.Buy, "BTC");
        var second = Transaction("2", TradeSide.Buy, "ETH");
        store.AddTransaction(first);
        store.AddTransaction(second);

        Assert.True(store.DeleteTransaction(first.Id));

        var remaining = Assert.Single(store.ListTransactions());
        Assert.Equal(second.Id, remaining.Id);
    }

    [Fact]
    public void ClearTransactionsRemovesLedgerRows()
    {
        var store = CreateStore("ledger.sqlite");
        store.AddTransaction(Transaction("1", TradeSide.Buy, "BTC"));
        store.AddTransaction(Transaction("2", TradeSide.Buy, "ETH"));

        var deleted = store.ClearTransactions();

        Assert.Equal(2, deleted);
        Assert.Empty(store.ListTransactions());
    }

    [Fact]
    public void BackupAndRestoreKeepLedgerTransactions()
    {
        var source = CreateStore("source.sqlite");
        source.AddTransaction(Transaction("1", TradeSide.Buy, "BTC"));
        var backupPath = Path.Combine(_directory, "backup.sqlite");

        source.BackupDatabase(backupPath);

        var restored = CreateStore("restored.sqlite");
        restored.RestoreDatabase(backupPath);

        var transaction = Assert.Single(restored.ListTransactions());
        Assert.Equal("BTC", transaction.Symbol);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SqliteLedgerStore CreateStore(string fileName)
    {
        var store = new SqliteLedgerStore(Path.Combine(_directory, fileName));
        store.EnsureCreated();
        return store;
    }

    private static LedgerTransaction Transaction(string id, TradeSide side, string symbol) =>
        new(
            id,
            DateTimeOffset.Parse("2026-01-01T00:00:00+00:00").AddMinutes(int.Parse(id)),
            side,
            symbol,
            symbol,
            1m,
            100m,
            "USDT",
            1m,
            "USDT",
            "TEST",
            string.Empty);
}

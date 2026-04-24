using System.Globalization;
using LocalCrypto.Core;
using Microsoft.Data.Sqlite;

namespace LocalCrypto.Data;

public sealed class SqliteLedgerStore
{
    private readonly string _databasePath;

    public SqliteLedgerStore(string databasePath)
    {
        _databasePath = databasePath;
    }

    public string DatabasePath => _databasePath;

    public static SqliteLedgerStore OpenDefault()
    {
        Directory.CreateDirectory(AppDataPaths.DataDirectory);
        var store = new SqliteLedgerStore(AppDataPaths.DatabasePath);
        store.EnsureCreated();
        return store;
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS transactions (
                id TEXT PRIMARY KEY,
                executed_at TEXT NOT NULL,
                side TEXT NOT NULL CHECK (side IN ('BUY', 'SELL')),
                symbol TEXT NOT NULL,
                asset_name TEXT NOT NULL,
                quantity REAL NOT NULL,
                unit_price REAL NOT NULL,
                quote_currency TEXT NOT NULL,
                fee_amount REAL NOT NULL DEFAULT 0,
                fee_currency TEXT NOT NULL,
                source TEXT NOT NULL,
                note TEXT NOT NULL DEFAULT '',
                external_id TEXT,
                duplicate_signature TEXT NOT NULL UNIQUE,
                import_id TEXT,
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_transactions_executed_at ON transactions(executed_at);
            CREATE INDEX IF NOT EXISTS idx_transactions_symbol ON transactions(symbol);
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<LedgerTransaction> ListTransactions()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, executed_at, side, symbol, asset_name, quantity, unit_price,
                   quote_currency, fee_amount, fee_currency, source, note
            FROM transactions
            ORDER BY executed_at DESC, created_at DESC;
            """;

        using var reader = command.ExecuteReader();
        var transactions = new List<LedgerTransaction>();

        while (reader.Read())
        {
            transactions.Add(new LedgerTransaction(
                reader.GetString(0),
                DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Enum.Parse<TradeSide>(reader.GetString(2), ignoreCase: true),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.GetString(7),
                reader.GetDecimal(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetString(11)));
        }

        return transactions;
    }

    public void AddTransaction(LedgerTransaction transaction)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO transactions (
                id, executed_at, side, symbol, asset_name, quantity, unit_price,
                quote_currency, fee_amount, fee_currency, source, note, external_id,
                duplicate_signature, import_id, created_at
            )
            VALUES (
                $id, $executed_at, $side, $symbol, $asset_name, $quantity, $unit_price,
                $quote_currency, $fee_amount, $fee_currency, $source, $note, $external_id,
                $duplicate_signature, $import_id, $created_at
            );
            """;

        command.Parameters.AddWithValue("$id", transaction.Id);
        command.Parameters.AddWithValue("$executed_at", transaction.ExecutedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$side", transaction.Side == TradeSide.Buy ? "BUY" : "SELL");
        command.Parameters.AddWithValue("$symbol", transaction.Symbol.ToUpperInvariant());
        command.Parameters.AddWithValue("$asset_name", transaction.AssetName);
        command.Parameters.AddWithValue("$quantity", transaction.Quantity);
        command.Parameters.AddWithValue("$unit_price", transaction.UnitPrice);
        command.Parameters.AddWithValue("$quote_currency", transaction.QuoteCurrency.ToUpperInvariant());
        command.Parameters.AddWithValue("$fee_amount", transaction.FeeAmount);
        command.Parameters.AddWithValue("$fee_currency", transaction.FeeCurrency.ToUpperInvariant());
        command.Parameters.AddWithValue("$source", transaction.Source);
        command.Parameters.AddWithValue("$note", transaction.Note);
        command.Parameters.AddWithValue("$external_id", DBNull.Value);
        command.Parameters.AddWithValue("$duplicate_signature", DuplicateSignature(transaction));
        command.Parameters.AddWithValue("$import_id", DBNull.Value);
        command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static string DuplicateSignature(LedgerTransaction transaction)
    {
        var parts = new[]
        {
            transaction.ExecutedAt.ToString("O", CultureInfo.InvariantCulture),
            transaction.Side == TradeSide.Buy ? "BUY" : "SELL",
            transaction.Symbol.ToUpperInvariant(),
            transaction.Quantity.ToString(CultureInfo.InvariantCulture),
            transaction.UnitPrice.ToString(CultureInfo.InvariantCulture),
            transaction.FeeAmount.ToString(CultureInfo.InvariantCulture),
            transaction.FeeCurrency.ToUpperInvariant()
        };

        return string.Join("|", parts);
    }
}

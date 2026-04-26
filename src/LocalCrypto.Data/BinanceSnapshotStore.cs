using System.Globalization;
using Microsoft.Data.Sqlite;

namespace LocalCrypto.Data;

public sealed class BinanceSnapshotStore
{
    private const int SnapshotRetentionDays = 30;

    private readonly string _databasePath;

    public BinanceSnapshotStore(string databasePath)
    {
        _databasePath = databasePath;
    }

    public static BinanceSnapshotStore OpenDefault()
    {
        Directory.CreateDirectory(AppDataPaths.DataDirectory);
        var store = new BinanceSnapshotStore(AppDataPaths.DatabasePath);
        store.EnsureCreated();
        return store;
    }

    public void EnsureCreated()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS binance_asset_snapshots (
                id TEXT PRIMARY KEY,
                synced_at TEXT NOT NULL,
                source TEXT NOT NULL,
                asset TEXT NOT NULL,
                underlying_asset TEXT NOT NULL,
                free_amount REAL NOT NULL,
                locked_amount REAL NOT NULL,
                total_amount REAL NOT NULL,
                price_usdt REAL,
                value_usdt REAL,
                status TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_binance_asset_snapshots_synced_at
                ON binance_asset_snapshots(synced_at);

            CREATE TABLE IF NOT EXISTS binance_open_order_snapshots (
                id TEXT PRIMARY KEY,
                synced_at TEXT NOT NULL,
                symbol TEXT NOT NULL,
                order_id INTEGER NOT NULL,
                side TEXT NOT NULL,
                type TEXT NOT NULL,
                status TEXT NOT NULL,
                price REAL NOT NULL,
                original_quantity REAL NOT NULL,
                executed_quantity REAL NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_binance_open_order_snapshots_synced_at
                ON binance_open_order_snapshots(synced_at);

            CREATE TABLE IF NOT EXISTS binance_open_orders_current (
                order_id INTEGER PRIMARY KEY,
                synced_at TEXT NOT NULL,
                symbol TEXT NOT NULL,
                side TEXT NOT NULL,
                type TEXT NOT NULL,
                status TEXT NOT NULL,
                price REAL NOT NULL,
                original_quantity REAL NOT NULL,
                executed_quantity REAL NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS binance_price_snapshots (
                id TEXT PRIMARY KEY,
                synced_at TEXT NOT NULL,
                symbol TEXT NOT NULL,
                asset TEXT NOT NULL,
                quote_asset TEXT NOT NULL,
                price REAL NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_binance_price_snapshots_synced_at
                ON binance_price_snapshots(synced_at);

            CREATE TABLE IF NOT EXISTS binance_klines (
                symbol TEXT NOT NULL,
                interval TEXT NOT NULL,
                open_time TEXT NOT NULL,
                close_time TEXT NOT NULL,
                open_price REAL NOT NULL,
                high_price REAL NOT NULL,
                low_price REAL NOT NULL,
                close_price REAL NOT NULL,
                volume REAL NOT NULL,
                synced_at TEXT NOT NULL,
                PRIMARY KEY (symbol, interval, open_time)
            );
            """;
        command.ExecuteNonQuery();
    }

    public void SaveSnapshot(
        DateTimeOffset syncedAt,
        IReadOnlyList<BinanceCachedAssetSnapshot> assetRows,
        IReadOnlyDictionary<string, decimal> pricesByAsset,
        IReadOnlyList<BinanceOpenOrder> openOrders,
        IReadOnlyList<BinanceKline> klines)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var row in assetRows)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO binance_asset_snapshots (
                    id, synced_at, source, asset, underlying_asset, free_amount, locked_amount,
                    total_amount, price_usdt, value_usdt, status
                )
                VALUES (
                    $id, $synced_at, $source, $asset, $underlying_asset, $free_amount, $locked_amount,
                    $total_amount, $price_usdt, $value_usdt, $status
                );
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            command.Parameters.AddWithValue("$synced_at", syncedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$source", row.Source);
            command.Parameters.AddWithValue("$asset", row.Asset.ToUpperInvariant());
            command.Parameters.AddWithValue("$underlying_asset", row.UnderlyingAsset.ToUpperInvariant());
            command.Parameters.AddWithValue("$free_amount", row.FreeAmount);
            command.Parameters.AddWithValue("$locked_amount", row.LockedAmount);
            command.Parameters.AddWithValue("$total_amount", row.TotalAmount);
            command.Parameters.AddWithValue("$price_usdt", row.PriceUsdt is null ? DBNull.Value : row.PriceUsdt);
            command.Parameters.AddWithValue("$value_usdt", row.ValueUsdt is null ? DBNull.Value : row.ValueUsdt);
            command.Parameters.AddWithValue("$status", row.Status);
            command.ExecuteNonQuery();
        }

        foreach (var price in pricesByAsset.Where(item => item.Value > 0))
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO binance_price_snapshots (
                    id, synced_at, symbol, asset, quote_asset, price
                )
                VALUES ($id, $synced_at, $symbol, $asset, $quote_asset, $price);
                """;
            var asset = price.Key.ToUpperInvariant();
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            command.Parameters.AddWithValue("$synced_at", syncedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$symbol", $"{asset}USDT");
            command.Parameters.AddWithValue("$asset", asset);
            command.Parameters.AddWithValue("$quote_asset", "USDT");
            command.Parameters.AddWithValue("$price", price.Value);
            command.ExecuteNonQuery();
        }

        ReplaceCurrentOpenOrders(connection, transaction, syncedAt, openOrders);

        foreach (var kline in klines)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO binance_klines (
                    symbol, interval, open_time, close_time, open_price, high_price,
                    low_price, close_price, volume, synced_at
                )
                VALUES (
                    $symbol, $interval, $open_time, $close_time, $open_price, $high_price,
                    $low_price, $close_price, $volume, $synced_at
                )
                ON CONFLICT(symbol, interval, open_time) DO UPDATE SET
                    close_time = excluded.close_time,
                    open_price = excluded.open_price,
                    high_price = excluded.high_price,
                    low_price = excluded.low_price,
                    close_price = excluded.close_price,
                    volume = excluded.volume,
                    synced_at = excluded.synced_at;
                """;
            command.Parameters.AddWithValue("$symbol", kline.Symbol);
            command.Parameters.AddWithValue("$interval", kline.Interval);
            command.Parameters.AddWithValue("$open_time", kline.OpenTime.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$close_time", kline.CloseTime.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$open_price", kline.Open);
            command.Parameters.AddWithValue("$high_price", kline.High);
            command.Parameters.AddWithValue("$low_price", kline.Low);
            command.Parameters.AddWithValue("$close_price", kline.Close);
            command.Parameters.AddWithValue("$volume", kline.Volume);
            command.Parameters.AddWithValue("$synced_at", syncedAt.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }

        DeleteRowsOlderThan(connection, transaction, "binance_asset_snapshots", syncedAt.AddDays(-SnapshotRetentionDays));
        DeleteRowsOlderThan(connection, transaction, "binance_price_snapshots", syncedAt.AddDays(-SnapshotRetentionDays));
        transaction.Commit();
    }

    public int CountKlines()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM binance_klines;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public int CountCurrentOpenOrders()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM binance_open_orders_current;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public int PurgeCache()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var deleted = 0;
        foreach (var table in new[]
        {
            "binance_asset_snapshots",
            "binance_open_order_snapshots",
            "binance_open_orders_current",
            "binance_price_snapshots",
            "binance_klines"
        })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table};";
            deleted += command.ExecuteNonQuery();
        }

        transaction.Commit();
        return deleted;
    }

    private static void ReplaceCurrentOpenOrders(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateTimeOffset syncedAt,
        IReadOnlyList<BinanceOpenOrder> openOrders)
    {
        using (var clearCommand = connection.CreateCommand())
        {
            clearCommand.Transaction = transaction;
            clearCommand.CommandText = "DELETE FROM binance_open_orders_current;";
            clearCommand.ExecuteNonQuery();
        }

        foreach (var order in openOrders)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO binance_open_orders_current (
                    order_id, synced_at, symbol, side, type, status, price,
                    original_quantity, executed_quantity, created_at, updated_at
                )
                VALUES (
                    $order_id, $synced_at, $symbol, $side, $type, $status, $price,
                    $original_quantity, $executed_quantity, $created_at, $updated_at
                );
                """;
            command.Parameters.AddWithValue("$order_id", order.OrderId);
            command.Parameters.AddWithValue("$synced_at", syncedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$symbol", order.Symbol);
            command.Parameters.AddWithValue("$side", order.Side);
            command.Parameters.AddWithValue("$type", order.Type);
            command.Parameters.AddWithValue("$status", order.Status);
            command.Parameters.AddWithValue("$price", order.Price);
            command.Parameters.AddWithValue("$original_quantity", order.OriginalQuantity);
            command.Parameters.AddWithValue("$executed_quantity", order.ExecutedQuantity);
            command.Parameters.AddWithValue("$created_at", order.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$updated_at", order.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    private static void DeleteRowsOlderThan(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        DateTimeOffset cutoff)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {table} WHERE synced_at < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }
}

public sealed record BinanceCachedAssetSnapshot(
    string Source,
    string Asset,
    string UnderlyingAsset,
    decimal FreeAmount,
    decimal LockedAmount,
    decimal TotalAmount,
    decimal? PriceUsdt,
    decimal? ValueUsdt,
    string Status);

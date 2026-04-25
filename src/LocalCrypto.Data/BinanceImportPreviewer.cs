using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace LocalCrypto.Data;

public sealed class BinanceImportPreviewer
{
    private static readonly string[] RequiredColumns =
    [
        "Identifiant utilisateur",
        "Duree",
        "Compte",
        "Operation",
        "Jeton",
        "Change",
        "Remarque"
    ];

    public BinanceImportPreview Preview(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Fichier Binance introuvable.", filePath);
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".csv" && TryReadOrderCsv(filePath, out var orderCsvPreview))
        {
            return orderCsvPreview;
        }

        if (extension == ".xlsx" && TryReadOrderXlsx(filePath, out var orderXlsxPreview))
        {
            return orderXlsxPreview;
        }

        var rows = extension switch
        {
            ".csv" => ReadCsv(filePath),
            ".xlsx" => ReadXlsx(filePath),
            _ => throw new InvalidOperationException("Format non supporte. Utilise un export Binance CSV ou XLSX.")
        };

        return BuildPreview(filePath, rows);
    }

    private static BinanceImportPreview BuildPreview(string filePath, IReadOnlyList<Dictionary<string, string>> sourceRows)
    {
        var parsedRows = sourceRows
            .Select((row, index) => new ParsedBinanceRow(BuildRow(index + 1, row), Get(row, "Remarque")))
            .ToList();
        var rows = parsedRows.Select(row => row.Row).ToList();
        var events = BuildEvents(parsedRows);
        var summaries = rows
            .GroupBy(row => row.Operation)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BinanceOperationSummary(group.Key, group.Count()))
            .ToList();

        return new BinanceImportPreview(
            filePath,
            rows.Count,
            rows.Count(row => row.Status == BinanceImportStatus.Importable),
            rows.Count(row => row.Status == BinanceImportStatus.Pending),
            rows.Count(row => row.Status == BinanceImportStatus.Ignored),
            rows.Count(row => row.Status == BinanceImportStatus.Rejected),
            rows,
            events,
            summaries);
    }

    private static IReadOnlyList<BinanceImportEvent> BuildEvents(IReadOnlyList<ParsedBinanceRow> parsedRows)
    {
        var events = new List<BinanceImportEvent>();

        var tradeGroups = parsedRows
            .Where(row => row.Row.Category == BinanceImportCategory.TradeLeg)
            .GroupBy(TradeGroupKey)
            .OrderBy(group => group.Min(row => row.Row.ExecutedAt));

        foreach (var group in tradeGroups)
        {
            events.Add(BuildTradeEvent(events.Count + 1, group.Select(row => row.Row).ToList()));
        }

        var aggregateGroups = parsedRows
            .Where(row => row.Row.Category != BinanceImportCategory.TradeLeg)
            .GroupBy(row => new { row.Row.Category, row.Row.Operation, row.Row.Asset })
            .OrderBy(group => group.Key.Category)
            .ThenByDescending(group => group.Count())
            .ThenBy(group => group.Key.Operation, StringComparer.OrdinalIgnoreCase);

        foreach (var group in aggregateGroups)
        {
            events.Add(BuildAggregateEvent(events.Count + 1, group.Select(row => row.Row).ToList()));
        }

        return events;
    }

    private static string TradeGroupKey(ParsedBinanceRow row)
    {
        if (row.Row.Operation == "Buy Crypto With Fiat" && !string.IsNullOrWhiteSpace(row.RawRemark))
        {
            return $"FIAT|{row.RawRemark}";
        }

        if (row.Row.ExecutedAt is not null)
        {
            return $"TIME|{row.Row.Account}|{row.Row.ExecutedAt.Value.ToString("O", CultureInfo.InvariantCulture)}";
        }

        return $"LINE|{row.Row.LineNumber}";
    }

    private static BinanceImportEvent BuildTradeEvent(int eventNumber, IReadOnlyList<BinanceImportRow> rows)
    {
        var hasBuy = rows.Any(row => row.Operation is "Transaction Buy" or "Buy Crypto With Fiat");
        var hasSell = rows.Any(row => row.Operation == "Transaction Sold");
        var hasConvert = rows.Any(row => row.Operation == "Binance Convert");
        var kind = hasSell ? "SELL" : hasConvert ? "CONVERT" : hasBuy ? "BUY" : "TRADE";
        var date = rows.Min(row => row.ExecutedAt);
        var feeRows = rows.Where(row => row.Operation == "Transaction Fee").ToList();
        var feeAmount = feeRows.Sum(row => Math.Abs(row.Amount ?? 0m));
        var feeCurrency = string.Join("/", feeRows.Select(row => row.Asset).Distinct(StringComparer.OrdinalIgnoreCase));

        var assetRow = kind switch
        {
            "SELL" => rows.FirstOrDefault(row => row.Operation == "Transaction Sold"),
            "CONVERT" => rows.FirstOrDefault(row => row.Amount > 0m),
            _ => rows.FirstOrDefault(row => row.Amount > 0m && !IsQuoteAsset(row.Asset)) ?? rows.FirstOrDefault(row => row.Amount > 0m)
        };
        var quoteRow = kind switch
        {
            "SELL" => rows.FirstOrDefault(row => row.Operation == "Transaction Revenue") ?? rows.FirstOrDefault(row => row.Amount > 0m && IsQuoteAsset(row.Asset)),
            _ => rows.FirstOrDefault(row => row.Operation == "Transaction Spend") ?? rows.FirstOrDefault(row => row.Amount < 0m && row.Operation != "Transaction Fee")
        };

        var status = assetRow is not null && quoteRow is not null
            ? BinanceImportStatus.Importable
            : BinanceImportStatus.Pending;
        var reason = status == BinanceImportStatus.Importable
            ? "Evenement pret pour mapping ledger apres validation."
            : "Groupe incomplet: a verifier avant import.";

        return new BinanceImportEvent(
            eventNumber,
            date,
            kind,
            assetRow?.Asset ?? "-",
            assetRow?.Amount is null ? null : Math.Abs(assetRow.Amount.Value),
            quoteRow?.Asset ?? "-",
            quoteRow?.Amount is null ? null : Math.Abs(quoteRow.Amount.Value),
            UnitPrice(assetRow?.Amount, quoteRow?.Amount),
            feeAmount == 0m ? null : feeAmount,
            string.IsNullOrWhiteSpace(feeCurrency) ? "-" : feeCurrency,
            rows.Count,
            BinanceImportCategory.TradeLeg,
            status,
            reason);
    }

    private static BinanceImportEvent BuildAggregateEvent(int eventNumber, IReadOnlyList<BinanceImportRow> rows)
    {
        var category = rows[0].Category;
        var amount = rows.Sum(row => row.Amount ?? 0m);
        var status = rows.Any(row => row.Status == BinanceImportStatus.Rejected)
            ? BinanceImportStatus.Rejected
            : category == BinanceImportCategory.InternalMovement
                ? BinanceImportStatus.Ignored
                : BinanceImportStatus.Pending;

        return new BinanceImportEvent(
            eventNumber,
            rows.Min(row => row.ExecutedAt),
            EventKind(category),
            rows[0].Asset,
            amount,
            "-",
            null,
            null,
            null,
            "-",
            rows.Count,
            category,
            status,
            category switch
            {
                BinanceImportCategory.Reward => "Revenus/rewards agreges, a confirmer avant ecriture.",
                BinanceImportCategory.InternalMovement => "Mouvements internes agreges, ignores pour le PnL.",
                BinanceImportCategory.CashMovement => "Mouvement cash a garder hors PnL trade.",
                _ => "Operation a mapper plus tard."
            });
    }

    private static bool IsQuoteAsset(string asset) =>
        asset.Equals("EUR", StringComparison.OrdinalIgnoreCase) ||
        asset.Equals("USD", StringComparison.OrdinalIgnoreCase) ||
        asset.Equals("USDC", StringComparison.OrdinalIgnoreCase) ||
        asset.Equals("USDT", StringComparison.OrdinalIgnoreCase);

    private static string EventKind(BinanceImportCategory category) =>
        category switch
        {
            BinanceImportCategory.Reward => "REWARD",
            BinanceImportCategory.InternalMovement => "INTERNAL",
            BinanceImportCategory.CashMovement => "CASH",
            _ => "UNKNOWN"
        };

    private static decimal? UnitPrice(decimal? quantity, decimal? quoteAmount)
    {
        if (quantity is null || quoteAmount is null || quantity == 0m)
        {
            return null;
        }

        return Math.Abs(quoteAmount.Value) / Math.Abs(quantity.Value);
    }

    private static BinanceImportRow BuildRow(int lineNumber, IReadOnlyDictionary<string, string> row)
    {
        var account = Get(row, "Compte");
        var operation = Get(row, "Operation");
        var asset = Get(row, "Jeton").ToUpperInvariant();
        var rawAmount = Get(row, "Change");
        var rawDate = Get(row, "Duree");
        var remark = Get(row, "Remarque");

        var parsedDate = ParseDate(rawDate);
        var parsedAmount = ParseAmount(rawAmount);
        var category = Classify(operation);
        var status = StatusFor(category, parsedDate, parsedAmount);
        var reason = ReasonFor(category, parsedDate, parsedAmount);

        return new BinanceImportRow(
            lineNumber,
            parsedDate,
            account,
            operation,
            asset,
            parsedAmount,
            RemarkKind(remark),
            category,
            status,
            reason);
    }

    private static BinanceImportCategory Classify(string operation)
    {
        var normalized = operation.Trim();

        return normalized switch
        {
            "Transaction Buy" or "Transaction Spend" or "Transaction Fee" or "Transaction Sold" or "Transaction Revenue" or
                "Buy Crypto With Fiat" or "Binance Convert" => BinanceImportCategory.TradeLeg,
            "Simple Earn Flexible Interest" or "Simple Earn Locked Rewards" or "Alpha 2.0 - Transaction Revenue" or
                "Alpha 2.0 - Refund" => BinanceImportCategory.Reward,
            "Simple Earn Flexible Subscription" or "Simple Earn Flexible Redemption" or "Simple Earn Locked Subscription" or
                "Alpha 2.0 - Asset Freeze" or "Alpha 2.0 - Asset Unfreeze" or
                "Transfer Between Main and Funding Wallet" or "Transfer Funds to Spot" or
                "Transfer Funds to Funding Wallet" => BinanceImportCategory.InternalMovement,
            "Deposit" => BinanceImportCategory.CashMovement,
            _ => BinanceImportCategory.Unknown
        };
    }

    private static BinanceImportStatus StatusFor(BinanceImportCategory category, DateTimeOffset? parsedDate, decimal? parsedAmount)
    {
        if (parsedDate is null || parsedAmount is null)
        {
            return BinanceImportStatus.Rejected;
        }

        return category switch
        {
            BinanceImportCategory.TradeLeg => BinanceImportStatus.Importable,
            BinanceImportCategory.Reward => BinanceImportStatus.Pending,
            BinanceImportCategory.InternalMovement => BinanceImportStatus.Ignored,
            BinanceImportCategory.CashMovement => BinanceImportStatus.Pending,
            _ => BinanceImportStatus.Pending
        };
    }

    private static string ReasonFor(BinanceImportCategory category, DateTimeOffset? parsedDate, decimal? parsedAmount)
    {
        if (parsedDate is null)
        {
            return "Date illisible.";
        }

        if (parsedAmount is null)
        {
            return "Montant illisible.";
        }

        return category switch
        {
            BinanceImportCategory.TradeLeg => "Ligne de trade a grouper avant import.",
            BinanceImportCategory.Reward => "Revenu/reward a confirmer avant ecriture.",
            BinanceImportCategory.InternalMovement => "Mouvement interne ignore pour le PnL.",
            BinanceImportCategory.CashMovement => "Depot/retrait a garder hors PnL trade.",
            _ => "Operation Binance non encore mappee."
        };
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        if (DateTime.TryParseExact(value.Trim(), "yy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) ||
            DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
        {
            return new DateTimeOffset(parsed);
        }

        return null;
    }

    private static decimal? ParseAmount(string value)
    {
        var normalized = value.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string RemarkKind(string remark)
    {
        if (string.IsNullOrWhiteSpace(remark))
        {
            return string.Empty;
        }

        return remark.Split('/')[0].Trim();
    }

    private static string Get(IReadOnlyDictionary<string, string> row, string column) =>
        row.TryGetValue(column, out var value) ? value.Trim() : string.Empty;

    private static IReadOnlyList<Dictionary<string, string>> ReadCsv(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length == 0)
        {
            return [];
        }

        var headers = NormalizeHeaders(ParseCsvLine(lines[0]));
        ValidateHeaders(headers);

        var rows = new List<Dictionary<string, string>>();
        for (var index = 1; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            var values = ParseCsvLine(lines[index]);
            rows.Add(ToRow(headers, values));
        }

        return rows;
    }

    private static IReadOnlyList<Dictionary<string, string>> ReadXlsx(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidOperationException("Feuille XLSX Binance introuvable.");

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rawRows = document.Descendants(ns + "row")
            .Select(row => ReadXlsxRow(row, ns, sharedStrings))
            .Where(row => row.Count > 0)
            .ToList();

        var headerIndex = rawRows.FindIndex(row => NormalizeHeader(row.Values).Contains("Operation") && NormalizeHeader(row.Values).Contains("Jeton"));
        if (headerIndex < 0)
        {
            throw new InvalidOperationException("En-tetes Binance introuvables dans le XLSX.");
        }

        var headerCells = rawRows[headerIndex];
        var headers = NormalizeHeaders(headerCells.OrderBy(cell => cell.Key).Select(cell => cell.Value).ToList());
        ValidateHeaders(headers);

        var headerColumns = headerCells.OrderBy(cell => cell.Key).Select(cell => cell.Key).ToList();
        var rows = new List<Dictionary<string, string>>();
        foreach (var rawRow in rawRows.Skip(headerIndex + 1))
        {
            var values = headerColumns.Select(column => rawRow.TryGetValue(column, out var value) ? value : string.Empty).ToList();
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(ToRow(headers, values));
        }

        return rows;
    }

    private static bool TryReadOrderCsv(string filePath, out BinanceImportPreview preview)
    {
        preview = default!;
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        if (lines.Length == 0)
        {
            return false;
        }

        var headers = NormalizeHeaders(ParseCsvLine(lines[0]));
        if (!IsAlphaOrderHeader(headers) && !IsSpotOrderHeader(headers))
        {
            return false;
        }

        var rows = new List<Dictionary<string, string>>();
        for (var index = 1; index < lines.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(lines[index]))
            {
                rows.Add(ToRow(headers, ParseCsvLine(lines[index])));
            }
        }

        preview = BuildOrderPreview(filePath, rows, isAlphaOrder: IsAlphaOrderHeader(headers));
        return true;
    }

    private static bool TryReadOrderXlsx(string filePath, out BinanceImportPreview preview)
    {
        preview = default!;

        using var archive = ZipFile.OpenRead(filePath);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        if (sheetEntry is null)
        {
            return false;
        }

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rawRows = document.Descendants(ns + "row")
            .Select(row => ReadXlsxRow(row, ns, sharedStrings))
            .Where(row => row.Count > 0)
            .ToList();

        var headerIndex = rawRows.FindIndex(row =>
        {
            var headers = NormalizeHeader(row.Values);
            return IsSpotOrderHeader(headers) || IsAlphaOrderHeader(headers);
        });

        if (headerIndex < 0)
        {
            return false;
        }

        var headerCells = rawRows[headerIndex].OrderBy(cell => cell.Key).ToList();
        var headers = NormalizeHeaders(headerCells.Select(cell => cell.Value).ToList());
        var columns = headerCells.Select(cell => cell.Key).ToList();
        var rows = new List<Dictionary<string, string>>();

        foreach (var rawRow in rawRows.Skip(headerIndex + 1))
        {
            var values = columns.Select(column => rawRow.TryGetValue(column, out var value) ? value : string.Empty).ToList();
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(ToRow(headers, values));
        }

        preview = BuildOrderPreview(filePath, rows, isAlphaOrder: IsAlphaOrderHeader(headers));
        return true;
    }

    private static BinanceImportPreview BuildOrderPreview(string filePath, IReadOnlyList<Dictionary<string, string>> rows, bool isAlphaOrder)
    {
        var events = new List<BinanceImportEvent>();
        foreach (var row in rows)
        {
            var status = Get(row, "Statut");
            if (!status.Equals("FILLED", StringComparison.OrdinalIgnoreCase) && !status.Equals("Filled", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var side = Get(row, isAlphaOrder ? "Direction" : "Cote").ToUpperInvariant();
            var executed = isAlphaOrder
                ? ParseAssetAmount(Get(row, "Execute"))
                : ParseAssetAmount(Get(row, "Execute2"));
            var total = isAlphaOrder
                ? ParseAssetAmount(Get(row, "Total"))
                : ParseAssetAmount(Get(row, "Trading total3"));
            var averagePrice = isAlphaOrder
                ? ParseAssetAmount(Get(row, "Prix moyen de trading")).Amount
                : ParseAmount(Get(row, "Prix moyen"));

            events.Add(new BinanceImportEvent(
                events.Count + 1,
                ParseDate(Get(row, "Duree")),
                side == "SELL" ? "SELL" : "BUY",
                executed.Asset,
                executed.Amount,
                total.Asset,
                total.Amount,
                averagePrice,
                null,
                "-",
                1,
                BinanceImportCategory.TradeLeg,
                executed.Amount is > 0m && total.Amount is > 0m ? BinanceImportStatus.Importable : BinanceImportStatus.Pending,
                isAlphaOrder ? "Ordre Alpha execute: prix moyen Binance disponible." : "Ordre spot execute: prix moyen Binance disponible."));
        }

        var sourceRows = rows.Count;
        return new BinanceImportPreview(
            filePath,
            sourceRows,
            events.Count(importEvent => importEvent.Status == BinanceImportStatus.Importable),
            events.Count(importEvent => importEvent.Status == BinanceImportStatus.Pending),
            0,
            0,
            [],
            events,
            [new BinanceOperationSummary(isAlphaOrder ? "Alpha orders filled" : "Spot orders filled", events.Count)]);
    }

    private static bool IsSpotOrderHeader(IReadOnlyList<string> headers) =>
        headers.Contains("Paire") && headers.Contains("Cote") && headers.Contains("Prix moyen") && headers.Contains("Trading total3");

    private static bool IsAlphaOrderHeader(IReadOnlyList<string> headers) =>
        headers.Contains("Direction") && headers.Contains("Actif de base") && headers.Contains("Actif de cotation") &&
        headers.Contains("Prix moyen de trading") && headers.Contains("Execute") && headers.Contains("Total");

    private static ParsedAssetAmount ParseAssetAmount(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return new ParsedAssetAmount("-", null);
        }

        var index = trimmed.Length - 1;
        while (index >= 0 && char.IsLetter(trimmed[index]))
        {
            index--;
        }

        var amountPart = trimmed[..(index + 1)];
        var assetPart = trimmed[(index + 1)..].ToUpperInvariant();
        return new ParsedAssetAmount(string.IsNullOrWhiteSpace(assetPart) ? "-" : assetPart, ParseAmount(amountPart));
    }

    private static Dictionary<int, string> ReadXlsxRow(XElement row, XNamespace ns, IReadOnlyList<string> sharedStrings)
    {
        var values = new Dictionary<int, string>();
        foreach (var cell in row.Elements(ns + "c"))
        {
            var reference = cell.Attribute("r")?.Value;
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            var columnIndex = ColumnIndex(reference);
            var type = cell.Attribute("t")?.Value;
            var value = type == "inlineStr"
                ? cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? string.Empty
                : cell.Element(ns + "v")?.Value ?? string.Empty;

            if (type == "s" && int.TryParse(value, CultureInfo.InvariantCulture, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
            {
                value = sharedStrings[sharedIndex];
            }

            values[columnIndex] = value.Trim();
        }

        return values;
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToList();
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
        {
            index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
        }

        return index;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static Dictionary<string, string> ToRow(IReadOnlyList<string> headers, IReadOnlyList<string> values)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            row[headers[index]] = index < values.Count ? values[index] : string.Empty;
        }

        return row;
    }

    private static List<string> NormalizeHeaders(IReadOnlyList<string> headers) =>
        headers.Select(NormalizeHeader).ToList();

    private static List<string> NormalizeHeader(IEnumerable<string> headers) =>
        headers.Select(NormalizeHeader).ToList();

    private static string NormalizeHeader(string header)
    {
        var normalized = header.Trim().TrimStart('\uFEFF');
        normalized = normalized
            .Replace('é', 'e')
            .Replace('è', 'e')
            .Replace('ê', 'e')
            .Replace('É', 'E')
            .Replace('È', 'E')
            .Replace('Ê', 'E');
        normalized = normalized
            .Replace('é', 'e')
            .Replace('è', 'e')
            .Replace('ê', 'e')
            .Replace('ô', 'o')
            .Replace('¹', '1')
            .Replace('²', '2')
            .Replace('³', '3');
        normalized = string.Concat(normalized
            .Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));
        return normalized;
    }

    private static void ValidateHeaders(IReadOnlyList<string> headers)
    {
        foreach (var requiredColumn in RequiredColumns)
        {
            if (!headers.Contains(requiredColumn, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Colonne Binance manquante: {requiredColumn}.");
            }
        }
    }
}

public sealed record BinanceImportPreview(
    string FilePath,
    int TotalRows,
    int ImportableRows,
    int PendingRows,
    int IgnoredRows,
    int RejectedRows,
    IReadOnlyList<BinanceImportRow> Rows,
    IReadOnlyList<BinanceImportEvent> Events,
    IReadOnlyList<BinanceOperationSummary> OperationSummaries);

public sealed record BinanceImportRow(
    int LineNumber,
    DateTimeOffset? ExecutedAt,
    string Account,
    string Operation,
    string Asset,
    decimal? Amount,
    string RemarkKind,
    BinanceImportCategory Category,
    BinanceImportStatus Status,
    string Reason);

public sealed record BinanceOperationSummary(string Operation, int Count);

public sealed record BinanceImportEvent(
    int EventNumber,
    DateTimeOffset? ExecutedAt,
    string Kind,
    string Asset,
    decimal? Quantity,
    string QuoteCurrency,
    decimal? QuoteAmount,
    decimal? UnitPrice,
    decimal? FeeAmount,
    string FeeCurrency,
    int SourceRows,
    BinanceImportCategory Category,
    BinanceImportStatus Status,
    string Reason);

internal sealed record ParsedBinanceRow(BinanceImportRow Row, string RawRemark);

internal sealed record ParsedAssetAmount(string Asset, decimal? Amount);

public enum BinanceImportCategory
{
    TradeLeg,
    Reward,
    InternalMovement,
    CashMovement,
    Unknown
}

public enum BinanceImportStatus
{
    Importable,
    Pending,
    Ignored,
    Rejected
}

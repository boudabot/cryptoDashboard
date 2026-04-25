using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LocalCrypto.Core;
using LocalCrypto.Data;
using Microsoft.Win32;

namespace LocalCrypto.App;

public partial class MainWindow : Window
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SqliteLedgerStore _store;
    private readonly BinanceImportPreviewer _binanceImportPreviewer = new();
    private readonly BinanceLedgerMapper _binanceLedgerMapper = new();
    private readonly BinanceImportReconciler _binanceImportReconciler = new();
    private readonly List<BinanceImportEvent> _binanceImportEvents = [];
    private readonly HashSet<string> _loadedImportFiles = new(StringComparer.OrdinalIgnoreCase);
    private string _importViewFilter = "Tout";
    private int _loadedImportSourceRows;

    public MainWindow()
    {
        InitializeComponent();
        _store = SqliteLedgerStore.OpenDefault();
        DatabasePathText.Text = _store.DatabasePath;
        DataFileText.Text = _store.DatabasePath;
        RefreshPortfolio();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshPortfolio();

    private void NavigateSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string target)
        {
            return;
        }

        FrameworkElement? section = target switch
        {
            "Dashboard" => DashboardAnchor,
            "Positions" => PositionsAnchor,
            "Imports" => ImportsAnchor,
            "Data" => DataAnchor,
            _ => null
        };

        section?.BringIntoView();
    }

    private void LoadBinanceImport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Charger un export Binance",
                Filter = "Exports Binance (*.csv;*.xlsx)|*.csv;*.xlsx|CSV (*.csv)|*.csv|Excel (*.xlsx)|*.xlsx",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            if (dialog.FileNames.Length > 10)
            {
                ImportFeedbackText.Text = "Import limite a 10 fichiers a la fois pour garder une reconciliation lisible.";
                return;
            }

            var addedFiles = 0;
            var eventCountBefore = _binanceImportEvents.Count;
            var emptyExports = 0;

            foreach (var selectedFile in dialog.FileNames)
            {
                var fullPath = Path.GetFullPath(selectedFile);
                if (!_loadedImportFiles.Add(fullPath))
                {
                    continue;
                }

                var preview = _binanceImportPreviewer.Preview(fullPath);
                _loadedImportSourceRows += preview.TotalRows;
                _binanceImportEvents.AddRange(preview.Events);
                if (preview.Events.Count == 0 && preview.IgnoredRows > 0)
                {
                    emptyExports++;
                }
                addedFiles++;
            }

            var duplicateEvents = ReconcileLoadedImportEvents();
            var addedEvents = Math.Max(0, _binanceImportEvents.Count - eventCountBefore);
            ImportFeedbackText.Text = addedFiles == 0
                ? "Aucun nouveau fichier ajoute."
                : emptyExports > 0 && addedEvents == 0
                    ? $"{addedFiles} fichier(s) ajoute(s), export vide detecte, rien a importer. Aucune ecriture SQLite."
                    : $"{addedFiles} fichier(s) ajoute(s), {addedEvents} evenement(s) conserves, {duplicateEvents} doublon(s) probables en quarantaine. Aucune ecriture SQLite.";
            RefreshImportDashboard();
        }
        catch (Exception exception)
        {
            ImportFeedbackText.Text = exception.Message;
        }
    }

    private void ClearBinanceImports_Click(object sender, RoutedEventArgs e)
    {
        _loadedImportFiles.Clear();
        _binanceImportEvents.Clear();
        _loadedImportSourceRows = 0;
        ImportFeedbackText.Text = "Preview Binance reinitialisee.";
        RefreshImportDashboard();
    }

    private int ReconcileLoadedImportEvents()
    {
        var reconciliation = _binanceImportReconciler.Reconcile(_binanceImportEvents);
        _binanceImportEvents.Clear();
        _binanceImportEvents.AddRange(reconciliation.Accepted);
        RenumberImportEvents();
        return reconciliation.Duplicates.Count;
    }

    private void WriteImportableTrades_Click(object sender, RoutedEventArgs e)
    {
        var candidates = _binanceLedgerMapper.Map(_binanceImportEvents);
        var writable = candidates.Where(candidate => candidate.CanWrite && candidate.Transaction is not null).ToList();
        var blocked = candidates.Count - writable.Count;

        if (writable.Count == 0)
        {
            ImportFeedbackText.Text = blocked == 0
                ? "Aucun trade importable charge."
                : $"{blocked} evenement(s) restent a confirmer avant ecriture.";
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Ecrire {writable.Count} trade(s) BUY/SELL dans le ledger SQLite ? {blocked} evenement(s) resteront en preview.",
            "Valider l'import Binance",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var written = 0;
        var duplicates = 0;
        foreach (var candidate in writable)
        {
            try
            {
                _store.AddTransaction(candidate.Transaction!);
                written++;
            }
            catch (DuplicateTransactionException)
            {
                duplicates++;
            }
        }

        ImportFeedbackText.Text = $"{written} trade(s) ecrit(s), {duplicates} doublon(s) deja presents, {blocked} evenement(s) gardes en quarantaine.";
        RefreshPortfolio();
    }

    private void ImportViewFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filter)
        {
            _importViewFilter = filter;
            ApplyImportFilters();
        }
    }

    private void ImportAsset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string asset)
        {
            ImportAssetFilterBox.Text = asset;
            _importViewFilter = "Trades";
            ImportTabs.SelectedItem = ImportOrdersTab;
            ApplyImportFilters();
        }
    }

    private void ImportFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyImportFilters();

    private void ApplyImportFilters()
    {
        if (ImportPreviewGrid is null)
        {
            return;
        }

        var rows = FilterImportEvents(_binanceImportEvents);

        ImportPreviewGrid.ItemsSource = rows.Select(ToBinancePreviewRow).ToList();
    }

    private IReadOnlyList<BinanceImportEvent> FilterImportEvents(IReadOnlyList<BinanceImportEvent> source)
    {
        IEnumerable<BinanceImportEvent> rows = source;
        var assetFilter = ImportAssetFilterBox?.Text.Trim().ToUpperInvariant() ?? string.Empty;

        rows = _importViewFilter switch
        {
            "Trades" => rows.Where(row => row.Category == BinanceImportCategory.TradeLeg).ToList(),
            "Earn / Rewards" => rows.Where(row => row.Category == BinanceImportCategory.Reward).ToList(),
            "Internes" => rows.Where(row => row.Category == BinanceImportCategory.InternalMovement).ToList(),
            "A confirmer" => rows.Where(row => row.Status == BinanceImportStatus.Pending).ToList(),
            "Rejets" => rows.Where(row => row.Status == BinanceImportStatus.Rejected).ToList(),
            _ => rows
        };

        if (!string.IsNullOrWhiteSpace(assetFilter))
        {
            rows = rows.Where(row => row.Asset.Contains(assetFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return rows.ToList();
    }

    private void RefreshImportDashboard()
    {
        ImportVolumeText.Text = Money(_binanceImportEvents.Where(row => row.Category == BinanceImportCategory.TradeLeg).Sum(row => row.QuoteAmount ?? 0m), "EUR/USD");
        ImportEventCountText.Text = _binanceImportEvents.Count.ToString(FrenchCulture);
        ImportTradeCountText.Text = _binanceImportEvents.Count(row => row.Category == BinanceImportCategory.TradeLeg).ToString(FrenchCulture);
        ImportPendingCountText.Text = _binanceImportEvents.Count(row => row.Status == BinanceImportStatus.Pending).ToString(FrenchCulture);
        ImportAssetCountText.Text = _binanceImportEvents.Select(row => row.Asset).Where(asset => asset != "-").Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(FrenchCulture);
        ImportFileText.Text = _loadedImportFiles.Count == 0
            ? "Aucun export charge."
            : $"{_loadedImportFiles.Count} export(s), {_loadedImportSourceRows.ToString(FrenchCulture)} lignes source.";

        RefreshImportChart(_binanceImportEvents);
        RefreshImportAssets(_binanceImportEvents);
        RefreshRecentOrders(_binanceImportEvents);
        UpdateDataConfidence(PortfolioCalculator.Calculate(_store.ListTransactions()));
        ApplyImportFilters();
    }

    private void RefreshImportAssets(IReadOnlyList<BinanceImportEvent> events)
    {
        var assets = events
            .Where(row => row.Asset != "-")
            .GroupBy(row => row.Asset, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count(row => row.Category == BinanceImportCategory.TradeLeg))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ImportAssetRow(
                group.Key,
                LogoFor(group.Key),
                AccentFor(group.Key),
                AssetDescription(group.Key),
                FormatNumber(group.Sum(row => SignedQuantity(row))),
                group.Count(row => row.Category == BinanceImportCategory.TradeLeg).ToString(FrenchCulture),
                group.Count(row => row.Status == BinanceImportStatus.Pending).ToString(FrenchCulture)))
            .ToList();

        ImportAssetItems.ItemsSource = assets;
    }

    private void RefreshRecentOrders(IReadOnlyList<BinanceImportEvent> events)
    {
        ImportRecentOrdersItems.ItemsSource = events
            .Where(row => row.Category == BinanceImportCategory.TradeLeg)
            .OrderByDescending(row => row.ExecutedAt)
            .Take(8)
            .Select(row => new RecentOrderRow(
                LogoFor(row.Asset),
                AccentFor(row.Asset),
                $"{row.Kind} {row.Asset}",
                $"{(row.Quantity.HasValue ? FormatNumber(row.Quantity.Value) : "-")} contre {(row.QuoteAmount.HasValue ? $"{FormatNumber(row.QuoteAmount.Value)} {row.QuoteCurrency}" : "-")} | prix {(row.UnitPrice.HasValue ? FormatNumber(row.UnitPrice.Value) : "-")}",
                StatusText(row.Status),
                row.Status == BinanceImportStatus.Importable ? "#22C55E" : "#FBBF24"))
            .ToList();
    }

    private void RenumberImportEvents()
    {
        var renumbered = _binanceImportEvents
            .Select((importEvent, index) => importEvent with { EventNumber = index + 1 })
            .ToList();
        _binanceImportEvents.Clear();
        _binanceImportEvents.AddRange(renumbered);
    }

    private BinancePreviewRow ToBinancePreviewRow(BinanceImportEvent row) =>
        new(
            row.EventNumber.ToString(FrenchCulture),
            row.ExecutedAt?.ToLocalTime().ToString("g", FrenchCulture) ?? "-",
            row.Kind,
            row.Asset,
            row.Quantity.HasValue ? FormatNumber(row.Quantity.Value) : "-",
            row.QuoteAmount.HasValue ? $"{FormatNumber(row.QuoteAmount.Value)} {row.QuoteCurrency}" : "-",
            row.UnitPrice.HasValue ? $"{FormatNumber(row.UnitPrice.Value)} {row.QuoteCurrency}" : "-",
            row.FeeAmount.HasValue ? $"{FormatNumber(row.FeeAmount.Value)} {row.FeeCurrency}" : "-",
            row.SourceRows.ToString(FrenchCulture),
            row.SourceKind,
            string.IsNullOrWhiteSpace(row.ExternalId) ? "-" : row.ExternalId,
            StatusText(row.Status),
            row.Reason);

    private void RefreshImportChart(IReadOnlyList<BinanceImportEvent> events)
    {
        if (ImportChartItems is null)
        {
            return;
        }

        var chartRows = new[]
        {
            ChartRow("Trades", events.Count(row => row.Category == BinanceImportCategory.TradeLeg), "#22C55E"),
            ChartRow("Rewards", events.Count(row => row.Category == BinanceImportCategory.Reward), "#38BDF8"),
            ChartRow("Internes", events.Count(row => row.Category == BinanceImportCategory.InternalMovement), "#94A3B8"),
            ChartRow("Cash", events.Count(row => row.Category == BinanceImportCategory.CashMovement), "#F59E0B"),
            ChartRow("Rejets", events.Count(row => row.Status == BinanceImportStatus.Rejected), "#EF4444")
        };
        var max = Math.Max(1, chartRows.Max(row => row.RawCount));

        ImportChartItems.ItemsSource = chartRows
            .Select(row => row with { Width = 360d * row.RawCount / max })
            .ToList();
    }

    private static ImportChartRow ChartRow(string label, int count, string color) =>
        new(label, count.ToString(FrenchCulture), count, 0d, color);

    private static decimal SignedQuantity(BinanceImportEvent importEvent)
    {
        var quantity = importEvent.Quantity ?? 0m;
        return importEvent.Kind == "SELL" ? -quantity : quantity;
    }

    private static string LogoFor(string asset) =>
        string.IsNullOrWhiteSpace(asset) || asset == "-"
            ? "?"
            : asset.Length <= 3 ? asset : asset[..3];

    private static string AccentFor(string asset) =>
        asset.ToUpperInvariant() switch
        {
            "BTC" => "#F7931A",
            "ETH" => "#8B9CFF",
            "USDC" => "#2775CA",
            "USDT" => "#26A17B",
            "SOL" => "#14F195",
            "OPN" or "OPG" => "#FF5A1F",
            "EUR" => "#F0B90B",
            _ => "#38BDF8"
        };

    private static string AssetDescription(string asset) =>
        asset.ToUpperInvariant() switch
        {
            "BTC" => "Bitcoin",
            "ETH" => "Ethereum",
            "USDC" => "USD Coin",
            "USDT" => "Tether USD",
            "SOL" => "Solana",
            "EUR" => "Euro cash",
            _ => "Actif Binance"
        };

    private void BackupDatabase_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Sauvegarder la base localCrypto",
                Filter = "SQLite (*.sqlite)|*.sqlite|Tous les fichiers (*.*)|*.*",
                FileName = $"localcrypto-backup-{DateTime.Now:yyyyMMdd-HHmmss}.sqlite"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            _store.BackupDatabase(dialog.FileName);
            DataFeedbackText.Text = $"Sauvegarde creee: {dialog.FileName}";
        }
        catch (Exception exception)
        {
            DataFeedbackText.Text = exception.Message;
        }
    }

    private void RestoreDatabase_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Restaurer une sauvegarde localCrypto",
                Filter = "SQLite (*.sqlite;*.db)|*.sqlite;*.db|Tous les fichiers (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var confirmation = MessageBox.Show(
                this,
                "Restaurer cette sauvegarde remplacera la base SQLite active. Continuer ?",
                "Confirmer la restauration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            _store.RestoreDatabase(dialog.FileName);
            DataFeedbackText.Text = $"Base restauree depuis: {dialog.FileName}";
            DataFeedbackText.Text = "Base restauree. Portefeuille recharge depuis la source SQLite.";
            RefreshPortfolio();
        }
        catch (Exception exception)
        {
            DataFeedbackText.Text = exception.Message;
        }
    }

    private void DeleteTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            "Supprimer cette transaction du ledger SQLite ? Les positions seront recalculees depuis le journal restant.",
            "Confirmer la suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        if (_store.DeleteTransaction(id))
        {
            DataFeedbackText.Text = "Ledger SQLite mis a jour.";
            RefreshPortfolio();
        }
        else
        {
            DataFeedbackText.Text = "Transaction introuvable. Rafraichis le journal.";
        }
    }

    private void ClearLedger_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "Vider toutes les transactions du ledger SQLite ? Fais une sauvegarde avant si tu veux revenir en arriere.",
            "Confirmer le reset ledger",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var deleted = _store.ClearTransactions();
        DataFeedbackText.Text = $"{deleted} transaction(s) supprimee(s). Le portefeuille est pret pour un import propre.";
        AssetTransactionsGrid.ItemsSource = null;
        AssetXrayTitleText.Text = "Asset X-Ray";
        AssetXraySubtitleText.Text = "Selectionne un actif pour voir les transactions qui expliquent la position.";
        AssetXrayConfidenceText.Text = string.Empty;
        AssetXrayQuantityText.Text = string.Empty;
        AssetXrayAverageText.Text = string.Empty;
        AssetXrayCostText.Text = string.Empty;
        AssetXrayPnlText.Text = string.Empty;
        RefreshPortfolio();
    }

    private void RefreshPortfolio()
    {
        var transactions = _store.ListTransactions();
        var portfolio = PortfolioCalculator.Calculate(transactions);

        TrackedBalanceText.Text = Money(portfolio.InvestedTotal, portfolio.BaseCurrency);
        InvestedText.Text = Money(portfolio.InvestedTotal, portfolio.BaseCurrency);
        RealizedPnlText.Text = Money(portfolio.RealizedPnlTotal, portfolio.BaseCurrency);
        FeesText.Text = Money(portfolio.TotalFees, portfolio.BaseCurrency);
        UpdateDataConfidence(portfolio);

        PositionsGrid.ItemsSource = portfolio.Positions.Select(position => new PositionRow(
            position.Symbol,
            FormatNumber(position.Quantity),
            Money(position.AverageCost, position.QuoteCurrency),
            Money(position.InvestedCost, position.QuoteCurrency),
            Money(position.RealizedPnl, position.QuoteCurrency),
            Money(position.Fees, position.QuoteCurrency))).ToList();

        TransactionsGrid.ItemsSource = transactions.Select(transaction => new TransactionRow(
            transaction.Id,
            transaction.ExecutedAt.ToLocalTime().ToString("g", FrenchCulture),
            transaction.Side.ToString(),
            transaction.Symbol,
            FormatNumber(transaction.Quantity),
            Money(transaction.UnitPrice, transaction.QuoteCurrency),
            Money(transaction.FeeAmount, transaction.FeeCurrency),
            transaction.Source,
            transaction.Note)).ToList();

        if (portfolio.Warnings.Count > 0)
        {
            DataFeedbackText.Text = string.Join(Environment.NewLine, portfolio.Warnings);
        }
    }

    private void PositionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PositionsGrid.SelectedItem is not PositionRow position)
        {
            return;
        }

        var transactions = _store.ListTransactions()
            .Where(transaction => transaction.Symbol.Equals(position.Symbol, StringComparison.OrdinalIgnoreCase))
            .ToList();

        AssetXrayTitleText.Text = $"{position.Symbol} X-Ray";
        AssetXraySubtitleText.Text = "Transactions ledger qui expliquent cette position.";
        AssetXrayConfidenceText.Text = transactions.Count == 0 ? "A verifier" : "Ledger";
        AssetXrayQuantityText.Text = $"Quantite: {position.Quantity}";
        AssetXrayAverageText.Text = $"Prix moyen: {position.AverageCost}";
        AssetXrayCostText.Text = $"Cout: {position.InvestedCost}";
        AssetXrayPnlText.Text = $"PnL realise: {position.RealizedPnl}";

        AssetTransactionsGrid.ItemsSource = transactions.Select(transaction => new TransactionRow(
            transaction.Id,
            transaction.ExecutedAt.ToLocalTime().ToString("g", FrenchCulture),
            transaction.Side.ToString(),
            transaction.Symbol,
            FormatNumber(transaction.Quantity),
            Money(transaction.UnitPrice, transaction.QuoteCurrency),
            Money(transaction.FeeAmount, transaction.FeeCurrency),
            transaction.Source,
            transaction.Note)).ToList();
    }

    private void UpdateDataConfidence(PortfolioSnapshot portfolio)
    {
        var pendingImports = _binanceImportEvents.Count(row => row.Status == BinanceImportStatus.Pending);
        var blockedImports = _binanceImportEvents.Count(row => row.Status == BinanceImportStatus.Rejected || row.Status == BinanceImportStatus.Ignored);
        var warnings = portfolio.Warnings.Count;

        if (warnings == 0 && pendingImports == 0)
        {
            DataConfidenceText.Text = "OK";
            DataConfidenceHintText.Text = $"{portfolio.Transactions.Count.ToString(FrenchCulture)} transaction(s) ledger.";
            return;
        }

        DataConfidenceText.Text = "Partiel";
        DataConfidenceHintText.Text = $"{warnings} alerte(s), {pendingImports} a confirmer, {blockedImports} hors ledger.";
    }

    private static string Money(decimal value, string currency) =>
        $"{FormatNumber(value)} {currency}";

    private static string FormatNumber(decimal value) =>
        value.ToString("0.########", FrenchCulture);

    private static string CategoryText(BinanceImportCategory category) =>
        category switch
        {
            BinanceImportCategory.TradeLeg => "Trade",
            BinanceImportCategory.Reward => "Reward",
            BinanceImportCategory.InternalMovement => "Interne",
            BinanceImportCategory.CashMovement => "Cash",
            _ => "Inconnu"
        };

    private static string StatusText(BinanceImportStatus status) =>
        status switch
        {
            BinanceImportStatus.Importable => "Groupable",
            BinanceImportStatus.Pending => "A confirmer",
            BinanceImportStatus.Ignored => "Ignore",
            BinanceImportStatus.Rejected => "Rejet",
            _ => status.ToString()
        };

    private sealed record PositionRow(
        string Symbol,
        string Quantity,
        string AverageCost,
        string InvestedCost,
        string RealizedPnl,
        string Fees);

    private sealed record TransactionRow(
        string Id,
        string ExecutedAt,
        string Side,
        string Symbol,
        string Quantity,
        string UnitPrice,
        string Fee,
        string Source,
        string Note);

    private sealed record BinancePreviewRow(
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

    private sealed record ImportChartRow(string Label, string Count, int RawCount, double Width, string Color);

    private sealed record ImportAssetRow(
        string Asset,
        string Logo,
        string Accent,
        string Description,
        string NetQuantity,
        string TradeCount,
        string PendingCount);

    private sealed record RecentOrderRow(
        string Logo,
        string Accent,
        string Title,
        string Subtitle,
        string Status,
        string StatusColor);
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using LocalCrypto.Core;
using LocalCrypto.Data;
using Microsoft.Win32;

namespace LocalCrypto.App;

public partial class MainWindow : Window
{
    private readonly SqliteLedgerStore _store;
    private readonly BinanceImportPreviewer _binanceImportPreviewer = new();
    private readonly BinanceLedgerMapper _binanceLedgerMapper = new();
    private readonly BinanceImportReconciler _binanceImportReconciler = new();
    private readonly List<BinanceImportEvent> _binanceImportEvents = [];
    private readonly List<BinanceImportDuplicate> _binanceImportDuplicates = [];
    private readonly HashSet<string> _loadedImportFiles = new(StringComparer.OrdinalIgnoreCase);
    private int _loadedImportSourceRows;

    public MainWindow()
    {
        InitializeComponent();
        _store = SqliteLedgerStore.OpenDefault();
        DatabasePathText.Text = _store.DatabasePath;
        DataView.SetDatabasePath(_store.DatabasePath);
        RefreshImportDashboard();
        RefreshPortfolio();
    }

    private void NavigateSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string target)
        {
            return;
        }

        FrameworkElement? section = target switch
        {
            "Dashboard" => DashboardView,
            "Positions" => PositionsView,
            "Imports" => ImportStudioView,
            "Data" => DataView,
            _ => null
        };

        section?.BringIntoView();
    }

    private void ImportStudioView_AddExportRequested(object? sender, EventArgs e)
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
                ImportStudioView.SetFeedback("Import limite a 10 fichiers a la fois pour garder une reconciliation lisible.");
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
            var feedback = addedFiles == 0
                ? "Aucun nouveau fichier ajoute."
                : emptyExports > 0 && addedEvents == 0
                    ? $"{addedFiles} fichier(s) ajoute(s), export vide detecte, rien a importer. Aucune ecriture SQLite."
                    : $"{addedFiles} fichier(s) ajoute(s), {addedEvents} evenement(s) conserves, {duplicateEvents} doublon(s) probables en quarantaine. Aucune ecriture SQLite.";
            ImportStudioView.SetFeedback(feedback);
            RefreshImportDashboard();
        }
        catch (Exception exception)
        {
            ImportStudioView.SetFeedback(exception.Message);
        }
    }

    private void ImportStudioView_ResetRequested(object? sender, EventArgs e)
    {
        _loadedImportFiles.Clear();
        _binanceImportEvents.Clear();
        _binanceImportDuplicates.Clear();
        _loadedImportSourceRows = 0;
        ImportStudioView.ClearAssetFilter();
        ImportStudioView.SetFeedback("Preview Binance reinitialisee.");
        RefreshImportDashboard();
    }

    private void ImportStudioView_ValidateTradesRequested(object? sender, EventArgs e)
    {
        var candidates = _binanceLedgerMapper.Map(_binanceImportEvents);
        var writable = candidates.Where(candidate => candidate.CanWrite && candidate.Transaction is not null).ToList();
        var blocked = candidates.Count - writable.Count;

        if (writable.Count == 0)
        {
            ImportStudioView.SetFeedback(blocked == 0
                ? "Aucun trade importable charge."
                : $"{blocked} evenement(s) restent a confirmer avant ecriture.");
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

        ImportStudioView.SetFeedback($"{written} trade(s) ecrit(s), {duplicates} doublon(s) deja presents, {blocked} evenement(s) gardes en preview/quarantaine.");
        RefreshPortfolio();
    }

    private void ImportStudioView_FiltersChanged(object? sender, EventArgs e) => ApplyImportFilters();

    private int ReconcileLoadedImportEvents()
    {
        var reconciliation = _binanceImportReconciler.Reconcile(_binanceImportEvents);
        _binanceImportEvents.Clear();
        _binanceImportEvents.AddRange(reconciliation.Accepted);
        _binanceImportDuplicates.Clear();
        _binanceImportDuplicates.AddRange(reconciliation.Duplicates);
        RenumberImportEvents();
        return reconciliation.Duplicates.Count;
    }

    private void RenumberImportEvents()
    {
        var renumbered = _binanceImportEvents
            .Select((importEvent, index) => importEvent with { EventNumber = index + 1 })
            .ToList();
        _binanceImportEvents.Clear();
        _binanceImportEvents.AddRange(renumbered);
    }

    private void RefreshImportDashboard()
    {
        ImportStudioView.SetSummary(ImportUiBuilder.BuildSummary(_binanceImportEvents, _binanceImportDuplicates));
        ImportStudioView.SetFileInfo(_loadedImportFiles.Count == 0
            ? "Aucun export charge. Les fichiers ajoutes restent en preview jusqu'a validation."
            : $"{_loadedImportFiles.Count} export(s), {_loadedImportSourceRows.ToString(UiFormatting.FrenchCulture)} lignes source.");

        ImportStudioView.SetCharts(ImportUiBuilder.BuildChartRows(_binanceImportEvents, _binanceImportDuplicates));
        ImportStudioView.SetAssetChips(ImportUiBuilder.BuildAssetChips(_binanceImportEvents, ImportStudioView.AssetFilter));
        ImportStudioView.SetAssets(ImportUiBuilder.BuildAssetRows(_binanceImportEvents));
        ImportStudioView.SetRecentOrders(ImportUiBuilder.BuildRecentOrders(_binanceImportEvents));
        ImportStudioView.SetQuarantineRows(ImportUiBuilder.BuildQuarantineRows(_binanceImportDuplicates));
        ApplyImportFilters();
        UpdateDataConfidence(PortfolioCalculator.Calculate(_store.ListTransactions()), _store.ListTransactions());
    }

    private void ApplyImportFilters()
    {
        var rows = ImportUiBuilder.FilterEvents(
            _binanceImportEvents,
            ImportStudioView.TypeFilter,
            ImportStudioView.StatusFilter,
            ImportStudioView.AssetFilter);
        ImportStudioView.SetPreviewRows(rows.Select(ImportUiBuilder.ToPreviewRow).ToList());
        ImportStudioView.SetAssetChips(ImportUiBuilder.BuildAssetChips(_binanceImportEvents, ImportStudioView.AssetFilter));
    }

    private void RefreshPortfolio()
    {
        var transactions = _store.ListTransactions();
        var portfolio = PortfolioCalculator.Calculate(transactions);

        DashboardView.SetMetrics(PortfolioUiBuilder.BuildDashboardMetrics(portfolio, transactions));
        UpdateDataConfidence(portfolio, transactions);

        var positionRows = portfolio.Positions
            .Select(position => PortfolioUiBuilder.ToPositionCardRow(position, portfolio, transactions))
            .ToList();
        PositionsView.SetPositions(positionRows);
        PositionsView.SetCharts(
            PortfolioUiBuilder.BuildCostChartRows(transactions),
            PortfolioUiBuilder.BuildVolumeChartRows(transactions),
            PortfolioUiBuilder.BuildPnlChartRows(portfolio, transactions));
        DataView.SetTransactions(PortfolioUiBuilder.BuildTransactionRows(transactions));

        if (portfolio.Warnings.Count > 0)
        {
            DataView.SetFeedback(string.Join(Environment.NewLine, portfolio.Warnings));
        }

        if (positionRows.Count == 0)
        {
            PositionsView.ResetAssetXray();
        }
        else
        {
            ShowAssetXray(positionRows[0].Symbol);
        }
    }

    private void PositionsView_AssetProofRequested(object? sender, string symbol) => ShowAssetXray(symbol);

    private void ShowAssetXray(string symbol)
    {
        var allTransactions = _store.ListTransactions();
        var portfolio = PortfolioCalculator.Calculate(allTransactions);
        var position = portfolio.Positions.FirstOrDefault(item => item.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        var transactions = allTransactions
            .Where(transaction => transaction.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (position is null)
        {
            PositionsView.ResetAssetXray();
            return;
        }

        PositionsView.SetAssetXray(PortfolioUiBuilder.ToAssetXrayModel(position, portfolio, transactions));
    }

    private void DataView_BackupRequested(object? sender, EventArgs e)
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
            DataView.SetFeedback($"Sauvegarde creee: {dialog.FileName}");
        }
        catch (Exception exception)
        {
            DataView.SetFeedback(exception.Message);
        }
    }

    private void DataView_RestoreRequested(object? sender, EventArgs e)
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
            DataView.SetFeedback("Base restauree. Portefeuille recharge depuis la source SQLite.");
            RefreshPortfolio();
        }
        catch (Exception exception)
        {
            DataView.SetFeedback(exception.Message);
        }
    }

    private void DataView_DeleteTransactionRequested(object? sender, string id)
    {
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
            DataView.SetFeedback("Ledger SQLite mis a jour.");
            RefreshPortfolio();
        }
        else
        {
            DataView.SetFeedback("Transaction introuvable. Rafraichis le journal.");
        }
    }

    private void DataView_ClearLedgerRequested(object? sender, EventArgs e)
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
        DataView.SetFeedback($"{deleted} transaction(s) supprimee(s). Le portefeuille est pret pour un import propre.");
        PositionsView.ResetAssetXray();
        RefreshPortfolio();
    }

    private void UpdateDataConfidence(PortfolioSnapshot portfolio, IReadOnlyList<LedgerTransaction> transactions)
    {
        if (transactions.Count == 0)
        {
            return;
        }

        var pendingImports = _binanceImportEvents.Count(row => row.Status == BinanceImportStatus.Pending);
        var blockedImports = _binanceImportEvents.Count(row => row.Status == BinanceImportStatus.Rejected || row.Status == BinanceImportStatus.Ignored);
        var mixed = UiFormatting.QuoteCurrencies(transactions).Count > 1;
        if (portfolio.Warnings.Count == 0 && pendingImports == 0 && !mixed)
        {
            return;
        }

        DataView.SetFeedback($"{portfolio.Warnings.Count} alerte(s), {pendingImports} a confirmer, {blockedImports} hors ledger, devise mixte: {(mixed ? "oui" : "non")}.");
    }
}

using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private readonly BinanceApiClient _binanceApiClient = new();
    private readonly BinanceCredentialStore _binanceCredentialStore = new();
    private readonly BinanceSnapshotStore _binanceSnapshotStore;
    private readonly List<BinanceImportEvent> _binanceImportEvents = [];
    private readonly List<BinanceImportDuplicate> _binanceImportDuplicates = [];
    private readonly HashSet<string> _loadedImportFiles = new(StringComparer.OrdinalIgnoreCase);
    private bool _binanceApiBusy;
    private int _loadedImportSourceRows;

    public MainWindow()
    {
        InitializeComponent();
        _store = SqliteLedgerStore.OpenDefault();
        _binanceSnapshotStore = BinanceSnapshotStore.OpenDefault();
        DatabasePathText.Text = _store.DatabasePath;
        DataView.SetDatabasePath(_store.DatabasePath);
        InitializeBinanceApiView();
        RefreshImportDashboard();
        RefreshPortfolio();
        ShowPage("Dashboard", DashboardNavButton);
        UpdateResponsiveLayout();
    }

    private void NavigateSection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string target)
        {
            return;
        }

        ShowPage(target, button);
    }

    private void ToggleNavGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string target)
        {
            return;
        }

        if (target == "Assets")
        {
            ToggleMenu(AssetsMenu, AssetsChevronText);
        }
        else if (target == "Orders")
        {
            ToggleMenu(OrdersMenu, OrdersChevronText);
        }
    }

    private static void ToggleMenu(UIElement menu, TextBlock chevron)
    {
        var expanded = menu.Visibility == Visibility.Visible;
        menu.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
        chevron.Text = expanded ? "+" : "-";
    }

    private void ShowPage(string target, Button? activeButton = null)
    {
        DashboardView.Visibility = target == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        PositionsView.Visibility = target == "Positions" ? Visibility.Visible : Visibility.Collapsed;
        BinanceApiView.Visibility = target == "BinanceApi" ? Visibility.Visible : Visibility.Collapsed;
        ImportStudioView.Visibility = target == "Imports" ? Visibility.Visible : Visibility.Collapsed;
        DataView.Visibility = target == "Data" ? Visibility.Visible : Visibility.Collapsed;

        SetActiveNavigation(activeButton ?? DefaultButtonFor(target));
        MainScrollViewer.ScrollToHome();
    }

    private Button DefaultButtonFor(string target) => target switch
    {
        "Dashboard" => DashboardNavButton,
        "Positions" => SpotNavButton,
        "BinanceApi" => BinanceApiNavButton,
        "Imports" => ImportNavButton,
        "Data" => DataNavButton,
        _ => DashboardNavButton
    };

    private void SetActiveNavigation(Button activeButton)
    {
        var allButtons = new[]
        {
            DashboardNavButton,
            AssetsOverviewNavButton,
            SpotNavButton,
            EarnNavButton,
            AlphaNavButton,
            BinanceApiNavButton,
            ImportNavButton,
            LedgerNavButton,
            DataNavButton
        };

        foreach (var button in allButtons)
        {
            button.Background = Brushes.Transparent;
            button.Foreground = new SolidColorBrush(Color.FromRgb(168, 179, 199));
        }

        activeButton.Background = new SolidColorBrush(Color.FromRgb(31, 42, 60));
        activeButton.Foreground = Brushes.White;
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout();
    }

    private void UpdateResponsiveLayout()
    {
        var compact = ActualWidth < 1380;
        DashboardView.SetCompactMode(compact);
        PositionsView.SetCompactMode(compact);
        BinanceApiView.SetCompactMode(compact);
        ImportStudioView.SetCompactMode(compact);
    }

    private void InitializeBinanceApiView()
    {
        try
        {
            var credentials = _binanceCredentialStore.Load();
            BinanceApiView.SetInitialState(
                credentials is not null,
                credentials is null
                    ? "Aucune cle API locale. Cree une cle Binance lecture seule, colle-la ici, puis teste la connexion."
                    : "Cle API locale detectee. Les champs restent vides pour eviter les fuites visuelles.");
        }
        catch (Exception exception)
        {
            BinanceApiView.SetInitialState(false, $"Impossible de lire les identifiants Binance locaux: {SafeErrorMessage(exception)}");
        }
    }

    private async void BinanceApiView_SaveCredentialsRequested(object? sender, BinanceApiCredentials credentials)
    {
        try
        {
            BinanceApiView.SetSyncRunning("Verification des permissions de la cle Binance...");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var restrictions = await _binanceApiClient.GetApiRestrictionsAsync(credentials, cancellation.Token);
            if (!restrictions.IsStrictReadOnly)
            {
                _binanceCredentialStore.Clear();
                BinanceApiView.ClearCredentialInputs();
                BinanceApiView.SetError(ReadOnlyFailureMessage(restrictions));
                return;
            }

            _binanceCredentialStore.Save(credentials);
            BinanceApiView.ClearCredentialInputs();
            BinanceApiView.SetInitialState(
                true,
                restrictions.IpRestrict
                    ? "Cle lecture seule validee et enregistree. Restriction IP active cote Binance."
                    : "Cle lecture seule validee et enregistree. Pour plus de securite, ajoute une restriction IP cote Binance.");
        }
        catch (Exception exception)
        {
            BinanceApiView.ClearCredentialInputs();
            BinanceApiView.SetError(SafeErrorMessage(exception, credentials));
        }
    }

    private async void BinanceApiView_TestConnectionRequested(object? sender, EventArgs e)
    {
        await RefreshBinanceSpotAsync("Test connexion Binance et lecture couverture...");
    }

    private async void BinanceApiView_RefreshSpotRequested(object? sender, EventArgs e)
    {
        await RefreshBinanceSpotAsync("Synchronisation Binance: Spot, Earn, ordres, prix et graphes...");
    }

    private void BinanceApiView_ClearCredentialsRequested(object? sender, EventArgs e)
    {
        _binanceCredentialStore.Clear();
        BinanceApiView.ClearCredentialInputs();
        BinanceApiView.SetInitialState(false, "Cle Binance locale effacee.");
    }

    private async Task RefreshBinanceSpotAsync(string runningMessage)
    {
        if (_binanceApiBusy)
        {
            BinanceApiView.SetSyncRunning("Synchronisation Binance deja en cours...");
            return;
        }

        _binanceApiBusy = true;
        BinanceApiCredentials? credentials = null;
        try
        {
            credentials = _binanceCredentialStore.Load();
            if (credentials is null)
            {
                BinanceApiView.SetError("Aucune cle API locale. Enregistre une cle Binance read-only avant la synchro.");
                return;
            }

            BinanceApiView.SetSyncRunning(runningMessage);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var restrictions = await _binanceApiClient.GetApiRestrictionsAsync(credentials, cancellation.Token);
            if (!restrictions.IsStrictReadOnly)
            {
                _binanceCredentialStore.Clear();
                BinanceApiView.ClearCredentialInputs();
                BinanceApiView.SetError(ReadOnlyFailureMessage(restrictions));
                return;
            }

            var warnings = new List<string>();
            var snapshot = await _binanceApiClient.GetAccountSnapshotAsync(credentials, cancellation.Token);
            var earnAccount = await TryLoadSimpleEarnAccountAsync(credentials, warnings, cancellation.Token);
            var earnPositions = await LoadEarnPositionsAsync(credentials, warnings, cancellation.Token);
            var assetUniverse = snapshot.Balances.Select(balance => balance.Asset)
                .Concat(earnPositions.Select(position => position.Asset))
                .Select(BinanceApiUiBuilder.UnderlyingAssetFor)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var prices = await LoadBinancePricesAsync(assetUniverse, cancellation.Token);
            var rows = BinanceApiUiBuilder.BuildRows(snapshot, earnPositions, prices);
            var openOrders = await LoadOpenOrdersAsync(credentials, warnings, cancellation.Token);
            var klines = await LoadBinanceKlinesAsync(BinanceApiUiBuilder.AssetsNeedingMarketData(rows), cancellation.Token);
            var syncedAt = DateTimeOffset.UtcNow;
            _binanceSnapshotStore.SaveSnapshot(syncedAt, ToCachedAssetRows(rows), prices, openOrders, klines);
            var latestSnapshot = _binanceSnapshotStore.LoadLatestSnapshot();
            var comparisons = BinanceLedgerReconciler.Compare(
                PortfolioCalculator.Calculate(_store.ListTransactions()),
                latestSnapshot);

            var total = BinanceApiUiBuilder.TotalUsdt(rows);
            var pricedRows = rows.Count(row => row.PriceUsdt != "-");
            var earnSummary = earnAccount is null
                ? "Earn indisponible"
                : $"Earn {UiFormatting.FormatNumber(earnAccount.TotalAmountInUSDT)} USDT";
            var warningText = warnings.Count == 0 ? string.Empty : $" Limites: {string.Join("; ", warnings)}.";
            BinanceApiView.SetSyncResult(
                rows,
                BinanceApiUiBuilder.BuildComparisonRows(comparisons),
                BinanceApiUiBuilder.BuildOpenOrderRows(openOrders),
                $"{UiFormatting.FormatNumber(total)} USDT",
                syncedAt,
                klines.Count,
                $"Source Binance lue: {snapshot.Balances.Count} spot, {earnPositions.Count} earn, {openOrders.Count} ordre(s) ouvert(s), {pricedRows} prix, {klines.Count} bougie(s) cachees. {earnSummary}. Aucune ecriture ledger SQLite.{warningText}");
        }
        catch (Exception exception)
        {
            BinanceApiView.SetError(SafeErrorMessage(exception, credentials));
        }
        finally
        {
            _binanceApiBusy = false;
        }
    }

    private async Task<IReadOnlyDictionary<string, decimal>> LoadBinancePricesAsync(
        IReadOnlyList<string> assets,
        CancellationToken cancellationToken)
    {
        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            var symbol = BinanceApiUiBuilder.PriceSymbolFor(asset);
            if (symbol is null)
            {
                continue;
            }

            var ticker = await _binanceApiClient.TryGetPriceAsync(symbol, cancellationToken);
            if (ticker is not null)
            {
                prices[BinanceApiUiBuilder.UnderlyingAssetFor(asset)] = ticker.Price;
            }
        }

        return prices;
    }

    private static IReadOnlyList<BinanceCachedAssetSnapshot> ToCachedAssetRows(IReadOnlyList<BinanceLiveBalanceRow> rows) =>
        rows.Select(row => new BinanceCachedAssetSnapshot(
                row.Source,
                row.Asset,
                row.UnderlyingAsset,
                row.FreeAmount,
                row.LockedAmount,
                row.TotalAmount,
                row.PriceUsdtValue,
                row.ValueUsdtValue,
                row.Status))
            .ToList();

    private async Task<BinanceSimpleEarnAccount?> TryLoadSimpleEarnAccountAsync(
        BinanceApiCredentials credentials,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _binanceApiClient.GetSimpleEarnAccountAsync(credentials, cancellationToken);
        }
        catch (Exception exception) when (IsOptionalBinanceSectionFailure(exception))
        {
            warnings.Add($"Earn compte: {SafeErrorMessage(exception, credentials)}");
            return null;
        }
    }

    private async Task<IReadOnlyList<BinanceEarnPosition>> LoadEarnPositionsAsync(
        BinanceApiCredentials credentials,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var positions = new List<BinanceEarnPosition>();
        try
        {
            positions.AddRange(await _binanceApiClient.GetFlexibleEarnPositionsAsync(credentials, cancellationToken));
        }
        catch (Exception exception) when (IsOptionalBinanceSectionFailure(exception))
        {
            warnings.Add($"Earn flexible: {SafeErrorMessage(exception, credentials)}");
        }

        try
        {
            positions.AddRange(await _binanceApiClient.GetLockedEarnPositionsAsync(credentials, cancellationToken));
        }
        catch (Exception exception) when (IsOptionalBinanceSectionFailure(exception))
        {
            warnings.Add($"Earn locked: {SafeErrorMessage(exception, credentials)}");
        }

        return positions;
    }

    private async Task<IReadOnlyList<BinanceOpenOrder>> LoadOpenOrdersAsync(
        BinanceApiCredentials credentials,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _binanceApiClient.GetOpenOrdersAsync(credentials, cancellationToken);
        }
        catch (Exception exception) when (IsOptionalBinanceSectionFailure(exception))
        {
            warnings.Add($"ordres ouverts: {SafeErrorMessage(exception, credentials)}");
            return [];
        }
    }

    private async Task<IReadOnlyList<BinanceKline>> LoadBinanceKlinesAsync(
        IReadOnlyList<string> assets,
        CancellationToken cancellationToken)
    {
        var klines = new List<BinanceKline>();
        foreach (var asset in assets.Take(20))
        {
            var symbol = BinanceApiUiBuilder.PriceSymbolFor(asset);
            if (symbol is null)
            {
                continue;
            }

            klines.AddRange(await _binanceApiClient.TryGetKlinesAsync(symbol, "1d", 90, cancellationToken));
        }

        return klines;
    }

    private static bool IsOptionalBinanceSectionFailure(Exception exception) =>
        exception is BinanceApiException or HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException;

    private static string SafeErrorMessage(Exception exception, BinanceApiCredentials? credentials = null)
    {
        var message = exception switch
        {
            BinanceApiException binanceApiException => binanceApiException.Message,
            HttpRequestException => "Connexion Binance impossible. Verifie Internet ou les restrictions reseau.",
            OperationCanceledException => "Connexion Binance interrompue: delai depasse.",
            CryptographicException => "Identifiants Binance locaux illisibles. Efface la cle locale puis reenregistre-la.",
            FormatException => "Fichier d'identifiants Binance local invalide. Efface la cle locale puis reenregistre-la.",
            JsonException => "Reponse Binance ou fichier local illisible.",
            InvalidOperationException invalidOperationException => invalidOperationException.Message,
            _ => "Erreur inattendue dans le module Binance API."
        };
        return SensitiveTextSanitizer.Sanitize(message, KnownSensitiveValues(credentials));
    }

    private static IEnumerable<string?> KnownSensitiveValues(BinanceApiCredentials? credentials)
    {
        if (credentials is null)
        {
            yield break;
        }

        yield return credentials.ApiKey;
        yield return credentials.ApiSecret;
    }

    private static string ReadOnlyFailureMessage(BinanceApiRestrictions restrictions)
    {
        var permissions = restrictions.DangerousPermissions.Count == 0
            ? "configuration non lecture seule"
            : string.Join(", ", restrictions.DangerousPermissions);
        return $"Cle Binance refusee: permissions non autorisees detectees ({permissions}). Cree une cle separee avec lecture seule uniquement.";
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

    private void DataView_ClearBinanceCacheRequested(object? sender, EventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "Purger le cache Binance live ? Les transactions ledger SQLite et la cle API locale ne seront pas supprimees.",
            "Confirmer la purge du cache Binance",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var deleted = _binanceSnapshotStore.PurgeCache();
        DataView.SetFeedback($"{deleted} ligne(s) de cache Binance supprimee(s). Le ledger SQLite reste intact.");
        BinanceApiView.SetInitialState(_binanceCredentialStore.Load() is not null, "Cache Binance purge. Rafraichis Binance pour recreer des snapshots live.");
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

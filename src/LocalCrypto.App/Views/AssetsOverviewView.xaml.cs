using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class AssetsOverviewView : UserControl
{
    private bool _compactMode;

    public AssetsOverviewView()
    {
        InitializeComponent();
        MarketCacheCard.Value = "Indicatif";
        MarketCacheCard.Hint = "Prix, klines et snapshots separes du ledger.";
        ImportPreviewCard.Value = "Fichiers";
        ImportPreviewCard.Hint = "CSV/XLSX, audit, reconciliation.";
        SetBinanceObservation(false, "Non connecte");
    }

    public void SetLedgerMetrics(DashboardMetrics metrics)
    {
        LedgerCard.Value = metrics.AssetCount;
        LedgerCard.Hint = $"{metrics.TransactionCount} transaction(s), {metrics.Confidence.ToLowerInvariant()}.";
    }

    public void SetBinanceObservation(bool hasObservation, string hint)
    {
        BinanceCard.Value = hasObservation ? "Observee" : "Non connecte";
        BinanceCard.Hint = hint;
    }

    public void SetCompactMode(bool compact)
    {
        if (_compactMode == compact)
        {
            return;
        }

        _compactMode = compact;
        SourceGrid.Columns = compact ? 2 : 4;
        OverviewPanelsGrid.ColumnDefinitions[1].Width = compact ? new System.Windows.GridLength(0) : new System.Windows.GridLength(16);
        OverviewPanelsGrid.ColumnDefinitions[2].Width = compact ? new System.Windows.GridLength(0) : new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
        Grid.SetRow(WorkflowPanel, compact ? 1 : 0);
        Grid.SetColumn(WorkflowPanel, compact ? 0 : 2);
        Grid.SetColumnSpan(WorkflowPanel, compact ? 3 : 1);
        WorkflowPanel.Margin = compact ? new System.Windows.Thickness(0, 14, 0, 0) : new System.Windows.Thickness(0);
    }
}

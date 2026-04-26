using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class DashboardView : UserControl
{
    public event EventHandler? RefreshBinanceRequested;
    private bool _compactMode;

    public DashboardView()
    {
        InitializeComponent();
    }

    public void SetMetrics(DashboardMetrics metrics)
    {
        AssetCountCard.Value = metrics.AssetCount;
        TransactionCountCard.Value = metrics.TransactionCount;
        TrackedCostCard.Value = metrics.TrackedCost;
        TrackedCostCard.Hint = metrics.TrackedCostHint;
        RealizedPnlCard.Value = metrics.RealizedPnl;
        RealizedPnlCard.Hint = metrics.RealizedPnlHint;
        FeesCard.Value = metrics.Fees;
        FeesCard.Hint = metrics.FeesHint;
        ConfidenceCard.Value = metrics.Confidence;
        ConfidenceCard.Hint = metrics.ConfidenceHint;
        ConfidenceCard.ValueBrush = metrics.ConfidenceTone;
    }

    public void SetSourceSummary(DashboardSourceSummary summary)
    {
        BinanceStateText.Text = summary.BinanceState;
        BinanceValueCard.Value = summary.BinanceValue;
        BinanceValueCard.Hint = summary.BinanceHint;
        BinanceAssetsCard.Value = summary.BinanceAssets;
        BinanceAssetsCard.Hint = "Spot/Earn observes, consolides par source.";
        ReconciliationCard.Value = summary.ReconciliationState;
        ReconciliationCard.Hint = summary.ReconciliationHint;
        OpenOrdersCard.Value = summary.OpenOrders;
        OpenOrdersCard.Hint = "Observation read-only.";
        DashboardHintText.Text = summary.CacheState;
    }

    public void SetCompactMode(bool compact)
    {
        if (_compactMode == compact)
        {
            return;
        }

        _compactMode = compact;
        DashboardMetricsGrid.Columns = compact ? 3 : 6;
        SourceMetricsGrid.Columns = compact ? 2 : 4;
    }

    private void RefreshBinance_Click(object sender, System.Windows.RoutedEventArgs e) =>
        RefreshBinanceRequested?.Invoke(this, EventArgs.Empty);
}

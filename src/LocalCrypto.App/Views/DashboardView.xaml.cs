using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class DashboardView : UserControl
{
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

    public void SetCompactMode(bool compact)
    {
        if (_compactMode == compact)
        {
            return;
        }

        _compactMode = compact;
        DashboardMetricsGrid.Columns = compact ? 3 : 6;
    }
}

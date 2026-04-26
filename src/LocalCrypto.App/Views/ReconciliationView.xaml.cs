using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class ReconciliationView : UserControl
{
    private bool _compactMode;

    public ReconciliationView()
    {
        InitializeComponent();
        SetRows([], null);
    }

    public void SetRows(IReadOnlyList<BinanceLedgerComparisonRow> rows, DateTimeOffset? syncedAt)
    {
        ComparisonGrid.ItemsSource = rows;
        ComparedAssetsCard.Value = rows.Count.ToString(UiFormatting.FrenchCulture);
        OkAssetsCard.Value = rows.Count(row => row.Status == "OK").ToString(UiFormatting.FrenchCulture);
        WarningAssetsCard.Value = rows.Count(row => row.Status != "OK").ToString(UiFormatting.FrenchCulture);
        WarningAssetsCard.Hint = "Ecart, absent ledger ou absent Binance.";
        SnapshotCard.Value = syncedAt is null ? "-" : syncedAt.Value.ToLocalTime().ToString("dd/MM HH:mm", UiFormatting.FrenchCulture);
        SnapshotCard.Hint = "Derniere observation Binance.";
        HintText.Text = rows.Count == 0
            ? "Aucun snapshot Binance en cache. Va dans Binance API / Compte ou clique Rafraichir Binance depuis le dashboard."
            : "Les ecarts sont des signaux de rapprochement. Le ledger SQLite ne change jamais sans validation explicite.";
    }

    public void SetCompactMode(bool compact)
    {
        if (_compactMode == compact)
        {
            return;
        }

        _compactMode = compact;
        MetricsGrid.Columns = compact ? 2 : 4;
    }
}

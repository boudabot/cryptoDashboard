using System.Windows;
using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class EarnView : UserControl
{
    public EarnView()
    {
        InitializeComponent();
        SetRows([]);
    }

    public void SetRows(IReadOnlyList<BinanceLiveBalanceRow> rows)
    {
        var earnRows = rows
            .Where(row => row.Source.Contains("Earn", StringComparison.OrdinalIgnoreCase))
            .ToList();
        EarnGrid.ItemsSource = earnRows;
        EarnEmptyText.Visibility = earnRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EarnProductsCard.Value = earnRows.Count.ToString(UiFormatting.FrenchCulture);
        EarnAssetsCard.Value = earnRows.Select(row => row.UnderlyingAsset).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(UiFormatting.FrenchCulture);
        EarnStateCard.Value = earnRows.Count == 0 ? "Non charge" : "Observe";
        EarnStateCard.Hint = "Source Binance live, pas ledger.";
    }
}

using System.Windows;
using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class OpenOrdersView : UserControl
{
    public OpenOrdersView()
    {
        InitializeComponent();
        SetOrders([]);
    }

    public void SetOrders(IReadOnlyList<BinanceOpenOrderRow> rows)
    {
        OpenOrdersGrid.ItemsSource = rows;
        OpenOrdersEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OpenOrdersCountCard.Value = rows.Count.ToString(UiFormatting.FrenchCulture);
        OpenOrdersCountCard.Hint = "Observation Binance read-only.";
        OpenOrdersAssetsCard.Value = rows.Select(row => row.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString(UiFormatting.FrenchCulture);
        OpenOrdersAssetsCard.Hint = "Symboles avec ordre ouvert.";
    }
}

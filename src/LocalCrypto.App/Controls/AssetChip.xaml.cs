using System.Windows;
using System.Windows.Controls;

namespace LocalCrypto.App.Controls;

public partial class AssetChip : UserControl
{
    public event EventHandler<string>? ChipClicked;

    public AssetChip()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AssetChipRow row)
        {
            ChipClicked?.Invoke(this, row.Asset);
        }
    }
}

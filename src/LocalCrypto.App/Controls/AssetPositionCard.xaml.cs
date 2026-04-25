using System.Windows;
using System.Windows.Controls;

namespace LocalCrypto.App.Controls;

public partial class AssetPositionCard : UserControl
{
    public event EventHandler<string>? ProofRequested;

    public AssetPositionCard()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PositionCardRow row)
        {
            ProofRequested?.Invoke(this, row.Symbol);
        }
    }
}

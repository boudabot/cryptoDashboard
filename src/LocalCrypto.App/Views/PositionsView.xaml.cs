using System.Windows;
using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class PositionsView : UserControl
{
    public event EventHandler<string>? AssetProofRequested;

    public PositionsView()
    {
        InitializeComponent();
        ResetAssetXray();
    }

    public void SetPositions(IReadOnlyList<PositionCardRow> rows)
    {
        PositionCardsItems.ItemsSource = rows;
        PositionsEmptyText.Visibility = rows.Count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public void SetAssetXray(AssetXrayModel model)
    {
        AssetXrayTitleText.Text = model.Title;
        AssetXraySubtitleText.Text = model.Subtitle;
        AssetXrayBadge.Text = model.Confidence;
        AssetXrayBadge.ToneBrush = model.ConfidenceTone;
        AssetXrayStatusHintText.Text = model.ConfidenceHint;
        AssetXrayQuantityText.Text = model.Quantity;
        AssetXrayAverageText.Text = model.AverageCost;
        AssetXrayCostText.Text = model.InvestedCost;
        AssetXrayPnlText.Text = model.RealizedPnl;
        AssetXrayFeesText.Text = model.Fees;
        AssetTransactionsGrid.ItemsSource = model.Transactions;
    }

    public void ResetAssetXray()
    {
        SetAssetXray(new AssetXrayModel(
            "Asset X-Ray",
            "Selectionne un actif pour voir sa preuve ledger.",
            "-",
            "#334155",
            "Aucune position selectionnee.",
            "-",
            "-",
            "-",
            "-",
            "-",
            []));
    }

    public void SetCharts(
        IReadOnlyList<LedgerChartRow> costRows,
        IReadOnlyList<LedgerChartRow> volumeRows,
        IReadOnlyList<LedgerChartRow> pnlRows)
    {
        CostChartItems.ItemsSource = costRows;
        VolumeChartItems.ItemsSource = volumeRows;
        PnlChartItems.ItemsSource = pnlRows;
        CostChartItems.ItemTemplate = ChartTemplate();
        VolumeChartItems.ItemTemplate = ChartTemplate();
        PnlChartItems.ItemTemplate = ChartTemplate();
    }

    private void AssetPositionCard_ProofRequested(object? sender, string symbol)
    {
        AssetProofRequested?.Invoke(this, symbol);
    }

    private static DataTemplate ChartTemplate()
    {
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Margin="0,4">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="92" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="92" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding Label}" Foreground="#94A3B8" />
                    <Grid Grid.Column="1" Height="12" Background="#1E293B">
                        <Border Background="{Binding Color}" Width="{Binding Width}" HorizontalAlignment="Left" CornerRadius="6" />
                    </Grid>
                    <TextBlock Grid.Column="2" Text="{Binding Value}" Foreground="#E5E7EB" HorizontalAlignment="Right" />
                </Grid>
            </DataTemplate>
            """;
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }
}

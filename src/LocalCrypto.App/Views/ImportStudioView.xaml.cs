using System.Windows;
using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class ImportStudioView : UserControl
{
    public event EventHandler? AddExportRequested;
    public event EventHandler? ValidateTradesRequested;
    public event EventHandler? ResetRequested;
    public event EventHandler? FiltersChanged;

    public ImportStudioView()
    {
        InitializeComponent();
        ImportChartItems.ItemTemplate = ChartTemplate();
        ImportRecentOrdersItems.ItemTemplate = RecentOrderTemplate();
        ImportAssetItems.ItemTemplate = ImportAssetTemplate();
    }

    public string AssetFilter => AssetFilterBox.Text.Trim().ToUpperInvariant();

    public string TypeFilter { get; private set; } = "All";

    public string StatusFilter { get; private set; } = "All";

    public void SetSummary(ImportSummaryModel summary)
    {
        ImportVolumeCard.Value = summary.Volume;
        ImportEventCountCard.Value = summary.EventCount;
        ImportTradeCountCard.Value = summary.TradeCount;
        ImportPendingCountCard.Value = summary.PendingCount;
        ImportAssetCountCard.Value = summary.AssetCount;
        ImportQuarantineCountCard.Value = summary.QuarantineCount;
    }

    public void SetFileInfo(string text) => ImportFileText.Text = text;

    public void SetFeedback(string text) => ImportFeedbackText.Text = text;

    public void SetAssetChips(IReadOnlyList<AssetChipRow> rows) => AssetChipItems.ItemsSource = rows;

    public void SetCharts(IReadOnlyList<ImportChartRow> rows) => ImportChartItems.ItemsSource = rows;

    public void SetRecentOrders(IReadOnlyList<RecentOrderRow> rows) => ImportRecentOrdersItems.ItemsSource = rows;

    public void SetAssets(IReadOnlyList<ImportAssetRow> rows) => ImportAssetItems.ItemsSource = rows;

    public void SetPreviewRows(IReadOnlyList<BinancePreviewRow> rows) => ImportPreviewGrid.ItemsSource = rows;

    public void SetQuarantineRows(IReadOnlyList<QuarantineRow> rows) => QuarantineGrid.ItemsSource = rows;

    public void SelectOrders() => ImportTabs.SelectedItem = ImportOrdersTab;

    public void SelectQuarantine() => ImportTabs.SelectedItem = ImportQuarantineTab;

    public void ClearAssetFilter()
    {
        AssetFilterBox.Text = string.Empty;
    }

    private void AddExport_Click(object sender, System.Windows.RoutedEventArgs e) => AddExportRequested?.Invoke(this, EventArgs.Empty);

    private void ValidateTrades_Click(object sender, System.Windows.RoutedEventArgs e) => ValidateTradesRequested?.Invoke(this, EventArgs.Empty);

    private void Reset_Click(object sender, System.Windows.RoutedEventArgs e) => ResetRequested?.Invoke(this, EventArgs.Empty);

    private void AssetFilter_TextChanged(object sender, TextChangedEventArgs e) => FiltersChanged?.Invoke(this, EventArgs.Empty);

    private void TypeFilter_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filter)
        {
            TypeFilter = filter;
            FiltersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void StatusFilter_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string filter)
        {
            StatusFilter = filter;
            if (filter == "Duplicates")
            {
                SelectQuarantine();
            }

            FiltersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void AssetChip_ChipClicked(object? sender, string asset)
    {
        AssetFilterBox.Text = asset == "*" ? string.Empty : asset;
        TypeFilter = "Trades";
        SelectOrders();
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    private static DataTemplate ChartTemplate()
    {
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Margin="0,5">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="120" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="60" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding Label}" Foreground="#CBD5E1" />
                    <Grid Grid.Column="1" Height="14" Background="#1E293B">
                        <Border Background="{Binding Color}" Width="{Binding Width}" HorizontalAlignment="Left" CornerRadius="7" />
                    </Grid>
                    <TextBlock Grid.Column="2" Text="{Binding Count}" Foreground="White" HorizontalAlignment="Right" />
                </Grid>
            </DataTemplate>
            """;
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }

    private static DataTemplate RecentOrderTemplate()
    {
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Margin="0,6">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="42" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <Border Width="30" Height="30" CornerRadius="15" Background="{Binding Accent}">
                        <TextBlock Text="{Binding Logo}" Foreground="#0B101B" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <StackPanel Grid.Column="1">
                        <TextBlock Text="{Binding Title}" Foreground="White" FontWeight="SemiBold" />
                        <TextBlock Text="{Binding Subtitle}" Foreground="#94A3B8" />
                    </StackPanel>
                    <TextBlock Grid.Column="2" Text="{Binding Status}" Foreground="{Binding StatusColor}" FontWeight="SemiBold" />
                </Grid>
            </DataTemplate>
            """;
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }

    private static DataTemplate ImportAssetTemplate()
    {
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Border Background="#111827" BorderBrush="#263244" BorderThickness="1" CornerRadius="8" Padding="14" Margin="0,0,0,8">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="52" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="130" />
                            <ColumnDefinition Width="130" />
                            <ColumnDefinition Width="130" />
                            <ColumnDefinition Width="160" />
                        </Grid.ColumnDefinitions>
                        <Border Width="36" Height="36" CornerRadius="18" Background="{Binding Accent}">
                            <TextBlock Text="{Binding Logo}" Foreground="#0B101B" FontWeight="Bold" HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                        <StackPanel Grid.Column="1">
                            <TextBlock Text="{Binding Asset}" Foreground="White" FontSize="16" FontWeight="SemiBold" />
                            <TextBlock Text="{Binding Description}" Foreground="#94A3B8" />
                        </StackPanel>
                        <StackPanel Grid.Column="2">
                            <TextBlock Text="Net preview" Foreground="#64748B" />
                            <TextBlock Text="{Binding NetQuantity}" Foreground="White" FontWeight="SemiBold" />
                        </StackPanel>
                        <StackPanel Grid.Column="3">
                            <TextBlock Text="Trades" Foreground="#64748B" />
                            <TextBlock Text="{Binding TradeCount}" Foreground="#22C55E" FontWeight="SemiBold" />
                        </StackPanel>
                        <StackPanel Grid.Column="4">
                            <TextBlock Text="A confirmer" Foreground="#64748B" />
                            <TextBlock Text="{Binding PendingCount}" Foreground="#FBBF24" FontWeight="SemiBold" />
                        </StackPanel>
                        <StackPanel Grid.Column="5">
                            <TextBlock Text="Devises" Foreground="#64748B" />
                            <TextBlock Text="{Binding QuoteBreakdown}" Foreground="#CBD5E1" FontWeight="SemiBold" />
                        </StackPanel>
                    </Grid>
                </Border>
            </DataTemplate>
            """;
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }
}

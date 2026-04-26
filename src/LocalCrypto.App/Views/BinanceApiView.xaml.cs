using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LocalCrypto.Data;

namespace LocalCrypto.App.Views;

public partial class BinanceApiView : UserControl
{
    public event EventHandler<BinanceApiCredentials>? SaveCredentialsRequested;
    public event EventHandler? TestConnectionRequested;
    public event EventHandler? ClearCredentialsRequested;
    public event EventHandler? RefreshSpotRequested;

    private bool _compactMode;

    public BinanceApiView()
    {
        InitializeComponent();
        SetInitialState(false, "Aucune cle API enregistree.");
        SetBalances([]);
    }

    public void ClearCredentialInputs()
    {
        ApiKeyBox.Clear();
        ApiSecretBox.Clear();
    }

    public void SetInitialState(bool hasCredentials, string hint)
    {
        BinanceConnectionCard.Value = hasCredentials ? "Pret" : "Non connecte";
        BinanceConnectionCard.Hint = hasCredentials ? "Cle locale trouvee." : "Ajoute une cle read-only.";
        BinanceConnectionCard.ValueBrush = hasCredentials ? "#38BDF8" : "#FBBF24";
        BinanceSpotAssetsCard.Value = "-";
        BinanceApproxValueCard.Value = "Non consolide";
        BinanceApproxValueCard.Hint = "Estimation live separee du ledger.";
        BinanceLastSyncCard.Value = "-";
        CredentialHintText.Text = hint;
        BinanceFeedbackText.Text = string.Empty;
        SetConnectionStatus(hasCredentials ? "Cle locale" : "Non connecte", hasCredentials ? "#38BDF8" : "#64748B");
    }

    public void SetSyncRunning(string message)
    {
        BinanceFeedbackText.Text = message;
        SetConnectionStatus("Synchronisation", "#FBBF24");
    }

    public void SetSyncResult(
        IReadOnlyList<BinanceLiveBalanceRow> rows,
        string approximateValue,
        DateTimeOffset syncedAt,
        string feedback)
    {
        SetBalances(rows);
        BinanceConnectionCard.Value = "OK";
        BinanceConnectionCard.Hint = "Connexion Binance read-only validee.";
        BinanceConnectionCard.ValueBrush = "#22C55E";
        BinanceSpotAssetsCard.Value = rows.Count.ToString(UiFormatting.FrenchCulture);
        BinanceApproxValueCard.Value = approximateValue;
        BinanceApproxValueCard.Hint = "Approximation prix publics USDT.";
        BinanceLastSyncCard.Value = syncedAt.ToLocalTime().ToString("dd/MM HH:mm", UiFormatting.FrenchCulture);
        BinanceFeedbackText.Text = feedback;
        SetConnectionStatus("Connecte", "#22C55E");
    }

    public void SetError(string message)
    {
        BinanceFeedbackText.Text = SanitizeMessage(message);
        BinanceConnectionCard.Value = "Erreur";
        BinanceConnectionCard.Hint = "Verifie la cle, le secret et les droits lecture.";
        BinanceConnectionCard.ValueBrush = "#F87171";
        SetConnectionStatus("Erreur", "#F87171");
    }

    public void SetCompactMode(bool compact)
    {
        if (_compactMode == compact)
        {
            return;
        }

        _compactMode = compact;
        BinanceMetricsGrid.Columns = compact ? 2 : 4;
    }

    private void SetBalances(IReadOnlyList<BinanceLiveBalanceRow> rows)
    {
        BinanceBalancesGrid.ItemsSource = rows;
    }

    private void SetConnectionStatus(string text, string brush)
    {
        BinanceStatusText.Text = text;
        BinanceStatusDot.Fill = (Brush)new BrushConverter().ConvertFromString(brush)!;
    }

    private void SaveCredentials_Click(object sender, RoutedEventArgs e)
    {
        SaveCredentialsRequested?.Invoke(
            this,
            new BinanceApiCredentials(ApiKeyBox.Text.Trim(), ApiSecretBox.Password.Trim()));
    }

    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Erreur Binance inconnue.";
        }

        var sanitized = message;
        foreach (var marker in new[] { "signature=", "X-MBX-APIKEY", "apiKey=", "apiSecret=", "secret=", "secret:" })
        {
            var index = sanitized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                sanitized = sanitized[..index] + $"{marker}<masque>";
            }
        }

        return sanitized.Length > 280 ? sanitized[..280] + "..." : sanitized;
    }

    private void TestConnection_Click(object sender, RoutedEventArgs e) => TestConnectionRequested?.Invoke(this, EventArgs.Empty);

    private void ClearCredentials_Click(object sender, RoutedEventArgs e) => ClearCredentialsRequested?.Invoke(this, EventArgs.Empty);

    private void RefreshSpot_Click(object sender, RoutedEventArgs e) => RefreshSpotRequested?.Invoke(this, EventArgs.Empty);
}

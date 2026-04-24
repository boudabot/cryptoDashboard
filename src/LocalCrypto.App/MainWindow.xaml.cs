using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using LocalCrypto.Core;
using LocalCrypto.Data;
using Microsoft.Win32;

namespace LocalCrypto.App;

public partial class MainWindow : Window
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private readonly SqliteLedgerStore _store;

    public MainWindow()
    {
        InitializeComponent();
        _store = SqliteLedgerStore.OpenDefault();
        DatabasePathText.Text = _store.DatabasePath;
        DataFileText.Text = _store.DatabasePath;
        ResetForm();
        RefreshPortfolio();
    }

    private void AddTransaction_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var transaction = new LedgerTransaction(
                Guid.NewGuid().ToString("N"),
                ParseExecutedAt(ExecutedAtBox.Text),
                ParseSide(),
                SymbolBox.Text.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(AssetNameBox.Text) ? SymbolBox.Text.Trim().ToUpperInvariant() : AssetNameBox.Text.Trim(),
                ParseDecimal(QuantityBox.Text, "quantite"),
                ParseDecimal(UnitPriceBox.Text, "prix unitaire"),
                DefaultText(QuoteCurrencyBox.Text, "USDT").ToUpperInvariant(),
                ParseDecimal(FeeAmountBox.Text, "frais"),
                DefaultText(FeeCurrencyBox.Text, "USDT").ToUpperInvariant(),
                DefaultText(SourceBox.Text, "MANUAL").ToUpperInvariant(),
                NoteBox.Text.Trim());

            ValidateTransaction(transaction);
            if (_store.HasDuplicate(transaction))
            {
                throw new InvalidOperationException("Transaction refusee: doublon probable deja present dans le ledger.");
            }

            _store.AddTransaction(transaction);
            FeedbackText.Text = $"Transaction {transaction.Side} {transaction.Symbol} enregistree.";
            DataFeedbackText.Text = "Ledger SQLite mis a jour.";
            ResetForm();
            RefreshPortfolio();
        }
        catch (Exception exception)
        {
            FeedbackText.Text = exception.Message;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshPortfolio();

    private void BackupDatabase_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Sauvegarder la base localCrypto",
                Filter = "SQLite (*.sqlite)|*.sqlite|Tous les fichiers (*.*)|*.*",
                FileName = $"localcrypto-backup-{DateTime.Now:yyyyMMdd-HHmmss}.sqlite"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            _store.BackupDatabase(dialog.FileName);
            DataFeedbackText.Text = $"Sauvegarde creee: {dialog.FileName}";
        }
        catch (Exception exception)
        {
            DataFeedbackText.Text = exception.Message;
        }
    }

    private void RestoreDatabase_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Restaurer une sauvegarde localCrypto",
                Filter = "SQLite (*.sqlite;*.db)|*.sqlite;*.db|Tous les fichiers (*.*)|*.*"
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            var confirmation = MessageBox.Show(
                this,
                "Restaurer cette sauvegarde remplacera la base SQLite active. Continuer ?",
                "Confirmer la restauration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            _store.RestoreDatabase(dialog.FileName);
            DataFeedbackText.Text = $"Base restauree depuis: {dialog.FileName}";
            FeedbackText.Text = "Portefeuille recharge depuis la base restauree.";
            RefreshPortfolio();
        }
        catch (Exception exception)
        {
            DataFeedbackText.Text = exception.Message;
        }
    }

    private void DeleteTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            "Supprimer cette transaction du ledger SQLite ? Les positions seront recalculees depuis le journal restant.",
            "Confirmer la suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        if (_store.DeleteTransaction(id))
        {
            FeedbackText.Text = "Transaction supprimee. Portefeuille recalcule depuis le ledger.";
            DataFeedbackText.Text = "Ledger SQLite mis a jour.";
            RefreshPortfolio();
        }
        else
        {
            FeedbackText.Text = "Transaction introuvable. Rafraichis le journal.";
        }
    }

    private void RefreshPortfolio()
    {
        var transactions = _store.ListTransactions();
        var portfolio = PortfolioCalculator.Calculate(transactions);

        InvestedText.Text = Money(portfolio.InvestedTotal, portfolio.BaseCurrency);
        RealizedPnlText.Text = Money(portfolio.RealizedPnlTotal, portfolio.BaseCurrency);
        FeesText.Text = Money(portfolio.TotalFees, portfolio.BaseCurrency);
        TransactionCountText.Text = portfolio.Transactions.Count.ToString(FrenchCulture);

        PositionsGrid.ItemsSource = portfolio.Positions.Select(position => new PositionRow(
            position.Symbol,
            FormatNumber(position.Quantity),
            Money(position.AverageCost, position.QuoteCurrency),
            Money(position.InvestedCost, position.QuoteCurrency),
            Money(position.RealizedPnl, position.QuoteCurrency),
            Money(position.Fees, position.QuoteCurrency))).ToList();

        TransactionsGrid.ItemsSource = transactions.Select(transaction => new TransactionRow(
            transaction.Id,
            transaction.ExecutedAt.ToLocalTime().ToString("g", FrenchCulture),
            transaction.Side.ToString(),
            transaction.Symbol,
            FormatNumber(transaction.Quantity),
            Money(transaction.UnitPrice, transaction.QuoteCurrency),
            Money(transaction.FeeAmount, transaction.FeeCurrency),
            transaction.Source,
            transaction.Note)).ToList();

        if (portfolio.Warnings.Count > 0)
        {
            FeedbackText.Text = string.Join(Environment.NewLine, portfolio.Warnings);
        }
    }

    private void ResetForm()
    {
        ExecutedAtBox.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        SideBox.SelectedIndex = 0;
        SymbolBox.Text = string.Empty;
        AssetNameBox.Text = string.Empty;
        QuantityBox.Text = string.Empty;
        UnitPriceBox.Text = string.Empty;
        QuoteCurrencyBox.Text = "USDT";
        FeeAmountBox.Text = "0";
        FeeCurrencyBox.Text = "USDT";
        SourceBox.Text = "MANUAL";
        NoteBox.Text = string.Empty;
    }

    private TradeSide ParseSide()
    {
        if (SideBox.SelectedItem is ComboBoxItem item && item.Content is string side)
        {
            return Enum.Parse<TradeSide>(side, ignoreCase: true);
        }

        return TradeSide.Buy;
    }

    private static DateTimeOffset ParseExecutedAt(string value)
    {
        if (DateTime.TryParse(value, FrenchCulture, DateTimeStyles.AssumeLocal, out var frenchDate) ||
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out frenchDate))
        {
            return new DateTimeOffset(frenchDate);
        }

        throw new InvalidOperationException("Date invalide. Format conseille: 2026-04-24 14:30.");
    }

    private static decimal ParseDecimal(string value, string fieldName)
    {
        var normalized = value.Trim().Replace(',', '.');
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Valeur invalide pour {fieldName}.");
    }

    private static void ValidateTransaction(LedgerTransaction transaction)
    {
        if (string.IsNullOrWhiteSpace(transaction.Symbol))
        {
            throw new InvalidOperationException("Le symbole est obligatoire.");
        }

        if (transaction.Quantity <= 0m)
        {
            throw new InvalidOperationException("La quantite doit etre positive.");
        }

        if (transaction.UnitPrice <= 0m)
        {
            throw new InvalidOperationException("Le prix unitaire doit etre positif.");
        }

        if (transaction.FeeAmount < 0m)
        {
            throw new InvalidOperationException("Les frais ne peuvent pas etre negatifs.");
        }
    }

    private static string DefaultText(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Money(decimal value, string currency) =>
        $"{FormatNumber(value)} {currency}";

    private static string FormatNumber(decimal value) =>
        value.ToString("0.########", FrenchCulture);

    private sealed record PositionRow(
        string Symbol,
        string Quantity,
        string AverageCost,
        string InvestedCost,
        string RealizedPnl,
        string Fees);

    private sealed record TransactionRow(
        string Id,
        string ExecutedAt,
        string Side,
        string Symbol,
        string Quantity,
        string UnitPrice,
        string Fee,
        string Source,
        string Note);
}

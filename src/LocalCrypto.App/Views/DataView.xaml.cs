using System.Windows;
using System.Windows.Controls;

namespace LocalCrypto.App.Views;

public partial class DataView : UserControl
{
    public event EventHandler? BackupRequested;
    public event EventHandler? RestoreRequested;
    public event EventHandler? ClearLedgerRequested;
    public event EventHandler<string>? DeleteTransactionRequested;

    public DataView()
    {
        InitializeComponent();
    }

    public void SetDatabasePath(string path) => DataFileText.Text = path;

    public void SetFeedback(string message) => DataFeedbackText.Text = message;

    public void SetTransactions(IReadOnlyList<TransactionRow> transactions) => TransactionsGrid.ItemsSource = transactions;

    private void Backup_Click(object sender, RoutedEventArgs e) => BackupRequested?.Invoke(this, EventArgs.Empty);

    private void Restore_Click(object sender, RoutedEventArgs e) => RestoreRequested?.Invoke(this, EventArgs.Empty);

    private void ClearLedger_Click(object sender, RoutedEventArgs e) => ClearLedgerRequested?.Invoke(this, EventArgs.Empty);

    private void DeleteTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string id)
        {
            DeleteTransactionRequested?.Invoke(this, id);
        }
    }
}

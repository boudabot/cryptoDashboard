using System.IO.Compression;
using System.Text;
using LocalCrypto.Data;

namespace LocalCrypto.Tests;

public sealed class BinanceImportPreviewerTests : IDisposable
{
    private readonly string _directory;

    public BinanceImportPreviewerTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "localcrypto-binance-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void PreviewCsvClassifiesBinanceTransactionHistory()
    {
        var path = Path.Combine(_directory, "binance.csv");
        File.WriteAllText(path, """
            Identifiant utilisateur,Durée,Compte,Opération,Jeton,Change,Remarque
            123456789,26-03-02 21:10:18,Spot,Transaction Fee,SOL,-0.00063745,
            123456789,26-03-02 21:10:18,Spot,Transaction Buy,SOL,0.671,
            123456789,26-03-02 21:10:18,Spot,Transaction Spend,USDC,-58.38371,
            123456789,26-03-03 10:00:00,Funding,Simple Earn Flexible Interest,USDC,0.01,Binance Earn/S123
            123456789,26-03-04 10:00:00,Funding,Simple Earn Flexible Subscription,USDC,-10,Binance Earn/S124
            """, Encoding.UTF8);

        var preview = new BinanceImportPreviewer().Preview(path);

        Assert.Equal(5, preview.TotalRows);
        Assert.Equal(3, preview.ImportableRows);
        Assert.Equal(1, preview.PendingRows);
        Assert.Equal(1, preview.IgnoredRows);
        Assert.Equal("Binance Earn", preview.Rows[3].RemarkKind);

        var tradeEvent = Assert.Single(preview.Events.Where(importEvent => importEvent.Kind == "BUY"));
        Assert.Equal("SOL", tradeEvent.Asset);
        Assert.Equal(3, tradeEvent.SourceRows);
        Assert.Equal(58.38371m, tradeEvent.QuoteAmount);
    }

    [Fact]
    public void PreviewXlsxReadsOffsetBinanceSheet()
    {
        var path = Path.Combine(_directory, "binance.xlsx");
        CreateMinimalBinanceXlsx(path);

        var preview = new BinanceImportPreviewer().Preview(path);

        Assert.Equal(2, preview.TotalRows);
        Assert.Equal(1, preview.ImportableRows);
        Assert.Equal(1, preview.PendingRows);
        Assert.Equal("ETH", preview.Rows[0].Asset);
        Assert.Equal(BinanceImportCategory.CashMovement, preview.Rows[1].Category);
        Assert.Equal(2, preview.Events.Count);
    }

    [Fact]
    public void PreviewXlsxReadsSpotOrderHistory()
    {
        var path = Path.Combine(_directory, "spot-orders.xlsx");
        CreateMinimalSpotOrdersXlsx(path);

        var preview = new BinanceImportPreviewer().Preview(path);

        var order = Assert.Single(preview.Events);
        Assert.Equal("BUY", order.Kind);
        Assert.Equal("ETH", order.Asset);
        Assert.Equal(0.0515m, order.Quantity);
        Assert.Equal("EUR", order.QuoteCurrency);
        Assert.Equal(100.528m, order.QuoteAmount);
        Assert.Equal(1952m, order.UnitPrice);
    }

    [Fact]
    public void PreviewCsvReadsAlphaOrderHistory()
    {
        var path = Path.Combine(_directory, "alpha-orders.csv");
        File.WriteAllText(path, """
            Durée,Numéro de commande,Type,Direction,Actif de base,Actif de cotation,Prix moyen de trading,Exécuté,Total,Statut,Slippage,Frais de réseau
            26-04-21 05:28:41,30220557,LIMIT,SELL,RAVE,USDC,1.532012492454328832 USDC,176.26 RAVE,270.03252192 USDC,FILLED,,
            26-04-21 02:52:58,30015282,LIMIT,SELL,RAVE,USDC,0 USDC,0 RAVE,0 USDC,CANCELED,,
            """, Encoding.UTF8);

        var preview = new BinanceImportPreviewer().Preview(path);

        var order = Assert.Single(preview.Events);
        Assert.Equal("SELL", order.Kind);
        Assert.Equal("RAVE", order.Asset);
        Assert.Equal(176.26m, order.Quantity);
        Assert.Equal("USDC", order.QuoteCurrency);
        Assert.Equal(270.03252192m, order.QuoteAmount);
        Assert.Equal(1.532012492454328832m, order.UnitPrice);
    }

    [Fact]
    public void PreviewRejectsUnsupportedFiles()
    {
        var path = Path.Combine(_directory, "binance.txt");
        File.WriteAllText(path, "not an export", Encoding.UTF8);

        Assert.Throws<InvalidOperationException>(() => new BinanceImportPreviewer().Preview(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static void CreateMinimalBinanceXlsx(string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var sheet = archive.CreateEntry("xl/worksheets/sheet1.xml");
        using var writer = new StreamWriter(sheet.Open(), Encoding.UTF8);
        writer.Write("""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
                <row r="1"><c r="C1" t="inlineStr"><is><t>Historique des transactions</t></is></c></row>
                <row r="10">
                  <c r="C10" t="inlineStr"><is><t>Identifiant utilisateur</t></is></c>
                  <c r="D10" t="inlineStr"><is><t>Durée</t></is></c>
                  <c r="F10" t="inlineStr"><is><t>Compte</t></is></c>
                  <c r="G10" t="inlineStr"><is><t>Opération</t></is></c>
                  <c r="I10" t="inlineStr"><is><t>Jeton</t></is></c>
                  <c r="J10" t="inlineStr"><is><t>Change</t></is></c>
                  <c r="L10" t="inlineStr"><is><t>Remarque</t></is></c>
                </row>
                <row r="11">
                  <c r="C11" t="inlineStr"><is><t>123456789</t></is></c>
                  <c r="D11" t="inlineStr"><is><t>26-03-09 01:28:25</t></is></c>
                  <c r="F11" t="inlineStr"><is><t>Spot</t></is></c>
                  <c r="G11" t="inlineStr"><is><t>Transaction Buy</t></is></c>
                  <c r="I11" t="inlineStr"><is><t>ETH</t></is></c>
                  <c r="J11" t="inlineStr"><is><t>0.0118</t></is></c>
                  <c r="L11" t="inlineStr"><is><t></t></is></c>
                </row>
                <row r="12">
                  <c r="C12" t="inlineStr"><is><t>123456789</t></is></c>
                  <c r="D12" t="inlineStr"><is><t>26-03-09 02:00:00</t></is></c>
                  <c r="F12" t="inlineStr"><is><t>Spot</t></is></c>
                  <c r="G12" t="inlineStr"><is><t>Deposit</t></is></c>
                  <c r="I12" t="inlineStr"><is><t>EUR</t></is></c>
                  <c r="J12" t="inlineStr"><is><t>20</t></is></c>
                  <c r="L12" t="inlineStr"><is><t></t></is></c>
                </row>
              </sheetData>
            </worksheet>
            """);
    }

    private static void CreateMinimalSpotOrdersXlsx(string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var sheet = archive.CreateEntry("xl/worksheets/sheet1.xml");
        using var writer = new StreamWriter(sheet.Open(), Encoding.UTF8);
        writer.Write("""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
                <row r="2"><c r="C2" t="inlineStr"><is><t>Historique d'ordre Spot</t></is></c></row>
                <row r="10">
                  <c r="C10" t="inlineStr"><is><t>Durée</t></is></c>
                  <c r="D10" t="inlineStr"><is><t>Numéro de commande</t></is></c>
                  <c r="E10" t="inlineStr"><is><t>Paire</t></is></c>
                  <c r="F10" t="inlineStr"><is><t>Type¹</t></is></c>
                  <c r="G10" t="inlineStr"><is><t>Côté</t></is></c>
                  <c r="H10" t="inlineStr"><is><t>Prix de l'ordre</t></is></c>
                  <c r="I10" t="inlineStr"><is><t>Montant de la commande</t></is></c>
                  <c r="J10" t="inlineStr"><is><t>Durée</t></is></c>
                  <c r="K10" t="inlineStr"><is><t>Exécuté²</t></is></c>
                  <c r="L10" t="inlineStr"><is><t>Prix moyen</t></is></c>
                  <c r="M10" t="inlineStr"><is><t>Trading total³</t></is></c>
                  <c r="N10" t="inlineStr"><is><t>Statut</t></is></c>
                </row>
                <row r="11">
                  <c r="C11" t="inlineStr"><is><t>26-04-17 12:50:09</t></is></c>
                  <c r="D11" t="inlineStr"><is><t>4793738105</t></is></c>
                  <c r="E11" t="inlineStr"><is><t>ETHEUR</t></is></c>
                  <c r="F11" t="inlineStr"><is><t>LIMIT</t></is></c>
                  <c r="G11" t="inlineStr"><is><t>BUY</t></is></c>
                  <c r="H11" t="inlineStr"><is><t>1952</t></is></c>
                  <c r="I11" t="inlineStr"><is><t>0.0515ETH</t></is></c>
                  <c r="J11" t="inlineStr"><is><t>26-04-19 20:01:07</t></is></c>
                  <c r="K11" t="inlineStr"><is><t>0.0515ETH</t></is></c>
                  <c r="L11" t="inlineStr"><is><t>1952</t></is></c>
                  <c r="M11" t="inlineStr"><is><t>100.528EUR</t></is></c>
                  <c r="N11" t="inlineStr"><is><t>Filled</t></is></c>
                </row>
                <row r="12">
                  <c r="C12" t="inlineStr"><is><t>26-04-16 13:32:47</t></is></c>
                  <c r="D12" t="inlineStr"><is><t>4790903933</t></is></c>
                  <c r="E12" t="inlineStr"><is><t>ETHEUR</t></is></c>
                  <c r="F12" t="inlineStr"><is><t>LIMIT</t></is></c>
                  <c r="G12" t="inlineStr"><is><t>BUY</t></is></c>
                  <c r="K12" t="inlineStr"><is><t>0ETH</t></is></c>
                  <c r="L12" t="inlineStr"><is><t>0</t></is></c>
                  <c r="M12" t="inlineStr"><is><t>0EUR</t></is></c>
                  <c r="N12" t="inlineStr"><is><t>CANCELED</t></is></c>
                </row>
              </sheetData>
            </worksheet>
            """);
    }
}

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LocalCrypto.Data;

namespace LocalCrypto.App;

public sealed class BinanceCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("localCrypto.binance.readonly.v1");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;

    public BinanceCredentialStore()
        : this(Path.Combine(AppDataPaths.DataDirectory, "binance-readonly.dat"))
    {
    }

    public BinanceCredentialStore(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    public bool HasCredentials => File.Exists(_filePath);

    public void Save(BinanceApiCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiKey) || string.IsNullOrWhiteSpace(credentials.ApiSecret))
        {
            throw new InvalidOperationException("Cle API et secret requis.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(credentials, JsonOptions);
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json),
            Entropy,
            DataProtectionScope.CurrentUser);
        File.WriteAllText(_filePath, Convert.ToBase64String(protectedBytes), Encoding.UTF8);
    }

    public BinanceApiCredentials? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var encoded = File.ReadAllText(_filePath, Encoding.UTF8);
        var protectedBytes = Convert.FromBase64String(encoded);
        var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        return JsonSerializer.Deserialize<BinanceApiCredentials>(Encoding.UTF8.GetString(bytes));
    }

    public void Clear()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LocalCrypto.Data;

public sealed class BinanceApiClient
{
    public const string DefaultBaseUrl = "https://api.binance.com";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public BinanceApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress ??= new Uri(DefaultBaseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetJsonAsync<BinanceServerTimeResponse>("/api/v3/time", cancellationToken).ConfigureAwait(false);
        return DateTimeOffset.FromUnixTimeMilliseconds(response.ServerTime);
    }

    public async Task<BinancePriceTicker?> TryGetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var normalized = symbol.Trim().ToUpperInvariant();
        try
        {
            var response = await GetJsonAsync<BinancePriceTickerResponse>(
                $"/api/v3/ticker/price?symbol={Uri.EscapeDataString(normalized)}",
                cancellationToken).ConfigureAwait(false);
            return new BinancePriceTicker(response.Symbol.ToUpperInvariant(), ParseDecimal(response.Price));
        }
        catch (BinanceApiException exception) when (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<BinanceAccountSnapshot> GetAccountSnapshotAsync(
        BinanceApiCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiKey) || string.IsNullOrWhiteSpace(credentials.ApiSecret))
        {
            throw new InvalidOperationException("Cle API Binance incomplete.");
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            ["recvWindow"] = "5000",
            ["omitZeroBalances"] = "true"
        };
        var query = BuildSignedQuery(parameters, credentials.ApiSecret);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v3/account?{query}");
        request.Headers.Add("X-MBX-APIKEY", credentials.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response.StatusCode, body);

        var account = JsonSerializer.Deserialize<BinanceAccountResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Reponse Binance account illisible.");
        var balances = account.Balances
            .Select(item => new BinanceAccountBalance(
                item.Asset.ToUpperInvariant(),
                ParseDecimal(item.Free),
                ParseDecimal(item.Locked)))
            .Where(item => item.Total > 0)
            .OrderByDescending(item => StableValueRank(item.Asset))
            .ThenBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BinanceAccountSnapshot(DateTimeOffset.FromUnixTimeMilliseconds(account.UpdateTime), balances);
    }

    public async Task<BinanceApiRestrictions> GetApiRestrictionsAsync(
        BinanceApiCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiKey) || string.IsNullOrWhiteSpace(credentials.ApiSecret))
        {
            throw new InvalidOperationException("Cle API Binance incomplete.");
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            ["recvWindow"] = "5000"
        };
        var query = BuildSignedQuery(parameters, credentials.ApiSecret);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/sapi/v1/account/apiRestrictions?{query}");
        request.Headers.Add("X-MBX-APIKEY", credentials.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response.StatusCode, body);

        var restrictions = JsonSerializer.Deserialize<BinanceApiRestrictionsResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("Reponse Binance apiRestrictions illisible.");
        return new BinanceApiRestrictions(
            restrictions.IpRestrict,
            restrictions.EnableReading,
            restrictions.EnableWithdrawals,
            restrictions.EnableInternalTransfer,
            restrictions.EnableMargin,
            restrictions.EnableFutures,
            restrictions.PermitsUniversalTransfer,
            restrictions.EnableVanillaOptions,
            restrictions.EnableFixApiTrade,
            restrictions.EnableFixReadOnly,
            restrictions.EnableSpotAndMarginTrading,
            restrictions.EnablePortfolioMarginTrading);
    }

    public static string BuildSignedQuery(IReadOnlyDictionary<string, string> parameters, string apiSecret)
    {
        var query = string.Join("&", parameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
        var signature = SignQuery(query, apiSecret);
        return $"{query}&signature={signature}";
    }

    public static string SignQuery(string query, string apiSecret)
    {
        var key = Encoding.UTF8.GetBytes(apiSecret);
        var payload = Encoding.UTF8.GetBytes(query);
        byte[]? hash = null;
        try
        {
            hash = HMACSHA256.HashData(key, payload);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(payload);
            if (hash is not null)
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }
    }

    private async Task<T> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response.StatusCode, body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException("Reponse Binance illisible.");
    }

    private static void EnsureSuccess(HttpStatusCode statusCode, string responseBody)
    {
        if ((int)statusCode is >= 200 and <= 299)
        {
            return;
        }

        var message = $"Erreur Binance HTTP {(int)statusCode}.";
        try
        {
            var error = JsonSerializer.Deserialize<BinanceErrorResponse>(responseBody, JsonOptions);
            if (!string.IsNullOrWhiteSpace(error?.Msg))
            {
                message = error.Msg;
            }
        }
        catch (JsonException)
        {
            // Keep the generic HTTP message. Raw bodies may contain implementation details.
        }

        throw new BinanceApiException(statusCode, SensitiveTextSanitizer.Sanitize(message, 240));
    }

    private static decimal ParseDecimal(string value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;

    private static int StableValueRank(string asset) => asset switch
    {
        "USDT" => 3,
        "USDC" => 2,
        "FDUSD" => 1,
        _ => 0
    };

    private sealed record BinanceServerTimeResponse(long ServerTime);

    private sealed record BinancePriceTickerResponse(string Symbol, string Price);

    private sealed record BinanceAccountResponse(long UpdateTime, IReadOnlyList<BinanceAccountBalanceResponse> Balances);

    private sealed record BinanceAccountBalanceResponse(string Asset, string Free, string Locked);

    private sealed record BinanceApiRestrictionsResponse(
        bool IpRestrict,
        bool EnableReading,
        bool EnableWithdrawals,
        bool EnableInternalTransfer,
        bool EnableMargin,
        bool EnableFutures,
        bool PermitsUniversalTransfer,
        bool EnableVanillaOptions,
        bool EnableFixApiTrade,
        bool EnableFixReadOnly,
        bool EnableSpotAndMarginTrading,
        bool EnablePortfolioMarginTrading);

    private sealed record BinanceErrorResponse(int Code, string Msg);
}

public sealed record BinanceApiCredentials(string ApiKey, string ApiSecret);

public sealed record BinancePriceTicker(string Symbol, decimal Price);

public sealed record BinanceAccountSnapshot(DateTimeOffset SyncedAt, IReadOnlyList<BinanceAccountBalance> Balances);

public sealed record BinanceAccountBalance(string Asset, decimal Free, decimal Locked)
{
    public decimal Total => Free + Locked;
}

public sealed record BinanceApiRestrictions(
    bool IpRestrict,
    bool EnableReading,
    bool EnableWithdrawals,
    bool EnableInternalTransfer,
    bool EnableMargin,
    bool EnableFutures,
    bool PermitsUniversalTransfer,
    bool EnableVanillaOptions,
    bool EnableFixApiTrade,
    bool EnableFixReadOnly,
    bool EnableSpotAndMarginTrading,
    bool EnablePortfolioMarginTrading)
{
    public bool IsStrictReadOnly =>
        EnableReading &&
        !EnableWithdrawals &&
        !EnableInternalTransfer &&
        !EnableMargin &&
        !EnableFutures &&
        !PermitsUniversalTransfer &&
        !EnableVanillaOptions &&
        !EnableFixApiTrade &&
        !EnableSpotAndMarginTrading &&
        !EnablePortfolioMarginTrading;

    public IReadOnlyList<string> DangerousPermissions
    {
        get
        {
            var permissions = new List<string>();
            if (!EnableReading)
            {
                permissions.Add("lecture desactivee");
            }

            if (EnableWithdrawals)
            {
                permissions.Add("retrait");
            }

            if (EnableInternalTransfer)
            {
                permissions.Add("transfert interne");
            }

            if (EnableMargin)
            {
                permissions.Add("margin");
            }

            if (EnableFutures)
            {
                permissions.Add("futures");
            }

            if (PermitsUniversalTransfer)
            {
                permissions.Add("transfert universel");
            }

            if (EnableVanillaOptions)
            {
                permissions.Add("options");
            }

            if (EnableFixApiTrade)
            {
                permissions.Add("FIX trading");
            }

            if (EnableSpotAndMarginTrading)
            {
                permissions.Add("trading spot/margin");
            }

            if (EnablePortfolioMarginTrading)
            {
                permissions.Add("portfolio margin trading");
            }

            return permissions;
        }
    }
}

public sealed class BinanceApiException(HttpStatusCode statusCode, string message) : InvalidOperationException(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

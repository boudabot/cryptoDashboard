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

    public async Task<IReadOnlyList<BinanceKline>> TryGetKlinesAsync(
        string symbol,
        string interval = "1d",
        int limit = 90,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return [];
        }

        var normalized = symbol.Trim().ToUpperInvariant();
        var safeLimit = Math.Clamp(limit, 1, 1000);
        try
        {
            using var document = await GetJsonDocumentAsync(
                $"/api/v3/klines?symbol={Uri.EscapeDataString(normalized)}&interval={Uri.EscapeDataString(interval)}&limit={safeLimit.ToString(CultureInfo.InvariantCulture)}",
                cancellationToken).ConfigureAwait(false);

            var rows = new List<BinanceKline>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 7)
                {
                    continue;
                }

                rows.Add(new BinanceKline(
                    normalized,
                    interval,
                    DateTimeOffset.FromUnixTimeMilliseconds(row[0].GetInt64()),
                    DateTimeOffset.FromUnixTimeMilliseconds(row[6].GetInt64()),
                    ParseDecimal(row[1].GetString() ?? "0"),
                    ParseDecimal(row[2].GetString() ?? "0"),
                    ParseDecimal(row[3].GetString() ?? "0"),
                    ParseDecimal(row[4].GetString() ?? "0"),
                    ParseDecimal(row[5].GetString() ?? "0")));
            }

            return rows;
        }
        catch (BinanceApiException exception) when (exception.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            return [];
        }
    }

    public async Task<BinanceAccountSnapshot> GetAccountSnapshotAsync(
        BinanceApiCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials(credentials);

        var account = await GetSignedJsonAsync<BinanceAccountResponse>(
            "/api/v3/account",
            credentials,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["omitZeroBalances"] = "true"
            },
            cancellationToken).ConfigureAwait(false);

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

    public async Task<BinanceSimpleEarnAccount> GetSimpleEarnAccountAsync(
        BinanceApiCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials(credentials);

        var account = await GetSignedJsonAsync<BinanceSimpleEarnAccountResponse>(
            "/sapi/v1/simple-earn/account",
            credentials,
            null,
            cancellationToken).ConfigureAwait(false);

        return new BinanceSimpleEarnAccount(
            ParseDecimal(account.TotalAmountInBTC),
            ParseDecimal(account.TotalAmountInUSDT),
            ParseDecimal(account.TotalFlexibleAmountInBTC),
            ParseDecimal(account.TotalFlexibleAmountInUSDT),
            ParseDecimal(account.TotalLockedInBTC),
            ParseDecimal(account.TotalLockedInUSDT));
    }

    public async Task<IReadOnlyList<BinanceEarnPosition>> GetFlexibleEarnPositionsAsync(
        BinanceApiCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials(credentials);

        return await GetPagedEarnPositionsAsync(
            "/sapi/v1/simple-earn/flexible/position",
            credentials,
            response => response.Rows.Select(row => new BinanceEarnPosition(
                "Earn flexible",
                row.Asset.ToUpperInvariant(),
                ParseDecimal(row.TotalAmount),
                ParseDecimal(row.LatestAnnualPercentageRate),
                ParseDecimal(row.CumulativeTotalRewards),
                row.ProductId,
                row.AutoSubscribe ? "Auto" : "Manuel")),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BinanceEarnPosition>> GetLockedEarnPositionsAsync(
        BinanceApiCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials(credentials);

        return await GetPagedEarnPositionsAsync(
            "/sapi/v1/simple-earn/locked/position",
            credentials,
            response => response.Rows.Select(row => new BinanceEarnPosition(
                "Earn locked",
                row.Asset.ToUpperInvariant(),
                ParseDecimal(row.Amount),
                ParseDecimal(row.Apy),
                ParseDecimal(row.RewardAmt),
                !string.IsNullOrWhiteSpace(row.ProjectId) ? row.ProjectId : row.PositionId.ToString(CultureInfo.InvariantCulture),
                string.IsNullOrWhiteSpace(row.Status) ? "Holding" : row.Status)),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BinanceOpenOrder>> GetOpenOrdersAsync(
        BinanceApiCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials(credentials);

        var orders = await GetSignedJsonAsync<IReadOnlyList<BinanceOpenOrderResponse>>(
            "/api/v3/openOrders",
            credentials,
            null,
            cancellationToken).ConfigureAwait(false);

        return orders
            .Select(order => new BinanceOpenOrder(
                order.Symbol.ToUpperInvariant(),
                order.OrderId,
                order.ClientOrderId,
                order.Side,
                order.Type,
                order.Status,
                ParseDecimal(order.Price),
                ParseDecimal(order.OrigQty),
                ParseDecimal(order.ExecutedQty),
                DateTimeOffset.FromUnixTimeMilliseconds(order.Time),
                DateTimeOffset.FromUnixTimeMilliseconds(order.UpdateTime)))
            .OrderByDescending(order => order.UpdatedAt)
            .ToList();
    }

    public async Task<BinanceApiRestrictions> GetApiRestrictionsAsync(
        BinanceApiCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        EnsureCredentials(credentials);

        var restrictions = await GetSignedJsonAsync<BinanceApiRestrictionsResponse>(
            "/sapi/v1/account/apiRestrictions",
            credentials,
            null,
            cancellationToken).ConfigureAwait(false);

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

    private async Task<IReadOnlyList<BinanceEarnPosition>> GetPagedEarnPositionsAsync(
        string path,
        BinanceApiCredentials credentials,
        Func<BinanceEarnPositionPageResponse, IEnumerable<BinanceEarnPosition>> selector,
        CancellationToken cancellationToken)
    {
        var positions = new List<BinanceEarnPosition>();
        for (var current = 1; current <= 20; current++)
        {
            var page = await GetSignedJsonAsync<BinanceEarnPositionPageResponse>(
                path,
                credentials,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["current"] = current.ToString(CultureInfo.InvariantCulture),
                    ["size"] = "100"
                },
                cancellationToken).ConfigureAwait(false);

            positions.AddRange(selector(page).Where(position => position.Amount > 0));
            if (positions.Count >= page.Total || page.Rows.Count == 0)
            {
                break;
            }
        }

        return positions;
    }

    private async Task<T> GetSignedJsonAsync<T>(
        string path,
        BinanceApiCredentials credentials,
        IReadOnlyDictionary<string, string>? parameters,
        CancellationToken cancellationToken)
    {
        var signedParameters = new Dictionary<string, string>(parameters ?? new Dictionary<string, string>(), StringComparer.Ordinal)
        {
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            ["recvWindow"] = "5000"
        };
        var query = BuildSignedQuery(signedParameters, credentials.ApiSecret);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{path}?{query}");
        request.Headers.Add("X-MBX-APIKEY", credentials.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response.StatusCode, body);

        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException("Reponse Binance illisible.");
    }

    private async Task<T> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response.StatusCode, body);
        return JsonSerializer.Deserialize<T>(body, JsonOptions)
            ?? throw new InvalidOperationException("Reponse Binance illisible.");
    }

    private async Task<JsonDocument> GetJsonDocumentAsync(string requestUri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response.StatusCode, body);
        return JsonDocument.Parse(body);
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

    private static void EnsureCredentials(BinanceApiCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiKey) || string.IsNullOrWhiteSpace(credentials.ApiSecret))
        {
            throw new InvalidOperationException("Cle API Binance incomplete.");
        }
    }

    private static decimal ParseDecimal(string? value) =>
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

    private sealed record BinanceSimpleEarnAccountResponse(
        string TotalAmountInBTC,
        string TotalAmountInUSDT,
        string TotalFlexibleAmountInBTC,
        string TotalFlexibleAmountInUSDT,
        string TotalLockedInBTC,
        string TotalLockedInUSDT);

    private sealed record BinanceEarnPositionPageResponse(IReadOnlyList<BinanceEarnPositionResponse> Rows, int Total);

    private sealed record BinanceEarnPositionResponse(
        string Asset,
        string TotalAmount,
        string LatestAnnualPercentageRate,
        string CumulativeTotalRewards,
        string ProductId,
        bool AutoSubscribe,
        long PositionId,
        string ProjectId,
        string Amount,
        string Apy,
        string RewardAmt,
        string Status);

    private sealed record BinanceOpenOrderResponse(
        string Symbol,
        long OrderId,
        string ClientOrderId,
        string Side,
        string Type,
        string Status,
        string Price,
        string OrigQty,
        string ExecutedQty,
        long Time,
        long UpdateTime);

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

public sealed record BinanceKline(
    string Symbol,
    string Interval,
    DateTimeOffset OpenTime,
    DateTimeOffset CloseTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);

public sealed record BinanceAccountSnapshot(DateTimeOffset SyncedAt, IReadOnlyList<BinanceAccountBalance> Balances);

public sealed record BinanceAccountBalance(string Asset, decimal Free, decimal Locked)
{
    public decimal Total => Free + Locked;
}

public sealed record BinanceSimpleEarnAccount(
    decimal TotalAmountInBTC,
    decimal TotalAmountInUSDT,
    decimal TotalFlexibleAmountInBTC,
    decimal TotalFlexibleAmountInUSDT,
    decimal TotalLockedInBTC,
    decimal TotalLockedInUSDT);

public sealed record BinanceEarnPosition(
    string Source,
    string Asset,
    decimal Amount,
    decimal Apr,
    decimal Rewards,
    string ProductId,
    string Status);

public sealed record BinanceOpenOrder(
    string Symbol,
    long OrderId,
    string ClientOrderId,
    string Side,
    string Type,
    string Status,
    decimal Price,
    decimal OriginalQuantity,
    decimal ExecutedQuantity,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

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

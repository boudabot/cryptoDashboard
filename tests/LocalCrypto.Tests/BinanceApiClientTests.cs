using System.Net;
using System.Text;
using LocalCrypto.Data;

namespace LocalCrypto.Tests;

public sealed class BinanceApiClientTests
{
    [Fact]
    public async Task GetAccountSnapshotSignsUserDataRequest()
    {
        HttpRequestMessage? captured = null;
        var httpClient = new HttpClient(new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""
                {
                  "updateTime": 1710000000000,
                  "balances": [
                    { "asset": "ETH", "free": "0.50000000", "locked": "0.10000000" },
                    { "asset": "BTC", "free": "0.00000000", "locked": "0.00000000" }
                  ]
                }
                """)
            };
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var snapshot = await client.GetAccountSnapshotAsync(new BinanceApiCredentials("api-key", "secret"));

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("X-MBX-APIKEY", out var keys));
        Assert.Equal("api-key", Assert.Single(keys));
        Assert.Contains("timestamp=", captured.RequestUri!.Query);
        Assert.Contains("recvWindow=5000", captured.RequestUri.Query);
        Assert.Contains("omitZeroBalances=true", captured.RequestUri.Query);
        Assert.Contains("signature=", captured.RequestUri.Query);
        var balance = Assert.Single(snapshot.Balances);
        Assert.Equal("ETH", balance.Asset);
        Assert.Equal(0.6m, balance.Total);
    }

    [Fact]
    public async Task TryGetPriceReturnsTickerPrice()
    {
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""{ "symbol": "ETHUSDT", "price": "3123.45000000" }""")
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var ticker = await client.TryGetPriceAsync("ethusdt");

        Assert.NotNull(ticker);
        Assert.Equal("ETHUSDT", ticker!.Symbol);
        Assert.Equal(3123.45m, ticker.Price);
    }

    [Fact]
    public async Task GetApiRestrictionsReadsDangerousPermissions()
    {
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""
            {
              "ipRestrict": false,
              "createTime": 1698645219000,
              "enableReading": true,
              "enableWithdrawals": false,
              "enableInternalTransfer": false,
              "enableMargin": false,
              "enableFutures": false,
              "permitsUniversalTransfer": false,
              "enableVanillaOptions": false,
              "enableFixApiTrade": false,
              "enableFixReadOnly": true,
              "enableSpotAndMarginTrading": true,
              "enablePortfolioMarginTrading": false
            }
            """)
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var restrictions = await client.GetApiRestrictionsAsync(new BinanceApiCredentials("api-key", "secret"));

        Assert.False(restrictions.IsStrictReadOnly);
        Assert.Contains("trading spot/margin", restrictions.DangerousPermissions);
    }

    [Fact]
    public async Task GetApiRestrictionsAcceptsReadingOnlyKey()
    {
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""
            {
              "ipRestrict": true,
              "createTime": 1698645219000,
              "enableReading": true,
              "enableWithdrawals": false,
              "enableInternalTransfer": false,
              "enableMargin": false,
              "enableFutures": false,
              "permitsUniversalTransfer": false,
              "enableVanillaOptions": false,
              "enableFixApiTrade": false,
              "enableFixReadOnly": true,
              "enableSpotAndMarginTrading": false,
              "enablePortfolioMarginTrading": false
            }
            """)
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var restrictions = await client.GetApiRestrictionsAsync(new BinanceApiCredentials("api-key", "secret"));

        Assert.True(restrictions.IsStrictReadOnly);
        Assert.True(restrictions.IpRestrict);
        Assert.Empty(restrictions.DangerousPermissions);
    }

    [Fact]
    public void BuildSignedQueryUsesHmacSha256()
    {
        var query = BinanceApiClient.BuildSignedQuery(
            new Dictionary<string, string>
            {
                ["symbol"] = "LTCBTC",
                ["side"] = "BUY",
                ["timestamp"] = "1499827319559"
            },
            "NhqPtmdSJYzaE5e5rI2KOzEnjgSt8gDV");

        Assert.Contains("symbol=LTCBTC", query);
        Assert.Contains("side=BUY", query);
        Assert.Contains("timestamp=1499827319559", query);
        Assert.Contains("signature=", query);
    }

    [Fact]
    public async Task AccountErrorDoesNotExposeSensitiveResponseValues()
    {
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = Json("""
            {
              "code": -1022,
              "msg": "bad request apiKey=PUBLIC-KEY secret=PRIVATE-SECRET signature=SIGNED-VALUE"
            }
            """)
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var exception = await Assert.ThrowsAsync<BinanceApiException>(() =>
            client.GetAccountSnapshotAsync(new BinanceApiCredentials("PUBLIC-KEY", "PRIVATE-SECRET")));

        Assert.DoesNotContain("PUBLIC-KEY", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-SECRET", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SIGNED-VALUE", exception.Message, StringComparison.Ordinal);
        Assert.Contains("<masque>", exception.Message, StringComparison.Ordinal);
    }

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}

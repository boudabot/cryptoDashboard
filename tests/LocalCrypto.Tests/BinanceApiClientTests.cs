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
    public async Task GetFlexibleEarnPositionsReadsSignedRows()
    {
        HttpRequestMessage? captured = null;
        var httpClient = new HttpClient(new StubHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = Json("""
                {
                  "rows": [
                    {
                      "totalAmount": "12.50000000",
                      "latestAnnualPercentageRate": "0.0123",
                      "cumulativeTotalRewards": "0.42",
                      "asset": "USDC",
                      "productId": "USDC001",
                      "autoSubscribe": true
                    }
                  ],
                  "total": 1
                }
                """)
            };
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var positions = await client.GetFlexibleEarnPositionsAsync(new BinanceApiCredentials("api-key", "secret"));

        Assert.NotNull(captured);
        Assert.Contains("/sapi/v1/simple-earn/flexible/position", captured!.RequestUri!.AbsolutePath);
        Assert.Contains("current=1", captured.RequestUri.Query);
        Assert.Contains("signature=", captured.RequestUri.Query);
        var position = Assert.Single(positions);
        Assert.Equal("Earn flexible", position.Source);
        Assert.Equal("USDC", position.Asset);
        Assert.Equal(12.5m, position.Amount);
        Assert.Equal(0.0123m, position.Apr);
        Assert.Equal(0.42m, position.Rewards);
    }

    [Fact]
    public async Task GetLockedEarnPositionsReadsSignedRows()
    {
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""
            {
              "rows": [
                {
                  "positionId": 123,
                  "projectId": "ETH90",
                  "asset": "ETH",
                  "amount": "0.75000000",
                  "APY": "0.052",
                  "rewardAmt": "0.01",
                  "status": "HOLDING"
                }
              ],
              "total": 1
            }
            """)
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var positions = await client.GetLockedEarnPositionsAsync(new BinanceApiCredentials("api-key", "secret"));

        var position = Assert.Single(positions);
        Assert.Equal("Earn locked", position.Source);
        Assert.Equal("ETH", position.Asset);
        Assert.Equal(0.75m, position.Amount);
        Assert.Equal(0.052m, position.Apr);
        Assert.Equal("HOLDING", position.Status);
    }

    [Fact]
    public async Task GetOpenOrdersReadsAllOpenSpotOrders()
    {
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""
            [
              {
                "symbol": "ETHUSDT",
                "orderId": 12345,
                "clientOrderId": "local",
                "price": "2500.00",
                "origQty": "0.2000",
                "executedQty": "0.0500",
                "status": "NEW",
                "type": "LIMIT",
                "side": "BUY",
                "time": 1710000000000,
                "updateTime": 1710000001000
              }
            ]
            """)
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var orders = await client.GetOpenOrdersAsync(new BinanceApiCredentials("api-key", "secret"));

        var order = Assert.Single(orders);
        Assert.Equal("ETHUSDT", order.Symbol);
        Assert.Equal(12345, order.OrderId);
        Assert.Equal(2500m, order.Price);
        Assert.Equal(0.2m, order.OriginalQuantity);
        Assert.Equal(0.05m, order.ExecutedQuantity);
    }

    [Fact]
    public async Task TryGetKlinesParsesCandles()
    {
        var httpClient = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = Json("""
            [
              [1710000000000, "10.0", "12.0", "9.0", "11.0", "123.45", 1710086399999]
            ]
            """)
        }))
        {
            BaseAddress = new Uri("https://example.test")
        };
        var client = new BinanceApiClient(httpClient);

        var klines = await client.TryGetKlinesAsync("ethusdt", "1d", 90);

        var kline = Assert.Single(klines);
        Assert.Equal("ETHUSDT", kline.Symbol);
        Assert.Equal("1d", kline.Interval);
        Assert.Equal(10m, kline.Open);
        Assert.Equal(12m, kline.High);
        Assert.Equal(9m, kline.Low);
        Assert.Equal(11m, kline.Close);
        Assert.Equal(123.45m, kline.Volume);
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

using LocalCrypto.Data;

namespace LocalCrypto.Tests;

public sealed class SensitiveTextSanitizerTests
{
    [Theory]
    [InlineData("apiKey=PUBLIC-KEY&secret=PRIVATE-SECRET&signature=SIGNED-VALUE")]
    [InlineData("api_key: PUBLIC-KEY api_secret: PRIVATE-SECRET signature: SIGNED-VALUE")]
    [InlineData("X-MBX-APIKEY PUBLIC-KEY failed with secret=PRIVATE-SECRET")]
    [InlineData("https://example.test/api?timestamp=1&signature=SIGNED-VALUE&apiKey=PUBLIC-KEY")]
    public void SanitizeMasksApiSecretsAndSignedValues(string message)
    {
        var sanitized = SensitiveTextSanitizer.Sanitize(message);

        Assert.DoesNotContain("PUBLIC-KEY", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-SECRET", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("SIGNED-VALUE", sanitized, StringComparison.Ordinal);
        Assert.Contains("<masque>", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeLimitsUnexpectedMessageLength()
    {
        var sanitized = SensitiveTextSanitizer.Sanitize(new string('x', 500), maxLength: 32);

        Assert.True(sanitized.Length <= 35);
        Assert.EndsWith("...", sanitized, StringComparison.Ordinal);
    }
}

using System.Text.RegularExpressions;

namespace LocalCrypto.Data;

public static class SensitiveTextSanitizer
{
    private static readonly Regex[] Patterns =
    [
        new(@"(?i)(apiKey|api_key|apiSecret|api_secret|secret|signature|X-MBX-APIKEY)(\s*[:=]\s*)[^&\s]+", RegexOptions.Compiled),
        new(@"(?i)(X-MBX-APIKEY)(\s+)[^&\s]+", RegexOptions.Compiled)
    ];

    public static string Sanitize(string? text, int maxLength = 280)
    {
        return Sanitize(text, [], maxLength);
    }

    public static string Sanitize(string? text, IEnumerable<string?> knownSensitiveValues, int maxLength = 280)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Erreur inconnue.";
        }

        var sanitized = text;
        foreach (var value in knownSensitiveValues)
        {
            if (!string.IsNullOrWhiteSpace(value) && value.Length >= 4)
            {
                sanitized = sanitized.Replace(value, "<masque>", StringComparison.Ordinal);
            }
        }

        foreach (var pattern in Patterns)
        {
            sanitized = pattern.Replace(sanitized, "$1$2<masque>");
        }

        return sanitized.Length > maxLength ? sanitized[..maxLength] + "..." : sanitized;
    }
}

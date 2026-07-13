using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Beauty.Api.Services;

public static class GeminiRateLimitPolicy
{
    public static bool IsQuotaExceeded(string rawJson, string detail)
    {
        var combined = $"{detail} {rawJson}".ToLowerInvariant();
        return combined.Contains("exceeded your current quota") ||
            combined.Contains("quota exceeded") ||
            combined.Contains("billing") ||
            combined.Contains("billable") ||
            combined.Contains("daily quota") ||
            combined.Contains("per day") ||
            combined.Contains("spend limit") ||
            combined.Contains("free tier") ||
            combined.Contains("rate-limits");
    }

    public static TimeSpan? TryGetShortRetryDelay(HttpResponseMessage response, string rawJson)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return IsShortRetryDelay(delta) ? delta : null;
        }

        if (response.Headers.RetryAfter?.Date is { } retryAt)
        {
            var delay = retryAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                return IsShortRetryDelay(delay) ? delay : null;
            }
        }

        var retryInfoDelay = ExtractRetryInfoDelay(rawJson);
        return retryInfoDelay is { } retryInfo && IsShortRetryDelay(retryInfo)
            ? retryInfo
            : null;
    }

    private static bool IsShortRetryDelay(TimeSpan delay) =>
        delay > TimeSpan.Zero && delay <= TimeSpan.FromSeconds(10);

    private static TimeSpan? ExtractRetryInfoDelay(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (!document.RootElement.TryGetProperty("error", out var error) ||
                !error.TryGetProperty("details", out var details) ||
                details.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var detail in details.EnumerateArray())
            {
                if (detail.TryGetProperty("retryDelay", out var retryDelay) &&
                    retryDelay.ValueKind == JsonValueKind.String &&
                    TryParseGoogleDuration(retryDelay.GetString() ?? "", out var delay))
                {
                    return delay;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool TryParseGoogleDuration(string value, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Regex.Match(value.Trim(), @"^(?<seconds>\d+(?:\.\d+)?)s$", RegexOptions.IgnoreCase);
        return match.Success &&
            double.TryParse(match.Groups["seconds"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) &&
            seconds > 0 &&
            (delay = TimeSpan.FromSeconds(seconds)) > TimeSpan.Zero;
    }
}

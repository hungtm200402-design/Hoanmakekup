using System.Net;
using Beauty.Api.Services;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class GeminiRateLimitPolicyTests
{
    [Fact]
    public void IsQuotaExceeded_DetectsDailyBillingQuota()
    {
        const string rawJson = """
        {
          "error": {
            "code": 429,
            "message": "You exceeded your current quota, please check your plan and billing details."
          }
        }
        """;

        Assert.True(GeminiRateLimitPolicy.IsQuotaExceeded(rawJson, ""));
    }

    [Fact]
    public void TryGetShortRetryDelay_AcceptsShortRetryAfter()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(4));

        var delay = GeminiRateLimitPolicy.TryGetShortRetryDelay(response, "{}");

        Assert.Equal(TimeSpan.FromSeconds(4), delay);
    }

    [Fact]
    public void TryGetShortRetryDelay_RejectsLongRetryInfo()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        const string rawJson = """
        {
          "error": {
            "details": [
              {
                "@type": "type.googleapis.com/google.rpc.RetryInfo",
                "retryDelay": "23s"
              }
            ]
          }
        }
        """;

        Assert.Null(GeminiRateLimitPolicy.TryGetShortRetryDelay(response, rawJson));
    }
}

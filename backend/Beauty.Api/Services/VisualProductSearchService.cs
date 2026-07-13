using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using Beauty.Api.Models;

namespace Beauty.Api.Services;

public sealed class VisualProductSearchService(
    IEnumerable<IVisualSearchProvider> providers,
    ILogger<VisualProductSearchService> logger)
{
    public async Task<VisualSearchResult> SearchAsync(ValidatedImage image, CancellationToken cancellationToken)
    {
        foreach (var provider in providers)
        {
            if (!provider.IsConfigured)
            {
                continue;
            }

            var result = await provider.SearchAsync(image, cancellationToken);
            logger.LogInformation(
                "Visual search provider {Provider} returned {Count} candidate URLs. Evidence={Evidence}",
                provider.Name,
                result.Candidates.Count,
                string.Join(" | ", result.Evidence));
            if (result.Candidates.Count > 0)
            {
                return result;
            }
        }

        return new VisualSearchResult([], ["Không có visual search provider nào trả URL ứng viên."]);
    }
}

public interface IVisualSearchProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<VisualSearchResult> SearchAsync(ValidatedImage image, CancellationToken cancellationToken);
}

public sealed class GeminiVisualSearchProvider(
    IConfiguration configuration,
    HttpClient httpClient,
    ILogger<GeminiVisualSearchProvider> logger) : IVisualSearchProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Name => "gemini-image-grounded-search-not-reverse-image-search";

    public bool IsConfigured =>
        string.Equals(
            configuration["ENABLE_NON_REVERSE_IMAGE_GROUNDED_SEARCH_PROVIDER"] ??
            Environment.GetEnvironmentVariable("ENABLE_NON_REVERSE_IMAGE_GROUNDED_SEARCH_PROVIDER"),
            "true",
            StringComparison.OrdinalIgnoreCase) &&
        (!string.IsNullOrWhiteSpace(configuration["GEMINI_API_KEY"]) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY")));

    public async Task<VisualSearchResult> SearchAsync(ValidatedImage image, CancellationToken cancellationToken)
    {
        var apiKey = configuration["GEMINI_API_KEY"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
        var model = configuration["GEMINI_MODEL"] ?? Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-3.1-flash-lite";
        var evidence = new List<string>
        {
            "image-grounded Gemini search started (not reverse-image-search)",
            "image included in search request",
            "google_search tool requested"
        };

        var body = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            text = """
                            Tìm trên web các trang sản phẩm giống chính xác ảnh mỹ phẩm này.
                            Bắt buộc ưu tiên product page chính thức, official regional, retailer uy tín, catalogue/PDF chính thức.
                            Không đoán tên sản phẩm trước. Không trả homepage/category/search/blog/social/marketplace.
                            Trả ngắn gọn tối đa 10 URL ứng viên nếu thấy.
                            """
                        },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = image.MimeType,
                                data = image.Base64
                            }
                        }
                    }
                }
            },
            tools = new object[]
            {
                new { google_search = new { } }
            },
            generationConfig = new
            {
                temperature = 0.0,
                topP = 0.25,
                maxOutputTokens = 1200
            }
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));

        string raw = "";
        HttpStatusCode lastStatus = 0;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
            message.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
            message.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            lastStatus = response.StatusCode;
            raw = await response.Content.ReadAsStringAsync(timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                break;
            }

            logger.LogWarning(
                "Gemini visual search failed. Attempt={Attempt}; Status={Status}; Body={Body}",
                attempt,
                (int)response.StatusCode,
                raw);
            if (attempt >= 3 || response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable))
            {
                evidence.Add($"visual search failed: HTTP {(int)response.StatusCode}");
                return new VisualSearchResult([], evidence);
            }

            evidence.Add($"visual search retry {attempt}: HTTP {(int)response.StatusCode}");
            await Task.Delay(TimeSpan.FromSeconds(attempt), timeout.Token);
        }

        if (lastStatus != HttpStatusCode.OK)
        {
            evidence.Add($"visual search failed: HTTP {(int)lastStatus}");
            return new VisualSearchResult([], evidence);
        }

        var candidates = ExtractGroundingSources(raw)
            .Concat(ExtractTextUrls(raw))
            .GroupBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(10)
            .ToArray();
        evidence.Add($"grounding URLs received: {candidates.Length}");
        foreach (var candidate in candidates)
        {
            evidence.Add($"candidate: {candidate.Url}");
        }

        return new VisualSearchResult(candidates, evidence);
    }

    private static IReadOnlyList<GroundedSource> ExtractGroundingSources(string rawJson)
    {
        var sources = new List<GroundedSource>();
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            foreach (var chunk in FindProperties(document.RootElement, "groundingChunks"))
            {
                if (chunk.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in chunk.EnumerateArray())
                {
                    if (item.TryGetProperty("web", out var web))
                    {
                        AddSource(sources, GetString(web, "uri"), GetString(web, "title"));
                    }
                }
            }
        }
        catch
        {
            return sources;
        }

        return sources;
    }

    private static IEnumerable<JsonElement> FindProperties(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName))
                {
                    yield return property.Value;
                }

                foreach (var child in FindProperties(property.Value, propertyName))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in FindProperties(item, propertyName))
                {
                    yield return child;
                }
            }
        }
    }

    private static IReadOnlyList<GroundedSource> ExtractTextUrls(string rawJson)
    {
        var sources = new List<GroundedSource>();
        foreach (Match match in Regex.Matches(rawJson, @"https?://[^\s\""'<>)\\]+", RegexOptions.IgnoreCase))
        {
            AddSource(sources, match.Value.TrimEnd('.', ',', ';'), "URL ứng viên từ visual search");
        }

        return sources;
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static void AddSource(List<GroundedSource> sources, string url, string title)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return;
        }

        sources.Add(new GroundedSource(
            uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(title) ? uri.Host : title,
            uri.ToString()));
    }
}

public sealed record VisualSearchResult(IReadOnlyList<GroundedSource> Candidates, IReadOnlyList<string> Evidence);

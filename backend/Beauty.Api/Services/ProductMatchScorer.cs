using System.Text.RegularExpressions;
using System.Text;
using Beauty.Api.Models;

namespace Beauty.Api.Services;

public sealed class ProductMatchScorer
{
    private static readonly HashSet<string> GenericTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "official", "product", "beauty", "makeup", "skincare", "fragrance", "perfume",
        "lip", "lips", "son", "moi", "cream", "serum", "the", "and", "paris",
        "eau", "de", "parfum", "toilette", "spray", "ml", "shade", "color"
    };

    private static readonly string[] TrustedRetailerHosts =
    [
        "sephora.com",
        "ulta.com",
        "nordstrom.com",
        "boots.com",
        "harrods.com",
        "selfridges.com",
        "spacenk.com",
        "cultbeauty.com",
        "lookfantastic.com",
        "macys.com",
        "bloomingdales.com"
    ];

    public ProductUrlScore Score(ConfirmedProductRequest request, string url, string title, string content)
    {
        var haystack = Normalize($"{url} {title} {content}");
        var brandTokens = Tokens(request.Brand).ToArray();
        var productTokens = Tokens($"{request.ProductName} {request.ProductLine} {request.Variant} {request.Shade}")
            .Where(token => !brandTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
            .Where(token => !GenericTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var itemForm = Normalize(request.ItemForm);
        var category = Normalize($"{request.Category} {request.ProductName} {request.ProductLine}");

        var sourceType = SourceType(url, request.Brand);
        var score = sourceType switch
        {
            "official" => 20,
            "official-document" => 18,
            "trusted-retailer" => 14,
            _ => 0
        };
        var reasons = new List<string>();
        if (score > 0)
        {
            reasons.Add(sourceType);
        }
        if (brandTokens.Length > 0 && brandTokens.Any(haystack.Contains))
        {
            score += 25;
            reasons.Add("brand");
        }

        var productMatches = productTokens.Count(haystack.Contains);
        if (productTokens.Length > 0)
        {
            score += Math.Min(40, productMatches * 15);
            if (productMatches > 0)
            {
                reasons.Add($"productTokens:{productMatches}/{productTokens.Length}");
            }
        }

        if (ItemFormMatches(itemForm, category, haystack))
        {
            score += 20;
            reasons.Add("itemForm");
        }

        if (VariantMatches(request, haystack))
        {
            score += 15;
            reasons.Add("variant");
        }

        var hasHardConflict = HasItemFormConflict(itemForm, category, haystack) ||
            HasKnownLineConflict(category, haystack, productTokens);
        return new ProductUrlScore(Math.Clamp(score, 0, 100), score >= 65 && !hasHardConflict, reasons, hasHardConflict);
    }

    public string SourceType(string url, string brand)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "unknown";
        }

        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var path = uri.AbsolutePath;
        var normalizedBrand = Normalize(brand);
        if (!string.IsNullOrWhiteSpace(normalizedBrand))
        {
            var compactHost = Regex.Replace(host, @"[^a-z0-9]", "");
            var compactBrand = Regex.Replace(normalizedBrand, @"[^a-z0-9]", "");
            if (compactBrand.Length >= 3 && compactHost.Contains(compactBrand, StringComparison.OrdinalIgnoreCase))
            {
                return path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "official-document" : "official";
            }
        }

        if (TrustedRetailerHosts.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)))
        {
            return "trusted-retailer";
        }

        return path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "official-document" : "unknown";
    }

    public bool IsRejectedContainerUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return true;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var lastSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        return lastSegment is "home" or "homepage" or "shop" or "products" or "product" or "collections" or "collection" or "search" or "category" or "categories" or "lips" or "lip" or "makeup" or "skincare" or "fragrance";
    }

    private static bool VariantMatches(ConfirmedProductRequest request, string haystack)
    {
        var expected = Normalize($"{request.ProductLine} {request.Variant} {request.Shade} {request.Size}");
        if (expected.Contains("lip glow oil") || expected.Contains("glow oil") || expected.Contains("lip oil"))
        {
            return haystack.Contains("lip glow oil") || haystack.Contains("glow oil") || haystack.Contains("lip oil");
        }

        return !expected.Contains("lip glow") ||
            (haystack.Contains("lip glow") && !haystack.Contains("lip maximizer") && !haystack.Contains("glow oil"));
    }

    private static bool ItemFormMatches(string itemForm, string category, string haystack)
    {
        if (itemForm.Contains("case") || category.Contains("case") || category.Contains("vo son"))
        {
            return haystack.Contains("case") || haystack.Contains("casing") || haystack.Contains("fashion case");
        }

        if (itemForm.Contains("refill") || category.Contains("refill") || category.Contains("loi son"))
        {
            return haystack.Contains("refill");
        }

        if (category.Contains("lip oil"))
        {
            return haystack.Contains("lip oil") || haystack.Contains("glow oil");
        }

        if (category.Contains("lip gloss") || category.Contains("son bong"))
        {
            return haystack.Contains("gloss") || haystack.Contains("maximizer");
        }

        if (category.Contains("lip balm") || category.Contains("son duong") || category.Contains("lip glow"))
        {
            return haystack.Contains("balm") || haystack.Contains("lip glow");
        }

        return true;
    }

    private static bool HasItemFormConflict(string itemForm, string category, string haystack)
    {
        if ((itemForm.Contains("case") || category.Contains("case") || category.Contains("vo son")) &&
            !haystack.Contains("case") &&
            (haystack.Contains("lipstick") || haystack.Contains("lip glow") || haystack.Contains("lip oil")))
        {
            return true;
        }

        if ((itemForm.Contains("full-product") || category.Contains("lipstick")) && haystack.Contains("refill"))
        {
            return true;
        }

        return false;
    }

    private static bool HasKnownLineConflict(string category, string haystack, IReadOnlyCollection<string> productTokens)
    {
        if (category.Contains("lip glow") &&
            (haystack.Contains("lip maximizer") || haystack.Contains("lip glow oil") || haystack.Contains("glow oil")))
        {
            return true;
        }

        if ((category.Contains("lip oil") || productTokens.Contains("oil")) &&
            haystack.Contains("lip maximizer"))
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<string> Tokens(string value) =>
        Normalize(value)
            .Split([' ', '-', '_', '/', '.', '\'', '"', '’'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3 && !GenericTokens.Contains(token));

    private static string Normalize(string value) =>
        Regex.Replace(value.Normalize(NormalizationForm.FormD), @"\p{Mn}", "")
            .Replace('đ', 'd')
            .Replace('Đ', 'd')
            .ToLowerInvariant();
}

public sealed record ProductUrlScore(int Score, bool Accepted, IReadOnlyList<string> Reasons, bool HasHardConflict);

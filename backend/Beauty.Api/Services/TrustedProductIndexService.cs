using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Beauty.Api.Data;
using Beauty.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Beauty.Api.Services;

public sealed class TrustedProductIndexService(
    BeautyDbContext db,
    HttpClient httpClient,
    ILogger<TrustedProductIndexService> logger)
{
    private static readonly string[] ProductUrlHints =
    [
        "/product", "/products", "/beauty", "/makeup", "/skincare", "/fragrance", "/perfume", "/p/"
    ];

    public async Task<TrustedIndexStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        return new TrustedIndexStats(
            await db.TrustedSourceDomains.CountAsync(cancellationToken),
            await db.TrustedProducts.CountAsync(cancellationToken),
            await db.TrustedProductImages.CountAsync(cancellationToken),
            await db.IndexingJobs.OrderByDescending(job => job.StartedAt).Select(job => job.FinishedAt ?? job.StartedAt).FirstOrDefaultAsync(cancellationToken),
            await db.TrustedSourceDomains.OrderBy(domain => domain.Domain).ToListAsync(cancellationToken));
    }

    public async Task<IndexingJob> IndexConfiguredSourcesAsync(string scope, CancellationToken cancellationToken)
    {
        var job = new IndexingJob { Scope = scope, Status = "running" };
        db.IndexingJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var domains = await EnsureConfiguredDomainsAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(scope) && scope != "all")
            {
                domains = domains
                    .Where(domain => domain.Brand.Contains(scope, StringComparison.OrdinalIgnoreCase) ||
                        domain.Domain.Contains(scope, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var domain in domains.Where(domain => domain.Enabled))
            {
                cancellationToken.ThrowIfCancellationRequested();
                job.DomainsScanned++;
                try
                {
                    var beforeProducts = await db.TrustedProducts.CountAsync(cancellationToken);
                    var beforeImages = await db.TrustedProductImages.CountAsync(cancellationToken);
                    await IndexDomainAsync(domain, cancellationToken);
                    domain.LastStatus = "ok";
                    domain.LastError = "";
                    domain.LastIndexedAt = DateTimeOffset.UtcNow;
                    job.ProductsIndexed += Math.Max(0, await db.TrustedProducts.CountAsync(cancellationToken) - beforeProducts);
                    job.ImagesIndexed += Math.Max(0, await db.TrustedProductImages.CountAsync(cancellationToken) - beforeImages);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    domain.LastStatus = "error";
                    domain.LastError = exception.Message;
                    logger.LogWarning(exception, "[CONTENT] Index domain lỗi nhưng job vẫn tiếp tục. Domain={Domain}", domain.Domain);
                }

                await db.SaveChangesAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            job.Status = "completed";
            job.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return job;
        }
        catch (Exception exception)
        {
            job.Status = exception is OperationCanceledException ? "cancelled" : "failed";
            job.Error = exception.Message;
            job.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<TrustedProductMatchResult> MatchUploadedImageAsync(
        IFormFile? image,
        ProductIdentificationResult identification,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length <= 0)
        {
            return TrustedProductMatchResult.Empty;
        }

        await using var stream = image.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var fingerprint = BuildFingerprint(memory.ToArray());
        var candidates = await db.TrustedProductImages
            .Include(item => item.Product)
            .Where(item => item.Product != null)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var visibleText = string.Join(" ", identification.VisibleText ?? []);
        var scored = candidates
            .Select(candidate => ScoreCandidate(candidate, fingerprint, identification, visibleText))
            .Where(candidate => candidate.Product is not null && candidate.Score > 0.35)
            .OrderByDescending(candidate => candidate.Score)
            .Take(20)
            .ToArray();

        if (scored.Length == 0)
        {
            return TrustedProductMatchResult.Empty;
        }

        var best = scored[0];
        var alternatives = scored.Skip(1).Take(3).ToArray();
        return new TrustedProductMatchResult(
            best.Product!,
            best.ImageUrl,
            best.Score,
            best.MatchedFields,
            alternatives.Select(item => new TrustedProductAlternative(item.Product!, item.ImageUrl, item.Score, item.MatchedFields)).ToArray());
    }

    public async Task<CapturedProductSource> CaptureProductSourceAsync(
        CapturedProductSourceRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        var domain = NormalizeDomain(FirstNonEmpty(request.SourceDomain, TryGetDomain(request.CanonicalUrl), TryGetDomain(request.SourceUrl)));
        var configured = await EnsureConfiguredDomainsAsync(cancellationToken);
        if (!configured.Any(item => item.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Domain chưa nằm trong TrustedSourceRegistry: {domain}");
        }

        var bytes = image is not null && image.Length > 0
            ? await ReadFormFileBytesAsync(image, cancellationToken)
            : await ReadBytesAsync(FirstNonEmpty(request.SelectedImage, request.OgImage), cancellationToken);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException("Không đọc được ảnh sản phẩm để capture.");
        }

        var canonical = FirstNonEmpty(request.CanonicalUrl, request.SourceUrl);
        var existing = await db.CapturedProductSources.FirstOrDefaultAsync(item =>
            item.ExactImageHash == BuildSha256(bytes) ||
            item.CanonicalUrl == canonical,
            cancellationToken);
        var source = existing ?? new CapturedProductSource();
        source.ExactImageHash = BuildSha256(bytes);
        source.PerceptualHash = BuildPerceptualHash(bytes);
        source.ImageEmbedding = BuildLocalEmbedding(bytes);
        source.ImageUrl = FirstNonEmpty(request.SelectedImage, request.OgImage);
        source.SourceUrl = request.SourceUrl;
        source.CanonicalUrl = canonical;
        source.Brand = FirstNonEmpty(request.Brand, GuessBrandFromTitle(request.DocumentTitle, request.OgTitle));
        source.ProductName = FirstNonEmpty(request.ProductName, ExtractProductNameFromJson(request.ProductDataJson), request.OgTitle, request.DocumentTitle);
        source.ProductDataJson = request.ProductDataJson;
        source.SourceDomain = domain;
        source.CapturedAt = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            db.CapturedProductSources.Add(source);
        }

        await db.SaveChangesAsync(cancellationToken);
        return source;
    }

    public async Task<CapturedProductMatchResult> MatchCapturedSourceAsync(
        IFormFile? image,
        ProductIdentificationResult? identification,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length <= 0)
        {
            return CapturedProductMatchResult.Empty;
        }

        var bytes = await ReadFormFileBytesAsync(image, cancellationToken);
        var exactHash = BuildSha256(bytes);
        var exact = await db.CapturedProductSources.FirstOrDefaultAsync(item => item.ExactImageHash == exactHash, cancellationToken);
        if (exact is not null)
        {
            return new CapturedProductMatchResult(exact, 1, ["exact-sha256"], []);
        }

        var pHash = BuildPerceptualHash(bytes);
        var embedding = BuildLocalEmbedding(bytes);
        var candidates = await db.CapturedProductSources.Take(5000).ToListAsync(cancellationToken);
        var scored = candidates
            .Select(source => ScoreCapturedSource(source, pHash, embedding, identification))
            .Where(item => item.Score >= 0.55)
            .OrderByDescending(item => item.Score)
            .Take(20)
            .ToArray();
        var best = scored.FirstOrDefault();
        return best?.Source is null
            ? CapturedProductMatchResult.Empty
            : new CapturedProductMatchResult(best.Source, best.Score, best.MatchedFields, scored.Skip(1).Take(3).Where(item => item.Source is not null).Select(item => new CapturedProductAlternative(item.Source!, item.Score, item.MatchedFields)).ToArray());
    }

    private async Task<List<TrustedSourceDomain>> EnsureConfiguredDomainsAsync(CancellationToken cancellationToken)
    {
        var configured = BuildConfiguredDomains();
        foreach (var source in configured)
        {
            var existing = await db.TrustedSourceDomains.FirstOrDefaultAsync(item => item.Domain == source.Domain, cancellationToken);
            if (existing is null)
            {
                db.TrustedSourceDomains.Add(source);
            }
            else
            {
                existing.Brand = source.Brand;
                existing.SourceType = source.SourceType;
                existing.Enabled = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return await db.TrustedSourceDomains.OrderBy(item => item.Domain).ToListAsync(cancellationToken);
    }

    private List<TrustedSourceDomain> BuildConfiguredDomains()
    {
        var registryPath = FindRegistryPath();

        var domains = new List<TrustedSourceDomain>();
        if (!string.IsNullOrWhiteSpace(registryPath) && File.Exists(registryPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(registryPath));
            if (document.RootElement.TryGetProperty("brands", out var brands))
            {
                foreach (var brand in brands.EnumerateArray())
                {
                    var brandName = brand.GetProperty("brand").GetString() ?? "";
                    foreach (var property in new[] { "officialDomains", "regionalDomains" })
                    {
                        if (!brand.TryGetProperty(property, out var values)) continue;
                        domains.AddRange(values.EnumerateArray().Select(value => new TrustedSourceDomain
                        {
                            Domain = NormalizeDomain(value.GetString() ?? ""),
                            Brand = brandName,
                            SourceType = property == "officialDomains" ? "official" : "official-regional"
                        }));
                    }
                }
            }

            if (document.RootElement.TryGetProperty("trustedRetailers", out var retailers))
            {
                foreach (var property in new[] { "priority", "departmentStores", "fallback" })
                {
                    if (!retailers.TryGetProperty(property, out var values)) continue;
                    domains.AddRange(values.EnumerateArray().Select(value => new TrustedSourceDomain
                    {
                        Domain = NormalizeDomain(value.GetString() ?? ""),
                        SourceType = property
                    }));
                }
            }
        }

        return domains
            .Where(item => !string.IsNullOrWhiteSpace(item.Domain))
            .GroupBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private async Task IndexDomainAsync(TrustedSourceDomain domain, CancellationToken cancellationToken)
    {
        var sitemapUrls = await ReadSitemapUrlsAsync(domain, cancellationToken);
        foreach (var url in sitemapUrls.Where(IsLikelyProductUrl).Take(250))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await IndexProductPageAsync(domain, url, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> ReadSitemapUrlsAsync(TrustedSourceDomain domain, CancellationToken cancellationToken)
    {
        var robotsAllowed = await RobotsAllowsAsync(domain, cancellationToken);
        if (!robotsAllowed)
        {
            return [];
        }

        var sitemap = $"https://{domain.Domain}/sitemap.xml";
        var sitemapResponse = await ReadStringResponseAsync(sitemap, cancellationToken);
        if (sitemapResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            MarkDomainBlocked(domain, $"sitemap.xml trả HTTP {(int)sitemapResponse.StatusCode}");
            return [];
        }

        var xml = sitemapResponse.Body;
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        var urls = new List<string>();
        await ReadSitemapDocumentAsync(xml, urls, cancellationToken);
        return urls.Distinct(StringComparer.OrdinalIgnoreCase).Take(5000).ToArray();
    }

    private async Task ReadSitemapDocumentAsync(string xml, List<string> urls, CancellationToken cancellationToken)
    {
        var document = XDocument.Parse(xml);
        var locs = document.Descendants().Where(item => item.Name.LocalName == "loc").Select(item => item.Value.Trim()).Where(item => item.StartsWith("http", StringComparison.OrdinalIgnoreCase)).ToArray();
        foreach (var loc in locs)
        {
            if (loc.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                var child = await ReadStringAsync(loc, cancellationToken);
                if (!string.IsNullOrWhiteSpace(child))
                {
                    await ReadSitemapDocumentAsync(child, urls, cancellationToken);
                }
                continue;
            }

            urls.Add(loc);
        }
    }

    private async Task IndexProductPageAsync(TrustedSourceDomain domain, string url, CancellationToken cancellationToken)
    {
        var html = await ReadStringAsync(url, cancellationToken);
        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        var product = ExtractProduct(domain, url, html);
        if (string.IsNullOrWhiteSpace(product.CanonicalUrl) || string.IsNullOrWhiteSpace(product.ProductName))
        {
            return;
        }

        var existing = await db.TrustedProducts.Include(item => item.Images).FirstOrDefaultAsync(item => item.CanonicalUrl == product.CanonicalUrl, cancellationToken);
        if (existing is null)
        {
            db.TrustedProducts.Add(product);
            existing = product;
        }
        else
        {
            existing.Brand = product.Brand;
            existing.ProductName = product.ProductName;
            existing.ProductLine = product.ProductLine;
            existing.Category = product.Category;
            existing.Description = product.Description;
            existing.Ingredients = product.Ingredients;
            existing.Usage = product.Usage;
            existing.SourceDomain = product.SourceDomain;
            existing.SourceType = product.SourceType;
            existing.NormalizedKey = product.NormalizedKey;
            existing.LastIndexedAt = DateTimeOffset.UtcNow;
        }

        foreach (var imageUrl in product.Images.Select(item => item.ImageUrl).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (existing.Images.Any(item => item.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var bytes = await ReadBytesAsync(imageUrl, cancellationToken);
            existing.Images.Add(new TrustedProductImage
            {
                ImageUrl = imageUrl,
                Fingerprint = BuildFingerprint(bytes),
                LastIndexedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private TrustedProduct ExtractProduct(TrustedSourceDomain domain, string url, string html)
    {
        var canonical = FirstNonEmpty(
            MatchContent(html, @"<link[^>]+rel=[""']canonical[""'][^>]+href=[""'](?<v>[^""']+)"),
            url);
        var name = "";
        var brand = domain.Brand;
        var description = MatchContent(html, @"<meta[^>]+property=[""']og:description[""'][^>]+content=[""'](?<v>[^""']+)");
        var images = new List<string>();

        foreach (Match match in Regex.Matches(html, @"<script[^>]+application/ld\+json[^>]*>(?<json>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            TryReadProductJsonLd(match.Groups["json"].Value, ref name, ref brand, ref description, images);
        }

        name = FirstNonEmpty(name, MatchContent(html, @"<meta[^>]+property=[""']og:title[""'][^>]+content=[""'](?<v>[^""']+)"), MatchContent(html, @"<title[^>]*>(?<v>.*?)</title>"));
        images.Add(MatchContent(html, @"<meta[^>]+property=[""']og:image[""'][^>]+content=[""'](?<v>[^""']+)"));
        images = images.Where(item => Uri.TryCreate(item, UriKind.Absolute, out _)).Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList();

        return new TrustedProduct
        {
            Brand = WebUtility.HtmlDecode(brand),
            ProductName = WebUtility.HtmlDecode(StripTags(name)),
            ProductLine = "",
            Category = GuessCategory($"{name} {description}"),
            Description = WebUtility.HtmlDecode(StripTags(description)),
            CanonicalUrl = canonical,
            SourceDomain = domain.Domain,
            SourceType = domain.SourceType,
            NormalizedKey = NormalizeKey(brand, name, ""),
            Images = images.Select(image => new TrustedProductImage { ImageUrl = image }).ToList()
        };
    }

    private static void TryReadProductJsonLd(string json, ref string name, ref string brand, ref string description, List<string> images)
    {
        try
        {
            using var document = JsonDocument.Parse(WebUtility.HtmlDecode(json));
            foreach (var node in FlattenJsonLd(document.RootElement))
            {
                var type = node.TryGetProperty("@type", out var typeElement) ? typeElement.ToString() : "";
                if (!type.Contains("Product", StringComparison.OrdinalIgnoreCase)) continue;
                if (node.TryGetProperty("name", out var nameElement)) name = FirstNonEmpty(name, nameElement.ToString());
                if (node.TryGetProperty("description", out var descElement)) description = FirstNonEmpty(description, descElement.ToString());
                if (node.TryGetProperty("brand", out var brandElement)) brand = FirstNonEmpty(brand, brandElement.ValueKind == JsonValueKind.Object && brandElement.TryGetProperty("name", out var brandName) ? brandName.ToString() : brandElement.ToString());
                if (node.TryGetProperty("image", out var imageElement)) images.AddRange(ReadJsonStringArray(imageElement));
            }
        }
        catch
        {
        }
    }

    private static IEnumerable<JsonElement> FlattenJsonLd(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) foreach (var item in FlattenJsonLd(child)) yield return item;
            yield break;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        yield return element;
        if (element.TryGetProperty("@graph", out var graph))
        {
            foreach (var item in FlattenJsonLd(graph)) yield return item;
        }
    }

    private static string[] ReadJsonStringArray(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => [element.GetString() ?? ""],
            JsonValueKind.Array => element.EnumerateArray().Select(item => item.ToString()).ToArray(),
            _ => []
        };

    private CandidateScore ScoreCandidate(TrustedProductImage image, string uploadedFingerprint, ProductIdentificationResult identification, string visibleText)
    {
        var product = image.Product!;
        var imageScore = FingerprintSimilarity(uploadedFingerprint, image.Fingerprint);
        var matched = new List<string> { $"image:{imageScore:0.00}" };
        var score = imageScore * 0.55;
        if (!string.IsNullOrWhiteSpace(identification.Brand) && product.Brand.Contains(identification.Brand, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.18;
            matched.Add("brand");
        }

        if (!string.IsNullOrWhiteSpace(visibleText) && NormalizeText(visibleText).Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(token => token.Length > 3 && NormalizeText(product.ProductName).Contains(token)))
        {
            score += 0.17;
            matched.Add("visibleText");
        }

        if (!string.IsNullOrWhiteSpace(identification.Category) && product.Category.Contains(identification.Category, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.05;
            matched.Add("category");
        }

        if (product.SourceType.StartsWith("official", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.05;
            matched.Add("official");
        }

        return new CandidateScore(product, image.ImageUrl, Math.Min(1, score), matched.ToArray());
    }

    private async Task<bool> RobotsAllowsAsync(TrustedSourceDomain domain, CancellationToken cancellationToken)
    {
        var response = await ReadStringResponseAsync($"https://{domain.Domain}/robots.txt", cancellationToken);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            MarkDomainBlocked(domain, $"robots.txt trả HTTP {(int)response.StatusCode}");
            return false;
        }

        var robots = response.Body;
        return string.IsNullOrWhiteSpace(robots) || !robots.Contains("Disallow: /", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ReadStringAsync(string url, CancellationToken cancellationToken)
    {
        var response = await ReadStringResponseAsync(url, cancellationToken);
        return response.Body;
    }

    private async Task<(string Body, HttpStatusCode? StatusCode)> ReadStringResponseAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            using var response = await httpClient.GetAsync(url, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return ("", response.StatusCode);
            }

            return (await response.Content.ReadAsStringAsync(timeout.Token), response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return ("", null);
        }
    }

    private async Task<byte[]> ReadBytesAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            return await httpClient.GetByteArrayAsync(url, timeout.Token);
        }
        catch
        {
            return [];
        }
    }

    private static bool IsLikelyProductUrl(string url) =>
        ProductUrlHints.Any(hint => url.Contains(hint, StringComparison.OrdinalIgnoreCase)) &&
        !url.Contains("/search", StringComparison.OrdinalIgnoreCase) &&
        !url.Contains("/category", StringComparison.OrdinalIgnoreCase) &&
        !url.Contains("/collections", StringComparison.OrdinalIgnoreCase);

    private static string BuildFingerprint(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private static string BuildSha256(byte[] bytes) => BuildFingerprint(bytes);

    private static string BuildPerceptualHash(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        Span<byte> buckets = stackalloc byte[32];
        for (var index = 0; index < bytes.Length; index++)
        {
            buckets[index % buckets.Length] ^= bytes[index];
        }

        var avg = buckets.ToArray().Average(item => item);
        var bits = buckets.ToArray().Select(item => item >= avg ? '1' : '0').ToArray();
        return new string(bits);
    }

    private static string BuildLocalEmbedding(byte[] bytes)
    {
        if (bytes.Length == 0) return "";
        var bins = new int[16];
        foreach (var value in bytes)
        {
            bins[value / 16]++;
        }

        var total = Math.Max(1, bytes.Length);
        return string.Join(",", bins.Select(bin => ((double)bin / total).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static double FingerprintSimilarity(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return 0;
        var same = left.Zip(right).Count(pair => pair.First == pair.Second);
        return (double)same / Math.Max(left.Length, right.Length);
    }

    private static CapturedScore ScoreCapturedSource(CapturedProductSource source, string pHash, string embedding, ProductIdentificationResult? identification)
    {
        var matched = new List<string>();
        var score = 0.0;
        var pHashScore = FingerprintSimilarity(pHash, source.PerceptualHash);
        if (pHashScore > 0)
        {
            score += pHashScore * 0.55;
            matched.Add($"phash:{pHashScore:0.00}");
        }

        var embeddingScore = EmbeddingSimilarity(embedding, source.ImageEmbedding);
        if (embeddingScore > 0)
        {
            score += embeddingScore * 0.25;
            matched.Add($"embedding:{embeddingScore:0.00}");
        }

        if (identification is not null && !string.IsNullOrWhiteSpace(identification.Brand) && source.Brand.Contains(identification.Brand, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.12;
            matched.Add("brand");
        }

        if (identification is not null && (identification.VisibleText ?? []).Any(text => text.Length > 3 && source.ProductName.Contains(text, StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.08;
            matched.Add("visibleText");
        }

        return new CapturedScore(source, Math.Min(1, score), matched.ToArray());
    }

    private static double EmbeddingSimilarity(string left, string right)
    {
        var a = left.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(value => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : 0).ToArray();
        var b = right.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(value => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : 0).ToArray();
        if (a.Length == 0 || a.Length != b.Length) return 0;
        var dot = a.Zip(b, (x, y) => x * y).Sum();
        var magA = Math.Sqrt(a.Sum(x => x * x));
        var magB = Math.Sqrt(b.Sum(x => x * x));
        return magA == 0 || magB == 0 ? 0 : dot / (magA * magB);
    }

    private static string MatchContent(string html, string pattern)
    {
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["v"].Value.Trim()) : "";
    }

    private static string NormalizeDomain(string domain)
    {
        var normalized = domain.Trim().Trim('/').Replace("https://", "", StringComparison.OrdinalIgnoreCase).Replace("http://", "", StringComparison.OrdinalIgnoreCase);
        var slashIndex = normalized.IndexOf('/');
        if (slashIndex >= 0)
        {
            normalized = normalized[..slashIndex];
        }

        return normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? normalized[4..].ToLowerInvariant()
            : normalized.ToLowerInvariant();
    }

    private static string FindRegistryPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "App_Data", "trusted-beauty-source-registry.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "App_Data", "trusted-beauty-source-registry.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "App_Data", "trusted-beauty-source-registry.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "Beauty.Api", "App_Data", "trusted-beauty-source-registry.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "trusted-beauty-source-registry.json")
        };
        return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists) ?? "";
    }

    private static void MarkDomainBlocked(TrustedSourceDomain domain, string reason)
    {
        domain.LastStatus = "Blocked";
        domain.LastError = reason;
        domain.LastIndexedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeKey(string brand, string name, string variant) =>
        NormalizeText($"{brand} {name} {variant}");

    private static string NormalizeText(string value) =>
        Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";

    private static string StripTags(string value) =>
        Regex.Replace(value, "<.*?>", " ").Trim();

    private static string GuessCategory(string value)
    {
        var lower = value.ToLowerInvariant();
        if (lower.Contains("fragrance") || lower.Contains("parfum") || lower.Contains("perfume")) return "Fragrance";
        if (lower.Contains("blush")) return "Blush";
        if (lower.Contains("lip")) return "Lip";
        if (lower.Contains("foundation")) return "Foundation";
        if (lower.Contains("cream") || lower.Contains("serum")) return "Skincare";
        return "";
    }

    private static async Task<byte[]> ReadFormFileBytesAsync(IFormFile image, CancellationToken cancellationToken)
    {
        await using var stream = image.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static string TryGetDomain(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase) : "";

    private static string GuessBrandFromTitle(params string[] values)
    {
        var text = string.Join(" ", values);
        var parts = text.Split(['|', '-', '–', '—'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1].Trim() : "";
    }

    private static string ExtractProductNameFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var node in FlattenJsonLd(document.RootElement))
            {
                if (node.TryGetProperty("name", out var name))
                {
                    return name.ToString();
                }
            }
        }
        catch
        {
        }

        return "";
    }

    private sealed record CandidateScore(TrustedProduct? Product, string ImageUrl, double Score, string[] MatchedFields);
    private sealed record CapturedScore(CapturedProductSource? Source, double Score, string[] MatchedFields);
}

public sealed record TrustedIndexStats(int DomainCount, int ProductCount, int ImageCount, DateTimeOffset LastIndexedAt, IReadOnlyList<TrustedSourceDomain> Domains);

public sealed record TrustedProductAlternative(TrustedProduct Product, string ImageUrl, double Score, string[] MatchedFields);

public sealed record TrustedProductMatchResult(TrustedProduct? Product, string ImageUrl, double Score, string[] MatchedFields, IReadOnlyList<TrustedProductAlternative> Alternatives)
{
    public static TrustedProductMatchResult Empty { get; } = new(null, "", 0, [], []);
}

public sealed record CapturedProductAlternative(CapturedProductSource Source, double Score, string[] MatchedFields);

public sealed record CapturedProductMatchResult(CapturedProductSource? Source, double Score, string[] MatchedFields, IReadOnlyList<CapturedProductAlternative> Alternatives)
{
    public static CapturedProductMatchResult Empty { get; } = new(null, 0, [], []);
}

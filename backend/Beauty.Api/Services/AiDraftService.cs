using Beauty.Api.Data;
using Beauty.Api.Models;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Beauty.Api.Services;

public sealed class AiDraftService(
    BeautyDbContext db,
    IConfiguration configuration,
    HttpClient httpClient,
    PublicUrlValidator publicUrlValidator,
    ProductMatchScorer productMatchScorer,
    TrustedProductIndexService trustedProductIndexService,
    ILogger<AiDraftService> logger)
{
    private const int MaxImageBytes = 8_000_000;
    private const int MaxQualityRewriteAttempts = 1;
    private static readonly TimeSpan GeminiMinCallSpacing = TimeSpan.FromSeconds(5);
    private const string DefaultGeminiModel = "gemini-3.1-flash-lite";
    private static readonly string[] GeminiFallbackModels =
    [
        "gemini-3.1-flash-lite",
        "gemini-3.5-flash",
        "gemini-2.5-flash-lite",
        "gemini-2.5-flash"
    ];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly object RecentContentLock = new();
    private static readonly Queue<RecentSaleContent> RecentSaleContents = new();
    private static readonly object GeminiCooldownLock = new();
    private static DateTimeOffset GeminiNextRequestAt = DateTimeOffset.MinValue;
    private static readonly object GeminiSearchCircuitLock = new();
    private static DateTimeOffset GeminiSearchCircuitOpenUntil = DateTimeOffset.MinValue;
    private static readonly TimeSpan GeminiSearchCircuitDuration = TimeSpan.FromSeconds(45);
    private static readonly HashSet<string> OcrStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "EAU", "DE", "PARFUM", "TOILETTE", "COLOGNE", "ELIXIR", "SPRAY", "NATURAL", "VAPORISATEUR",
        "ML", "FL", "OZ", "MADE", "IN", "THE", "AND", "WITH", "FOR", "NEW", "ORIGINAL", "AUTHENTIC",
        "LIMITED", "NET", "WT", "INGREDIENTS", "WARNING", "DISTRIBUTED", "IMPORT", "EXPORT", "BATCH", "LOT", "REF",
        "CD", "LOGO", "MONOGRAM"
    };
    private static readonly TrustedBeautySourceRegistry BeautySourceRegistry = LoadTrustedBeautySourceRegistry();
    private static readonly BrandOfficialProfile[] BrandOfficialRegistry = BeautySourceRegistry.Brands
        .Where(brand => brand.Enabled)
        .Select(brand => new BrandOfficialProfile(
            brand.Brand,
            brand.Aliases,
            brand.OfficialDomains.Concat(brand.RegionalDomains).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()))
        .ToArray();
    private static readonly string[] TrustedProductPageDomains = BeautySourceRegistry.TrustedRetailers.AllDomains;
    private readonly List<string> officialUrlDiagnostics = [];
    private bool GeminiSearchEnabled =>
        configuration.GetValue("GeminiSearchEnabled", true) &&
        !string.Equals(Environment.GetEnvironmentVariable("GEMINI_SEARCH_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);

    public async Task<AiDraft> CreateDraftAsync(CreateAiDraftRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var content = request.Type == "customer-makeup-image"
            ? $"Bản nháp bài đăng từ ảnh khách: {request.Prompt}. Nội dung này cần admin duyệt trước khi đăng."
            : $"Bản nháp bài sale sản phẩm: {request.Prompt}. Nội dung này không tự đổi giá hoặc kích hoạt sale.";

        var draft = new AiDraft
        {
            Type = request.Type,
            Prompt = request.Prompt,
            SourceImagePrivatePath = request.SourceImagePrivatePath,
            Content = content,
            Status = DraftStatus.Draft,
            CreatedByUserId = userId
        };

        db.AiDrafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);
        return draft;
    }

    public async Task<AiServiceResult<ProductIdentificationResult>> IdentifyProductAsync(IFormFile? image, CancellationToken cancellationToken)
    {
        var credential = GetCredential();
        if (credential is null)
        {
            return AiServiceResult<ProductIdentificationResult>.Fail(400, "Chưa kết nối Gemini API. Vui lòng cấu hình GEMINI_API_KEY ở backend.");
        }

        var imageResult = await ReadValidatedImageAsync(image, cancellationToken);
        if (!imageResult.Success)
        {
            return AiServiceResult<ProductIdentificationResult>.Fail(imageResult.StatusCode, imageResult.Message);
        }

        var prompt = """
        Bạn là chuyên gia nhận diện sản phẩm mỹ phẩm/làm đẹp từ ảnh, ưu tiên độ đúng hơn độ tự tin.
        Nhiệm vụ duy nhất: đọc ảnh bao bì, phân biệt đúng loại sản phẩm và trả JSON nhận diện.

        Quy tắc bắt buộc:
        - Không viết bài sale.
        - Không đoán bừa tên thương mại, dòng sản phẩm, mã màu, finish hoặc phiên bản nếu chữ/logo không đọc rõ.
        - Ưu tiên chữ thật nhìn thấy trên ảnh hơn mọi kiến thức bên ngoài. Nếu bao bì ghi "AMBROSIA D'ORO" thì variant và productName bắt buộc là Ambrosia D'Oro, không được đổi thành Di Fiori hoặc tên phiên bản gần giống.
        - Trước khi trả productName, hãy tự đọc lại visibleText và kiểm tra productName/variant có khớp nguyên văn các cụm chữ lớn trên bao bì không.
        - ProductName phải ghép từ brand + dòng chính + phiên bản thật nhìn thấy. Không tự thay phiên bản bằng sản phẩm khác trong cùng bộ sưu tập.
        - Không trả chuỗi "null", "undefined", "N/A", "không rõ"; field không chắc thì trả chuỗi rỗng.
        - visibleText chỉ gồm các cụm chữ thật sự nhìn thấy trên ảnh; hãy tách rõ từng cụm quan trọng như brand, dòng chính, phiên bản, nồng độ, dung tích.
        - productLine là dòng sản phẩm chính đọc được trên bao bì, ví dụ Dior Addict, Lip Glow, Lip Maximizer, Rouge Dior. Không chắc thì để rỗng.
        - itemForm chỉ được là full-product, case, refill, accessory hoặc unknown.
        - Phải phân biệt sản phẩm chính với vỏ/hộp/case/refill/lõi/phụ kiện.
        - Nếu ảnh là vỏ son, case, hộp rỗng, refill, lõi thay thế hoặc phụ kiện thì category phải ghi đúng loại đó; productName phải có "vỏ", "case", "refill" hoặc "phụ kiện" phù hợp. Không gọi là son/serum/kem nếu không thấy phần sản phẩm sử dụng trực tiếp.
        - Nếu chỉ thấy thương hiệu/logo và hình dáng sản phẩm nhưng không đọc được tên dòng chính xác, productName nên là mô tả an toàn như "Vỏ son [brand] màu/họa tiết..." thay vì bịa tên dòng.
        - Nếu tên thương mại/dòng sản phẩm không đọc rõ trên ảnh, confidence tối đa 70 và needsConfirmation = true.
        - Nếu ảnh mờ, có nhiều sản phẩm, bị che khuất, hoặc có khả năng nhầm giữa các dòng cùng thương hiệu thì confidence dưới 75 và needsConfirmation = true.
        - Chỉ đặt confidence từ 90 trở lên khi nhìn rõ brand + tên dòng + loại sản phẩm + phiên bản/màu hoặc có dấu hiệu nhận diện rất đặc trưng không gây nhầm.
        - shade là mã màu/tên màu nếu nhìn thấy rõ; finish là finish/kết cấu như velvet, satin, matte, serum, oil nếu nhìn thấy rõ.
        - searchQuery phải ưu tiên tên an toàn để xác minh: brand + productName + category + official.
        - Chỉ trả JSON đúng schema, không markdown, không giải thích ngoài JSON.

        Ví dụ nguyên tắc:
        - Ảnh chỉ thấy ống/vỏ Dior họa tiết hồng, không thấy thỏi son hoặc tên dòng rõ: nhận là vỏ/case Dior màu hồng họa tiết, cần xác nhận; không tự gán thành Rouge Dior Lipstick.
        - Ảnh thấy bao bì ghi rõ "Dior Addict Lipstick Fashion Case Pink Oblique": nhận đúng là vỏ/case son Dior Addict Fashion Case Pink Oblique, không mô tả như thỏi son.
        - Phân biệt Dior theo toàn bộ chữ nhìn thấy: "LIP GLOW OIL" là Dior Addict Lip Glow Oil; "LIP GLOW" hoặc "COLOR REVIVER BALM" là Dior Addict Lip Glow balm; "LIP MAXIMIZER" là Dior Addict Lip Maximizer; "REFILL" là lõi; "CASE" là vỏ.
        - Ảnh thấy chai/hộp YSL có chữ "LIBRE" và "BERRY CRUSH": nhận đúng là Yves Saint Laurent Libre Berry Crush, category là nước hoa, variant là Berry Crush. Không được nhận là cushion/foundation/makeup face.
        - Ảnh Gucci Bloom có chữ "AMBROSIA D'ORO": productName là "Gucci Bloom Ambrosia D'Oro", variant là "Ambrosia D'Oro". Không trả "Ambrosia Di Fiori".
        """;

        var body = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = imageResult.Image!.MimeType,
                                data = imageResult.Image.Base64
                            }
                        }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.0,
                topP = 0.2,
                maxOutputTokens = 1200,
                response_mime_type = "application/json",
                response_schema = ProductIdentificationSchema()
            }
        };

        var gemini = await SendGeminiAsync(
            body,
            credential,
            retryJson: true,
            cancellationToken,
            allowTransientRetries: true,
            requestTimeout: TimeSpan.FromSeconds(120));
        if (!gemini.Success)
        {
            return AiServiceResult<ProductIdentificationResult>.Fail(gemini.StatusCode, gemini.Message);
        }

        var parseResult = TryParseJson<ProductIdentificationResult>(gemini.Text, out var identification);
        if (!parseResult || identification is null)
        {
            return AiServiceResult<ProductIdentificationResult>.Fail(502, "Gemini đã phản hồi nhưng JSON nhận diện không hợp lệ. Vui lòng thử lại.");
        }

        identification = NormalizeIdentification(identification);
        return AiServiceResult<ProductIdentificationResult>.Ok(identification);
    }

    public async Task<AiServiceResult<SaleContentResult>> WriteSaleContentAsync(ConfirmedProductRequest request, CancellationToken cancellationToken)
    {
        request = NormalizeConfirmedProductRequest(request);
        BeginContentLogSection("VIẾT BÀI SALE", FirstNonEmpty(request.ProductName, request.Brand, request.SearchQuery));

        var credential = GetCredential();
        if (credential is null)
        {
            return AiServiceResult<SaleContentResult>.Fail(400, "Chưa kết nối Gemini API. Vui lòng cấu hình GEMINI_API_KEY ở backend.");
        }

        var productName = request.ProductName.Trim();
        if (string.IsNullOrWhiteSpace(productName))
        {
            return AiServiceResult<SaleContentResult>.Fail(400, "Vui lòng xác nhận tên sản phẩm trước khi viết bài sale.");
        }

        if (string.IsNullOrWhiteSpace(request.OfficialProductUrl))
        {
            return AiServiceResult<SaleContentResult>.Fail(
                400,
                "Không có URL đã được backend xác minh nên chưa được viết bài sale. Vui lòng tìm URL hoặc dán URL trang sản phẩm để xác minh trước.");
        }

        return await WriteSaleContentFromOfficialUrlAsync(request, productName, credential, cancellationToken);
    }

    public async Task<AiServiceResult<OfficialProductUrlResult>> FindOfficialProductUrlAsync(
        ConfirmedProductRequest request,
        CancellationToken cancellationToken)
    {
        request = NormalizeConfirmedProductRequest(request);
        BeginContentLogSection("TÌM URL BẰNG THÔNG TIN", FirstNonEmpty(request.ProductName, request.Brand, request.SearchQuery));

        var credential = GetCredential();
        if (credential is null)
        {
            return AiServiceResult<OfficialProductUrlResult>.Fail(400, "Chưa kết nối Gemini API. Vui lòng cấu hình GEMINI_API_KEY ở backend.");
        }

        var productName = request.ProductName.Trim();
        if (string.IsNullOrWhiteSpace(productName))
        {
            return AiServiceResult<OfficialProductUrlResult>.Fail(400, "Vui lòng xác nhận tên sản phẩm trước khi tìm URL chính hãng.");
        }

        if (IsUnderspecifiedProductUrlRequest(request, productName))
        {
            AddOfficialUrlDiagnostic("Không gọi tìm URL vì dữ liệu nhận diện còn quá chung, chưa đủ tên dòng/phiên bản/mã màu để xác định product page.");
            var underspecifiedResult = new OfficialProductUrlResult(
                "",
                "",
                "",
                "Đã đọc được thương hiệu/loại sản phẩm nhưng chưa đủ tên dòng, phiên bản hoặc mã màu để xác minh URL trang chi tiết sản phẩm. Vui lòng nhập thêm tên dòng hoặc dán URL product page chính xác.");
            LogOfficialUrlOutcome("text", request, productName, underspecifiedResult);
            return AiServiceResult<OfficialProductUrlResult>.Ok(underspecifiedResult);
        }

        using var discoveryTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        discoveryTimeout.CancelAfter(TimeSpan.FromSeconds(90));
        var sources = await DiscoverOfficialProductSourcesAsync(request, productName, credential, discoveryTimeout.Token);
        var source = sources.FirstOrDefault();
        var result = BuildOfficialProductUrlResult(
            sources,
            source is null
                ? BuildOfficialUrlFailureMessage("Chưa tìm thấy nguồn tham khảo đáng tin cậy đủ khớp cho sản phẩm này.")
                : BuildSourceFoundMessage(source));
        LogOfficialUrlOutcome("text", request, productName, result);
        return AiServiceResult<OfficialProductUrlResult>.Ok(result);
    }

    public async Task<AiServiceResult<OfficialProductUrlResult>> FindOfficialProductUrlFromImageAsync(
        ConfirmedProductRequest request,
        IFormFile? image,
        CancellationToken cancellationToken)
    {
        request = NormalizeConfirmedProductRequest(request);
        BeginContentLogSection("TÌM URL TỪ ẢNH", FirstNonEmpty(request.ProductName, request.Brand, request.SearchQuery, image?.FileName ?? ""));

        AddOfficialUrlDiagnostic("UPLOAD_RECEIVED");
        var captureOnlyResult = await TryFindCapturedProductSourceAsync(image, null, cancellationToken);
        if (captureOnlyResult is not null)
        {
            AddOfficialUrlDiagnostic("WORKFLOW_COMPLETED");
            LogOfficialUrlOutcome("source-capture", request, captureOnlyResult.Identification?.ProductName ?? "", captureOnlyResult);
            return AiServiceResult<OfficialProductUrlResult>.Ok(captureOnlyResult);
        }

        var credential = GetCredential();
        if (credential is null)
        {
            return AiServiceResult<OfficialProductUrlResult>.Fail(400, "Chưa kết nối Gemini API. Vui lòng cấu hình GEMINI_API_KEY ở backend.");
        }

        using var discoveryTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        discoveryTimeout.CancelAfter(TimeSpan.FromSeconds(90));
        var stopwatch = Stopwatch.StartNew();
        AddOfficialUrlDiagnostic("image received");
        AddOfficialUrlDiagnostic("image uploaded");

        try
        {
            AddOfficialUrlDiagnostic("VISION_STARTED");
            var identificationResult = await IdentifyProductAsync(image, discoveryTimeout.Token);
            if (!identificationResult.Success || identificationResult.Data is null)
            {
                AddOfficialUrlDiagnostic($"image analysis failed: {identificationResult.Message}");
                return AiServiceResult<OfficialProductUrlResult>.Fail(identificationResult.StatusCode, identificationResult.Message);
            }

            var identification = NormalizeIdentificationForUrlSearch(identificationResult.Data);
            AddOfficialUrlDiagnostic($"image analyzed in {stopwatch.ElapsedMilliseconds}ms");
            AddOfficialUrlDiagnostic($"visibleText extracted: {ShortDiagnostic(string.Join(" | ", identification.VisibleText))}");
            AddOfficialUrlDiagnostic($"visible text extracted: {ShortDiagnostic(string.Join(" | ", identification.VisibleText))}");
            AddOfficialUrlDiagnostic($"predicted product: {ShortDiagnostic(identification.ProductName)}");
            AddOfficialUrlDiagnostic($"Tên sản phẩm dự đoán: {ShortDiagnostic(identification.ProductName)}");

            var searchRequest = BuildConfirmedProductRequestFromIdentification(request, identification);
            var imageSearchName = searchRequest.ProductName.Trim();
            if (string.IsNullOrWhiteSpace(imageSearchName))
            {
                imageSearchName = identification.SearchQuery;
            }

            var capturedResult = await TryFindCapturedProductSourceAsync(image, identification, discoveryTimeout.Token);
            if (capturedResult is not null)
            {
                AddOfficialUrlDiagnostic("WORKFLOW_COMPLETED");
                LogOfficialUrlOutcome("source-capture-vision", searchRequest, imageSearchName, capturedResult);
                return AiServiceResult<OfficialProductUrlResult>.Ok(capturedResult);
            }

            var indexedResult = await TryFindTrustedIndexedProductAsync(image, identification, discoveryTimeout.Token);
            if (indexedResult is not null)
            {
                AddOfficialUrlDiagnostic("WORKFLOW_COMPLETED");
                LogOfficialUrlOutcome("image-index", searchRequest, imageSearchName, indexedResult);
                return AiServiceResult<OfficialProductUrlResult>.Ok(indexedResult);
            }

            AddOfficialUrlDiagnostic("Kho nguồn chưa có ứng viên đủ khớp; cập nhật lại nguồn liên quan rồi tìm lại một lần.");
            await trustedProductIndexService.IndexConfiguredSourcesAsync(FirstNonEmpty(identification.Brand, searchRequest.Brand), discoveryTimeout.Token);
            indexedResult = await TryFindTrustedIndexedProductAsync(image, identification, discoveryTimeout.Token);
            if (indexedResult is not null)
            {
                AddOfficialUrlDiagnostic("WORKFLOW_COMPLETED");
                LogOfficialUrlOutcome("image-index-refresh", searchRequest, imageSearchName, indexedResult);
                return AiServiceResult<OfficialProductUrlResult>.Ok(indexedResult);
            }

            if (!GeminiSearchEnabled)
            {
                var indexOnlyResult = new OfficialProductUrlResult(
                    "",
                    "",
                    "",
                    "Kho nguồn chưa có sản phẩm này, vui lòng cập nhật nguồn hoặc dán URL thủ công.")
                {
                    Identification = identification
                };
                AddOfficialUrlDiagnostic("WORKFLOW_COMPLETED");
                LogOfficialUrlOutcome("image-index-only", searchRequest, imageSearchName, indexOnlyResult);
                return AiServiceResult<OfficialProductUrlResult>.Ok(indexOnlyResult);
            }

            var queries = BuildGroundedProductSearchQueries(searchRequest, imageSearchName).Take(4).ToArray();
            AddOfficialUrlDiagnostic($"search queries generated: {string.Join(" || ", queries)}");
            var sources = await DiscoverOfficialProductSourcesAsync(searchRequest, imageSearchName, credential, discoveryTimeout.Token);

            var source = sources.FirstOrDefault();
            var result = BuildOfficialProductUrlResult(
                sources,
                source is null
                    ? BuildOfficialUrlFailureMessage("Chưa tìm thấy nguồn tham khảo đáng tin cậy đủ khớp từ hình ảnh này.")
                    : BuildSourceFoundMessage(source));
            if (source is not null)
            {
                result = result with
                {
                    Identification = identification with
                    {
                        NeedsConfirmation = false,
                        Message = "Đã nhận diện sản phẩm sau khi xác minh nguồn web đáng tin cậy."
                    }
                };
                AddOfficialUrlDiagnostic($"URL cuối được chọn: {source.Url}");
                AddOfficialUrlDiagnostic("identification saved");
                AddOfficialUrlDiagnostic("official identification saved");
            }
            AddOfficialUrlDiagnostic($"image URL workflow completed in {stopwatch.ElapsedMilliseconds}ms");
            AddOfficialUrlDiagnostic("WORKFLOW_COMPLETED");

            LogOfficialUrlOutcome("image", searchRequest, imageSearchName, result);
            return AiServiceResult<OfficialProductUrlResult>.Ok(result);
        }
        catch (OperationCanceledException) when (discoveryTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            AddOfficialUrlDiagnostic($"Lỗi thật gây timeout: deadline 90 giây của backend đã hết sau {stopwatch.ElapsedMilliseconds}ms.");
            logger.LogWarning(
                "Official URL image workflow timed out after {ElapsedMs}ms. Diagnostics={Diagnostics}",
                stopwatch.ElapsedMilliseconds,
                string.Join(" | ", officialUrlDiagnostics));
            return AiServiceResult<OfficialProductUrlResult>.Fail(
                504,
                BuildOfficialUrlFailureMessage("Không tìm được URL sản phẩm đáng tin cậy từ ảnh trong deadline 90 giây."));
        }
    }

    public async Task<AiServiceResult<OfficialProductUrlResult>> VerifyOfficialProductUrlAsync(
        VerifyProductUrlRequest request,
        CancellationToken cancellationToken)
    {
        var product = NormalizeConfirmedProductRequest(request.Product);
        BeginContentLogSection("XÁC MINH URL", FirstNonEmpty(product.ProductName, product.Brand, request.Url));
        var productName = product.ProductName.Trim();
        if (string.IsNullOrWhiteSpace(productName))
        {
            return AiServiceResult<OfficialProductUrlResult>.Fail(400, "Vui lòng xác nhận tên sản phẩm trước khi xác minh URL.");
        }

        var publicUrl = await publicUrlValidator.ValidateAsync(request.Url, cancellationToken);
        if (!publicUrl.Success || publicUrl.Uri is null)
        {
            return AiServiceResult<OfficialProductUrlResult>.Fail(400, publicUrl.Message);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var sourceResult = await ReadOfficialProductPageAsync(publicUrl.Uri.ToString(), product, productName, timeout.Token);
        if (!sourceResult.Success || sourceResult.Source is null)
        {
            return AiServiceResult<OfficialProductUrlResult>.Ok(new OfficialProductUrlResult(
                "",
                "",
                "",
                $"Không tìm thấy URL tin cậy cho đúng sản phẩm. {sourceResult.Message}"));
        }

        var score = productMatchScorer.Score(product, sourceResult.Source.Url, sourceResult.Source.Title, sourceResult.Source.Content);
        if (!score.Accepted)
        {
            return AiServiceResult<OfficialProductUrlResult>.Ok(new OfficialProductUrlResult(
                "",
                "",
                "",
                $"URL nhập tay chưa đủ khớp với sản phẩm. score={score.Score}; matched={string.Join(",", score.Reasons)}"));
        }

        var source = new GroundedSource(
            sourceResult.Source.Website,
            sourceResult.Source.Title,
            sourceResult.Source.Url,
            productMatchScorer.SourceType(sourceResult.Source.Url, product.Brand),
            score.Score,
            score.Reasons);
        return AiServiceResult<OfficialProductUrlResult>.Ok(BuildOfficialProductUrlResult(
            [source],
            "URL nhập tay đã được backend xác minh đúng sản phẩm."));
    }

    private static OfficialProductUrlResult BuildOfficialProductUrlResult(IReadOnlyList<GroundedSource> sources, string message)
    {
        var ordered = sources
            .OrderBy(source => SourceTypeRank(source.SourceType))
            .ThenByDescending(source => source.Confidence)
            .Take(3)
            .ToArray();
        var best = ordered.FirstOrDefault();
        return new OfficialProductUrlResult(
            best?.Url ?? "",
            best?.Title ?? "",
            best?.Website ?? "",
            message)
        {
            Brand = best is null ? "" : GuessBrandFromSource(best),
            SourceType = best?.SourceType ?? "",
            Confidence = best?.Confidence ?? 0,
            MatchedFields = best?.MatchedFields ?? [],
            Sources = ordered
        };
    }

    private async Task<OfficialProductUrlResult?> TryFindTrustedIndexedProductAsync(
        IFormFile? image,
        ProductIdentificationResult identification,
        CancellationToken cancellationToken)
    {
        AddOfficialUrlDiagnostic("Tìm ứng viên trong Trusted Website Product Index.");
        var match = await trustedProductIndexService.MatchUploadedImageAsync(image, identification, cancellationToken);
        if (match.Product is null)
        {
            AddOfficialUrlDiagnostic("Trusted index không có ứng viên ảnh tương đồng.");
            return null;
        }

        AddOfficialUrlDiagnostic($"Trusted index top match: {match.Product.ProductName} | {match.Product.CanonicalUrl} | score={match.Score:0.00}");
        if (match.Score < 0.72)
        {
            AddOfficialUrlDiagnostic("Trusted index có ứng viên nhưng điểm chưa vượt ngưỡng an toàn; trả ứng viên để người dùng chọn nếu cần.");
            return null;
        }

        var source = new GroundedSource(
            match.Product.SourceDomain,
            match.Product.ProductName,
            match.Product.CanonicalUrl,
            match.Product.SourceType,
            (int)Math.Round(match.Score * 100),
            match.MatchedFields);
        var result = BuildOfficialProductUrlResult(
            [source],
            "Đã tìm được sản phẩm từ Trusted Website Product Index, không gọi Gemini Google Search.")
            with
            {
                Identification = identification with
                {
                    ProductName = FirstNonEmpty(match.Product.ProductName, identification.ProductName),
                    Brand = FirstNonEmpty(match.Product.Brand, identification.Brand),
                    ProductLine = FirstNonEmpty(match.Product.ProductLine, identification.ProductLine),
                    Variant = FirstNonEmpty(match.Product.Variant, identification.Variant),
                    Shade = FirstNonEmpty(match.Product.Shade, identification.Shade),
                    Category = FirstNonEmpty(match.Product.Category, identification.Category),
                    ItemForm = FirstNonEmpty(match.Product.ItemForm, identification.ItemForm),
                    Size = FirstNonEmpty(match.Product.Size, identification.Size),
                    NeedsConfirmation = false,
                    Message = "Đã nhận diện chính thức bằng Trusted Website Product Index."
                }
            };
        AddOfficialUrlDiagnostic($"URL cuối được chọn từ trusted index: {match.Product.CanonicalUrl}");
        return result;
    }

    private async Task<OfficialProductUrlResult?> TryFindCapturedProductSourceAsync(
        IFormFile? image,
        ProductIdentificationResult? identification,
        CancellationToken cancellationToken)
    {
        AddOfficialUrlDiagnostic("SOURCE_HASH_LOOKUP");
        var match = await trustedProductIndexService.MatchCapturedSourceAsync(image, identification, cancellationToken);
        if (match.Source is null)
        {
            AddOfficialUrlDiagnostic("SOURCE_NOT_FOUND");
            return null;
        }

        var source = match.Source;
        AddOfficialUrlDiagnostic($"SOURCE_MATCHED: {source.ProductName} | {source.CanonicalUrl} | score={match.Score:0.00} | {string.Join(",", match.MatchedFields)}");
        if (match.Score < 0.72)
        {
            AddOfficialUrlDiagnostic("Source capture có ứng viên nhưng điểm chưa đủ chắc; cần người dùng chọn.");
            return null;
        }

        var grounded = new GroundedSource(
            source.SourceDomain,
            source.ProductName,
            source.CanonicalUrl,
            "captured-source",
            (int)Math.Round(match.Score * 100),
            match.MatchedFields);
        var resultIdentification = new ProductIdentificationResult(
            source.ProductName,
            source.Brand,
            "",
            "",
            "",
            "",
            "",
            "full-product",
            "",
            [],
            (int)Math.Round(match.Score * 100),
            "",
            false,
            "Đã nhận diện chính thức từ nguồn đã lưu bằng extension.");
        var result = BuildOfficialProductUrlResult(
            [grounded],
            "Đã tìm đúng trang nguồn đã lưu.")
            with
            {
                Identification = resultIdentification
            };
        return result;
    }

    private static string BuildSourceFoundMessage(GroundedSource source) =>
        source.SourceType is "official" or "official-regional" or "official-document"
            ? "Đã tìm được nguồn chính thức phù hợp với sản phẩm."
            : "Không có trang hãng còn hoạt động; đã tìm được nguồn bán lẻ uy tín phù hợp với sản phẩm.";

    private static int SourceTypeRank(string sourceType) => sourceType switch
    {
        "official" => 0,
        "official-regional" => 1,
        "official-document" => 2,
        "authorized-retailer" => 3,
        "trusted-retailer" => 4,
        _ => 9
    };

    private static IReadOnlyList<GroundedSource> FilterVisualProductPageSources(IEnumerable<GroundedSource> sources) =>
        (sources ?? [])
            .Where(source => Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https" &&
                !IsNonPublicCommerceRoute(uri) &&
                !ProductMatchScorerLooksLikeContainer(uri))
            .GroupBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(8)
            .ToArray();

    private static bool ProductMatchScorerLooksLikeContainer(Uri uri)
    {
        var path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        return last is "home" or "homepage" or "shop" or "products" or "product" or "collections" or "collection" or "search" or "category" or "categories";
    }

    private static ProductIdentificationResult BuildIdentificationFromTrustedSource(GroundedSource source, ConfirmedProductRequest request)
    {
        var title = CleanInline(source.Title);
        var brand = string.IsNullOrWhiteSpace(request.Brand)
            ? GuessBrandFromSource(source)
            : request.Brand;
        var category = string.IsNullOrWhiteSpace(request.Category)
            ? GuessCategoryFromText($"{title} {source.Url}")
            : request.Category;
        var itemForm = NormalizeItemForm(request.ItemForm, category, title);
        return NormalizeIdentification(new ProductIdentificationResult(
            ProductName: title,
            Brand: brand,
            ProductLine: request.ProductLine,
            Variant: request.Variant,
            Shade: request.Shade,
            Finish: request.Finish,
            Category: category,
            ItemForm: itemForm,
            Size: request.Size,
            VisibleText: [title, source.Website],
            Confidence: Math.Max(80, source.Confidence),
            SearchQuery: $"{brand} {title}",
            NeedsConfirmation: false,
            Message: $"Đã nhận diện sản phẩm từ nguồn đã xác minh: {source.Website}"));
    }

    private static ConfirmedProductRequest BuildConfirmedProductRequestFromIdentification(
        ConfirmedProductRequest original,
        ProductIdentificationResult identification)
    {
        var visibleText = CleanInline(string.Join(" ", identification.VisibleText ?? []));
        return original with
        {
            ProductName = FirstNonEmpty(identification.ProductName, original.ProductName),
            Brand = FirstNonEmpty(identification.Brand, original.Brand),
            ProductLine = FirstNonEmpty(identification.ProductLine, original.ProductLine),
            Variant = FirstNonEmpty(identification.Variant, original.Variant),
            Shade = FirstNonEmpty(identification.Shade, original.Shade),
            Finish = FirstNonEmpty(identification.Finish, original.Finish),
            Category = FirstNonEmpty(identification.Category, original.Category),
            ItemForm = FirstNonEmpty(identification.ItemForm, original.ItemForm),
            Size = FirstNonEmpty(identification.Size, original.Size),
            SearchQuery = FirstNonEmpty(visibleText, identification.SearchQuery, original.SearchQuery),
            UserConfirmed = false,
            OfficialProductUrl = ""
        };
    }

    private static ProductIdentificationResult NormalizeIdentificationForUrlSearch(ProductIdentificationResult identification)
    {
        identification = NormalizeIdentification(identification);
        var visibleText = (identification.VisibleText ?? [])
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var normalizedText = RemoveVietnameseDiacritics(string.Join(" ", visibleText)).ToUpperInvariant();
        if (Regex.IsMatch(normalizedText, @"\bDIOR\b") &&
            Regex.IsMatch(normalizedText, @"\bADDICT\b") &&
            Regex.IsMatch(normalizedText, @"\bLIP\s+GLOW\s+OIL\b"))
        {
            var size = Regex.IsMatch(normalizedText, @"\b6\s*ML\b") ? "6 ml" : identification.Size;
            return identification with
            {
                Brand = "Dior",
                ProductLine = "Dior Addict",
                ProductName = "Dior Addict Lip Glow Oil",
                Variant = "Dior Addict",
                Category = "Lip oil",
                ItemForm = "full-product",
                Size = size,
                VisibleText = visibleText,
                Confidence = Math.Max(identification.Confidence, 92),
                SearchQuery = CleanInline(string.Join(" ", visibleText)),
                NeedsConfirmation = false,
                Message = "Đã đọc được Dior Addict Lip Glow Oil từ chữ thật trên bao bì."
            };
        }

        return identification;
    }

    private static string GuessBrandFromSource(GroundedSource source)
    {
        var text = RemoveVietnameseDiacritics($"{source.Website} {source.Title}").ToLowerInvariant();
        foreach (var profile in BrandOfficialRegistry)
        {
            if (profile.Aliases.Any(alias => text.Contains(RemoveVietnameseDiacritics(alias).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) ||
                profile.Domains.Any(domain => text.Contains(domain, StringComparison.OrdinalIgnoreCase)))
            {
                return profile.CanonicalName;
            }
        }

        return source.Website.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    }

    private static string GuessCategoryFromText(string value)
    {
        var text = RemoveVietnameseDiacritics(value).ToLowerInvariant();
        if (text.Contains("foundation") || text.Contains("cushion") || text.Contains("powder")) return "makeup face";
        if (text.Contains("lip glow") || text.Contains("balm")) return "lip balm";
        if (text.Contains("lip oil")) return "lip oil";
        if (text.Contains("lip maximizer") || text.Contains("gloss")) return "lip gloss";
        if (text.Contains("lipstick") || text.Contains("rouge")) return "lipstick";
        if (text.Contains("parfum") || text.Contains("perfume") || text.Contains("fragrance")) return "fragrance";
        return "";
    }

    private static ConfirmedProductRequest NormalizeConfirmedProductRequest(ConfirmedProductRequest request) =>
        request with
        {
            ProductName = request.ProductName ?? "",
            Brand = NormalizeOfficialBrandName(request.Brand ?? ""),
            ProductLine = request.ProductLine ?? "",
            Variant = request.Variant ?? "",
            Shade = request.Shade ?? "",
            Finish = request.Finish ?? "",
            Category = request.Category ?? "",
            ItemForm = NormalizeItemForm(request.ItemForm ?? "", request.Category ?? "", request.ProductName ?? ""),
            Size = request.Size ?? "",
            SearchQuery = request.SearchQuery ?? "",
            OfficialProductUrl = request.OfficialProductUrl ?? "",
            Price = request.Price ?? "",
            SalePrice = request.SalePrice ?? "",
            Gift = request.Gift ?? "",
            ShopName = request.ShopName ?? "",
            Phone = request.Phone ?? "",
            Address = request.Address ?? "",
            Website = request.Website ?? "",
            RemainingQuantity = request.RemainingQuantity ?? "",
            PreviousCreativeDirection = request.PreviousCreativeDirection ?? ""
        };

    private static bool IsUnderspecifiedProductUrlRequest(ConfirmedProductRequest request, string productName)
    {
        var brandText = CleanInline(request.Brand);
        var identityText = CleanInline(string.Join(" ", new[]
        {
            productName,
            request.ProductName,
            request.ProductLine,
            request.Variant,
            request.Shade,
            request.Category,
            request.SearchQuery
        }.Where(value => !string.IsNullOrWhiteSpace(value))));

        if (!string.IsNullOrWhiteSpace(brandText) && !string.IsNullOrWhiteSpace(identityText))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(brandText) && string.IsNullOrWhiteSpace(identityText))
        {
            return true;
        }

        var brandTokens = RemoveVietnameseDiacritics(request.Brand)
            .ToLowerInvariant()
            .Split([' ', '-', '_', '/', '.', '\'', '"', '’', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var genericTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "official", "product", "beauty", "makeup", "skincare", "cosmetic", "cosmetics",
            "face", "foundation", "foundations", "cushion", "powder", "concealer", "primer",
            "lip", "lips", "lipstick", "son", "moi", "balm", "gloss", "oil",
            "fragrance", "perfume", "parfum", "eau", "toilette", "nuoc", "hoa",
            "cream", "serum", "gel", "lotion", "mascara", "eyeliner", "palette",
            "kem", "nen", "phan", "duong", "dior", "christian", "paris"
        };
        var tokens = RemoveVietnameseDiacritics(string.Join(" ", new[]
            {
                productName,
                request.ProductName,
                request.ProductLine,
                request.Variant,
                request.Shade,
                request.Finish,
                request.SearchQuery
            }.Where(value => !string.IsNullOrWhiteSpace(value))))
            .ToLowerInvariant()
            .Split([' ', '-', '_', '/', '.', '\'', '"', '’', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .Where(token => !brandTokens.Contains(token))
            .Where(token => !genericTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return string.IsNullOrWhiteSpace(brandText) && tokens.Length == 0;
    }

    private async Task<AiServiceResult<SaleContentResult>> WriteSaleContentWithGoogleSearchAsync(
        ConfirmedProductRequest request,
        string productName,
        string credential,
        CancellationToken cancellationToken)
    {
        var query = BuildSearchQuery(request, productName);
        var discoveredOfficialSources = await DiscoverOfficialProductSourcesAsync(request, productName, credential, cancellationToken);
        var researchPrompt = $$"""
        Bạn là chuyên gia research sản phẩm làm đẹp trước khi viết bài sale.
        Hãy dùng Google Search để tìm nguồn đáng tin cậy nhất cho sản phẩm sau:

        Sản phẩm: {{productName}}
        Thương hiệu: {{request.Brand}}
        Phiên bản/dòng: {{request.Variant}}
        Loại sản phẩm: {{request.Category}}
        Dung tích/trọng lượng: {{request.Size}}
        Query gợi ý: {{query}}

        Quy tắc chọn nguồn:
        - Bắt buộc ưu tiên URL trang sản phẩm cụ thể, ví dụ dạng /.../libre-eau-de-parfum/...html hoặc /product/ten-san-pham. Không dùng trang chủ, trang danh mục, trang search, trang collection.
        - Nếu biết website chính thức của thương hiệu, hãy tìm trực tiếp trong website đó trước bằng truy vấn site:. URL trả về phải là trang chi tiết sản phẩm, không phải trang chủ thương hiệu.
        - Nếu sản phẩm là Eau de Parfum thì không lấy trang Eau de Toilette, Le Parfum, Intense hoặc phiên bản khác. Nếu sản phẩm là Eau de Toilette thì không lấy Eau de Parfum.
        - productPageUrl phải là full URL bắt đầu bằng https:// và phải khớp đúng tên sản phẩm + phiên bản/nồng độ. Nếu không chắc đúng phiên bản thì để trống.
        - Ưu tiên website chính thức của thương hiệu, trang sản phẩm chính hãng, nhà phân phối/retailer lớn, hoặc nguồn chuyên ngành có dữ liệu sản phẩm rõ.
        - Không lấy blog copy nội dung, web rao vặt, marketplace mơ hồ, trang không khớp đúng phiên bản/dung tích.
        - Nếu có nhiều nguồn, hãy chọn 1-3 nguồn đáng tin nhất và chỉ dùng chi tiết khớp đúng sản phẩm.
        - Với nước hoa, ưu tiên thông tin nhóm mùi, note hương, nồng độ, dung tích, phong cách sử dụng; không tự phóng đại độ bám/tỏa nếu nguồn không nói.
        - Với mỹ phẩm/skincare/makeup, ưu tiên công dụng, texture, loại da/nhu cầu phù hợp, cách dùng và lưu ý; không tự bịa claim y khoa.

        Trả về bản ghi research tiếng Việt, ngắn nhưng giàu dữ liệu, gồm:
        1. productPageUrl: URL trang sản phẩm cụ thể nhất tìm được, đặt riêng một dòng. Nếu chỉ tìm được trang chủ/danh mục/sai phiên bản thì để trống.
        2. Nguồn đáng tin nhất và vì sao đáng tin.
        3. Các thông tin chắc chắn có thể dùng để viết bài sale.
        4. Điểm hấp dẫn nhất để thuyết phục khách mua.
        5. Điều cần tránh nói quá hoặc chưa đủ căn cứ.
        """;

        var researchBody = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = researchPrompt } }
                }
            },
            tools = new object[]
            {
                new { google_search = new { } }
            },
            generationConfig = new
            {
                temperature = 0.18,
                topP = 0.72,
                maxOutputTokens = 2400
            }
        };

        var research = await SendGeminiAsync(
            researchBody,
            credential,
            retryJson: false,
            cancellationToken,
            allowTransientRetries: false,
            requestTimeout: TimeSpan.FromSeconds(12));
        if (!research.Success)
        {
            logger.LogWarning(
                "Google Search research failed, falling back to confirmed product data. Product={Product}; Status={Status}; Message={Message}",
                productName,
                research.StatusCode,
                research.Message);

            return await WriteSaleContentWithoutSearchAsync(
                request,
                productName,
                credential,
                "Chưa lấy được nguồn Google lần này vì Gemini/Search đang quá tải hoặc bị giới hạn. Bài được viết tạm từ thông tin sản phẩm đã xác nhận, không bịa dữ liệu ngoài.",
                discoveredOfficialSources,
                cancellationToken);
        }

        var candidateSources = FilterProductPageSources(
            discoveredOfficialSources
                .Concat(ExtractGroundingSources(research.RawJson))
                .Concat(ExtractSourcesFromResearchText(research.Text)),
            request,
            productName);
        var sources = await ValidateOfficialProductSourcesAsync(candidateSources, request, productName, cancellationToken);

        var verifiedFacts = BuildGoogleResearchSourceContent(request, productName, research.Text, sources);
        var salePrompt = BuildSalePrompt(request, productName, query, BuildAntiRepeatInstruction(request), verifiedFacts);
        var prompt = $$"""
        {{salePrompt}}

        Yêu cầu riêng cho bản này:
        - Bài phải dựa vào phần research Google ở trên, nhưng không nhắc "theo Google" trong bài bán hàng.
        - Nếu nguồn là official/retailer lớn, hãy tận dụng chi tiết thật để bài có sức nặng.
        - Không bê nguyên văn nguồn; chuyển thành ngôn ngữ tư vấn bán hàng gần khách, có gu và có lý do mua rõ.
        """;

        var body = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.56,
                topP = 0.86,
                maxOutputTokens = 3900,
                response_mime_type = "application/json",
                response_schema = SaleCreativeSchema()
            }
        };

        var gemini = await SendGeminiAsync(body, credential, retryJson: false, cancellationToken, allowTransientRetries: true);
        if (!gemini.Success)
        {
            return AiServiceResult<SaleContentResult>.Fail(gemini.StatusCode, gemini.Message);
        }

        var parseResult = TryParseJson<SaleCreativeResult>(gemini.Text, out var creativeContent);
        if (!parseResult || creativeContent is null)
        {
            return AiServiceResult<SaleContentResult>.Fail(502, "Gemini đã phản hồi nhưng JSON bài sale không hợp lệ. Vui lòng thử lại.");
        }

        if ((creativeContent.Highlights ?? []).Count != 4)
        {
            return AiServiceResult<SaleContentResult>.Fail(502, "Gemini chưa trả đúng 4 lợi ích bán hàng. Vui lòng thử lại.");
        }

        var saleContent = NormalizeSaleContent(MapCreativeToSaleContent(creativeContent, request), request);
        if (IsLowQualitySaleContent(saleContent))
        {
            var qualityRetry = await TryRegenerateSaleContentForQualityAsync(body, credential, request, cancellationToken);
            if (qualityRetry.Content is null)
            {
                return AiServiceResult<SaleContentResult>.Fail(422, "Bản nháp chưa đạt chất lượng bán hàng. Hệ thống đã chặn các câu quá chung chung, vui lòng bấm Viết lại để tạo bản gần khách hơn.");
            }

            saleContent = qualityRetry.Content;
        }

        saleContent = saleContent with
        {
            ResearchSuccessful = true,
            WarningMessage = sources.Count > 0
                ? "Đã xác minh URL trang sản phẩm chính hãng trước khi viết bài sale."
                : "Chưa xác minh được URL trang sản phẩm chính hãng chính xác. Bài được viết từ thông tin sản phẩm đã xác nhận.",
            VerifiedDetails = saleContent.VerifiedDetails with
            {
                Sources = sources
            }
        };

        await SaveSaleDraftAsync($"Google Search research: {productName}", request.ProductName, ComposeMainArticle(saleContent), cancellationToken);
        RememberSaleContent(saleContent);
        return AiServiceResult<SaleContentResult>.Ok(saleContent);
    }

    private async Task<IReadOnlyList<GroundedSource>> DiscoverOfficialProductSourcesAsync(
        ConfirmedProductRequest request,
        string productName,
        string credential,
        CancellationToken cancellationToken)
    {
        if (!GeminiSearchEnabled)
        {
            AddOfficialUrlDiagnostic("GeminiSearchEnabled=false; không gọi Gemini Google Search Grounding, chỉ dùng Trusted Website Product Index.");
            return [];
        }

        try
        {
            return await RunTrustedRegistrySearchStagesAsync(request, productName, credential, cancellationToken);
        }
        catch (GeminiQuotaExceededException)
        {
            AddOfficialUrlDiagnostic("Cancelling all remaining source discovery tasks");
            AddOfficialUrlDiagnostic("Skipped department-store stage because workflow was cancelled");
            throw;
        }
    }

    private async Task<IReadOnlyList<GroundedSource>> RunTrustedRegistrySearchStagesAsync(
        ConfirmedProductRequest request,
        string productName,
        string credential,
        CancellationToken cancellationToken)
    {
        var stages = BuildTrustedSearchStages(request).Take(2).ToArray();
        foreach (var stage in stages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stage.Domains.Count == 0)
            {
                continue;
            }

            var query = BuildGroundedProductSearchQueries(request, productName, stage.Domains)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(query))
            {
                continue;
            }

            AddOfficialUrlDiagnostic($"{stage.LogName}: chạy 1 query theo registry.");
            var result = await SearchGroundedProductSourcesAsync(request, productName, query, credential, stage.Domains, cancellationToken);
            var candidates = FilterProductPageSources(result, request, productName)
                .Take(10)
                .ToArray();
            var sources = await ValidateOfficialProductSourcesAsync(candidates, request, productName, cancellationToken);
            if (sources.Count > 0)
            {
                AddOfficialUrlDiagnostic($"{stage.LogName}: URL verified.");
                return sources;
            }

            AddOfficialUrlDiagnostic($"{stage.LogName}: chưa có URL verified, chuyển stage kế tiếp.");
        }

        return [];
    }

    private static IReadOnlyList<TrustedSearchStage> BuildTrustedSearchStages(ConfirmedProductRequest request)
    {
        var brandProfile = ResolveTrustedBrandProfile(request.Brand);
        var officialDomains = new List<string>();
        if (brandProfile is not null)
        {
            officialDomains.AddRange(brandProfile.OfficialDomains);
            officialDomains.AddRange(brandProfile.RegionalDomains);
        }

        var retailerDomains = BeautySourceRegistry.TrustedRetailers.AllDomains;
        return new[]
        {
            new TrustedSearchStage("official + regional", officialDomains.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()),
            new TrustedSearchStage("trusted retailer", retailerDomains)
        }
            .Where(stage => stage.Domains.Count > 0)
            .ToArray();
    }

    private static TrustedBeautyBrand? ResolveTrustedBrandProfile(string brand)
    {
        var normalizedBrand = RemoveVietnameseDiacritics(brand ?? "").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedBrand))
        {
            return null;
        }

        foreach (var profile in BeautySourceRegistry.Brands)
        {
            var names = new[] { profile.Brand }.Concat(profile.Aliases ?? []);
            if (names.Any(name =>
            {
                var normalizedName = RemoveVietnameseDiacritics(name).ToLowerInvariant();
                return normalizedBrand.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                    normalizedBrand.Contains(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                    normalizedName.Contains(normalizedBrand, StringComparison.OrdinalIgnoreCase);
            }))
            {
                return profile;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> BuildGroundedProductSearchQueries(ConfirmedProductRequest request, string productName, IReadOnlyList<string>? domains = null)
    {
        var visibleText = CleanInline(request.SearchQuery);
        var exactName = CleanInline(productName);
        if (IsGenericSearchProductName(exactName, visibleText))
        {
            exactName = "";
        }

        var primaryParts = new[]
        {
            request.Brand,
            exactName,
            request.ProductLine,
            request.Variant,
            request.Shade,
            request.Size,
            request.Category
        }
            .Select(CleanInline)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var primary = CleanInline(string.Join(" ", primaryParts));
        var secondary = CleanInline(visibleText);

        var cleaned = new[]
        {
            QuoteSearchPhrase(primary),
            QuoteSearchPhrase(secondary)
        }
            .Where(value => value.Replace("\"", "", StringComparison.Ordinal).Trim().Length >= 6)
            .Select(value => Regex.Replace(value, @"\s+", " ").Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        if (domains is null || domains.Count == 0)
        {
            return cleaned;
        }

        var domainHint = string.Join(" OR ", domains
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .Select(domain => $"site:{domain}"));
        return cleaned
            .Select(query => string.IsNullOrWhiteSpace(domainHint) ? query : $"{domainHint} {query}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
    }

    private static string QuoteSearchPhrase(string value)
    {
        var clean = CleanInline(value).Replace("\"", "", StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(clean) ? "" : $"\"{clean}\"";
    }

    private static bool IsGenericSearchProductName(string productName, string visibleText)
    {
        var normalized = RemoveVietnameseDiacritics(productName ?? "").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return normalized.Contains(" for face", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(" face", StringComparison.OrdinalIgnoreCase) ||
            normalized is "face" or "for face" ||
            (normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3 &&
                !RemoveVietnameseDiacritics(visibleText ?? "").Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<GroundedSource>> SearchGroundedProductSourcesAsync(
        ConfirmedProductRequest request,
        string productName,
        string query,
        string credential,
        IReadOnlyList<string> officialDomains,
        CancellationToken cancellationToken)
    {
        var domainHint = officialDomains.Count == 0
            ? "Ưu tiên website chính thức, retailer uy tín hoặc catalogue/PDF chính thức."
            : $"Tìm trong nhóm domain này trong cùng một request: {string.Join(", ", officialDomains.Take(24))}.";
        var prompt = $$"""
        Dùng Google Search Grounding để tìm nguồn tham khảo đáng tin cậy cho sản phẩm làm đẹp.

        Query: {{query}}
        Sản phẩm: {{productName}}
        Brand: {{request.Brand}}
        Product line: {{request.ProductLine}}
        Variant/shade/size: {{request.Variant}} {{request.Shade}} {{request.Size}}
        Category/itemForm: {{request.Category}} / {{request.ItemForm}}
        {{domainHint}}

        Ưu tiên official domains trước, sau đó retailer uy tín như Sephora, Ulta, Selfridges, Harrods, Space NK, Cult Beauty, Notino, BeautyBay nếu nhóm domain là retailer.
        Chỉ trả URL product page/canonical, retailer uy tín hoặc catalogue/PDF chính thức. Không trả homepage/category/search/blog/social/marketplace.
        Trả tối đa 10 dòng productPageUrl, không markdown:
        productPageUrl: https://...
        """;

        var body = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = prompt } }
                }
            },
            tools = new object[]
            {
                new { google_search = new { } }
            },
            generationConfig = new
            {
                temperature = 0.0,
                topP = 0.35,
                maxOutputTokens = 700
            }
        };

        AddOfficialUrlDiagnostic($"Search query: {ShortDiagnostic(query)}");
        var queryStopwatch = Stopwatch.StartNew();
        var response = await SendGeminiAsync(
            body,
            credential,
            retryJson: false,
            cancellationToken,
            allowTransientRetries: true,
            requestTimeout: TimeSpan.FromSeconds(35));
        if (!response.Success)
        {
            AddOfficialUrlDiagnostic($"query completed in {queryStopwatch.ElapsedMilliseconds}ms: {ShortDiagnostic(query)}");
            AddOfficialUrlDiagnostic($"Truy vấn search lỗi: {ShortDiagnostic(query)} | {response.Message}");
            return [];
        }
        AddOfficialUrlDiagnostic($"query completed in {queryStopwatch.ElapsedMilliseconds}ms: {ShortDiagnostic(query)}");

        var groundingSources = ExtractGroundingSources(response.RawJson).ToArray();
        AddOfficialUrlDiagnostic($"grounding URLs received: {groundingSources.Length} | query={ShortDiagnostic(query)}");
        foreach (var groundingSource in groundingSources.Take(8))
        {
            AddOfficialUrlDiagnostic($"grounding URL: {groundingSource.Url}");
        }

        var sources = ExtractSourcesFromProductPageUrlLines(response.Text)
            .Concat(groundingSources)
            .Concat(ExtractSourcesFromResearchText(response.Text))
            .ToArray();
        AddOfficialUrlDiagnostic($"Truy vấn search có {sources.Length} URL thô: {ShortDiagnostic(query)}");
        return sources;
    }

    private async Task<IReadOnlyList<GroundedSource>> DiscoverOfficialProductSourcesFallbackAsync(
        ConfirmedProductRequest request,
        string productName,
        string credential,
        string[] officialDomains,
        CancellationToken cancellationToken)
    {
        var domainText = officialDomains.Length == 0
            ? "website chính hãng của thương hiệu"
            : string.Join(", ", officialDomains);
        var prompt = $$"""
        Lượt tìm bổ sung: hãy tìm lại URL trang chi tiết sản phẩm. Ưu tiên website chính hãng, nhưng nếu website hãng lỗi/chặn/không có trang sống thì dùng retailer uy tín có trang sản phẩm khớp thật.

        Sản phẩm: {{productName}}
        Thương hiệu: {{request.Brand}}
        Phiên bản/dòng: {{request.Variant}}
        Finish/nồng độ/kết cấu: {{request.Finish}}
        Dung tích/trọng lượng: {{request.Size}}
        Domain chính hãng có thể dùng: {{domainText}}
        Retailer uy tín có thể dùng nếu web hãng không ổn: Sephora, Ulta, Nordstrom, Macy's, Bloomingdale's, Harrods, Selfridges, Lookfantastic, Cult Beauty, Space NK, Boots.

        Quy tắc bắt buộc:
        - Bắt buộc tìm kiếm trên Google trước khi trả lời.
        - Ưu tiên URL từ website chính hãng của thương hiệu.
        - Nếu website hãng bị chặn/lỗi/không có trang sản phẩm sống, được trả URL trang chi tiết sản phẩm từ retailer uy tín trong danh sách trên.
        - Không lấy Amazon, marketplace chung, blog, tạp chí, trang review hoặc reseller không kiểm soát.
        - URL phải là trang chi tiết sản phẩm cụ thể. Không trả trang chủ, trang danh mục, trang search hoặc collection.
        - Không trả URL route nội bộ như /on/demandware.store/.../Product-Show?pid=... hoặc URL có Product-Show?pid=. Phải là URL public/canonical có slug sản phẩm.
        - Nếu domain US không có, thử domain global hoặc region khác nhưng vẫn phải là official brand website.
        - Nếu không xác minh chắc đúng sản phẩm/phiên bản, để productPageUrl rỗng.

        Trả đúng định dạng, tối đa 3 URL ứng viên, không markdown:
        productPageUrl: https://...
        productPageUrl: https://...
        reason: lý do ngắn vì sao URL khớp hoặc vì sao không có URL chắc chắn
        """;

        var body = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = prompt } }
                }
            },
            tools = new object[]
            {
                new { google_search = new { } }
            },
            generationConfig = new
            {
                temperature = 0.0,
                topP = 0.35,
                maxOutputTokens = 1200
            }
        };

        var response = await SendGeminiAsync(
            body,
            credential,
            retryJson: false,
            cancellationToken,
            allowTransientRetries: true,
            requestTimeout: TimeSpan.FromSeconds(35));

        if (!response.Success)
        {
            logger.LogWarning(
                "Fallback official product URL discovery failed. Product={Product}; Status={Status}; Message={Message}",
                productName,
                response.StatusCode,
                response.Message);
            AddOfficialUrlDiagnostic($"Tìm kiếm dự phòng thất bại: HTTP {response.StatusCode} - {response.Message}");
            return [];
        }

        var candidateSources = FilterProductPageSources(
            ExtractSourcesFromProductPageUrlLines(response.Text)
                .Concat(ExtractGroundingSources(response.RawJson))
                .Concat(ExtractSourcesFromResearchText(response.Text)),
            request,
            productName);
        return await ValidateOfficialProductSourcesAsync(candidateSources, request, productName, cancellationToken);
    }

    private async Task<IReadOnlyList<GroundedSource>> ValidateOfficialProductSourcesAsync(
        IReadOnlyList<GroundedSource> sources,
        ConfirmedProductRequest request,
        string productName,
        CancellationToken cancellationToken)
    {
        if (sources.Count == 0)
        {
            return [];
        }

        var scoredCandidates = sources
            .Select(source =>
            {
                var score = IsImageFirstBlankRequest(request)
                    ? new ProductUrlScore(70, true, ["visual-grounding"], false)
                    : productMatchScorer.Score(request, source.Url, source.Title, "");
                var registrySourceType = SourceTypeFromRegistry(source.Url, request.Brand);
                if (registrySourceType != "unknown" && score.Score < 65 && !score.HasHardConflict)
                {
                    score = score with
                    {
                        Score = Math.Max(score.Score, 65),
                        Accepted = true,
                        Reasons = score.Reasons.Concat([registrySourceType]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    };
                }
                return new
                {
                    Source = EnrichSource(source, request, score),
                    Score = score
                };
            })
            .Where(item => item.Score.Score >= 45 && !item.Score.HasHardConflict)
            .OrderBy(item => SourceTypeRank(item.Source.SourceType))
            .ThenByDescending(item => item.Score.Score)
            .Take(8)
            .ToArray();
        AddOfficialUrlDiagnostic($"candidates scored: {scoredCandidates.Length}");
        foreach (var candidate in scoredCandidates)
        {
            AddOfficialUrlDiagnostic($"candidate score: {candidate.Score.Score} | {candidate.Source.Url} | {string.Join(",", candidate.Score.Reasons)}");
        }

        var verified = new List<GroundedSource>();
        foreach (var candidate in scoredCandidates.Take(3))
        {
            var source = candidate.Source;
            var metadataScore = candidate.Score;
            AddOfficialUrlDiagnostic($"Kiểm tra URL ứng viên: {source.Url}");
            logger.LogInformation(
                "Validating official product URL candidate. Product={Product}; Brand={Brand}; CandidateTitle={Title}; CandidateUrl={Url}",
                productName,
                request.Brand,
                source.Title,
                source.Url);

            var readResult = await ReadOfficialProductPageAsync(source.Url, request, productName, cancellationToken);
            if (readResult.Success && readResult.Source is not null)
            {
                AddOfficialUrlDiagnostic($"URL Context read: {readResult.Source.Url}");
                AddOfficialUrlDiagnostic($"URL Context verified: {readResult.Source.Url}");
                AddOfficialUrlDiagnostic($"URL verified: {readResult.Source.Url}");
                var score = IsImageFirstBlankRequest(request)
                    ? new ProductUrlScore(Math.Max(80, metadataScore.Score), true, ["visual-grounding", "url-context-read"], false)
                    : productMatchScorer.Score(request, readResult.Source.Url, readResult.Source.Title, readResult.Source.Content);
                if (!score.Accepted)
                {
                    AddOfficialUrlDiagnostic($"Loại URL sau khi chấm điểm: {readResult.Source.Url} | score={score.Score} | {string.Join(",", score.Reasons)}");
                    logger.LogWarning(
                        "Rejected readable product URL by match score. Product={Product}; Url={Url}; Score={Score}; Reasons={Reasons}; HardConflict={HardConflict}",
                        productName,
                        readResult.Source.Url,
                        score.Score,
                        string.Join(",", score.Reasons),
                        score.HasHardConflict);
                    continue;
                }

                AddOfficialUrlDiagnostic($"Nhận URL sau khi đọc được trang: {readResult.Source.Url}");
                AddOfficialUrlDiagnostic($"trusted URL selected: {readResult.Source.Url}");
                logger.LogInformation(
                    "Accepted official product URL after reading page. Product={Product}; Url={Url}",
                    productName,
                    readResult.Source.Url);
                verified.Add(new GroundedSource(
                    readResult.Source.Website,
                    readResult.Source.Title,
                    readResult.Source.Url,
                    productMatchScorer.SourceType(readResult.Source.Url, request.Brand),
                    score.Score,
                    score.Reasons));
                continue;
            }

            var canAcceptByUrl = readResult.MayAcceptByUrlMatch && CanAcceptByStrictUrlMatch(source.Url);
            if (canAcceptByUrl && IsBlockedGenericUnreadableLuxuryCandidate(source, request, productName))
            {
                AddOfficialUrlDiagnostic($"Loại URL generic/chưa đọc được: {source.Url} | {readResult.Message}");
                logger.LogWarning(
                    "Rejected generic unreadable luxury URL candidate before strict fallback. Product={Product}; Brand={Brand}; Url={Url}; CandidateTitle={Title}; ReadReason={Reason}",
                    productName,
                    request.Brand,
                    source.Url,
                    source.Title,
                    readResult.Message);
                canAcceptByUrl = false;
            }
            var strictUrlMatch = canAcceptByUrl && IsStrictOfficialProductUrlMatch(source, request, productName);
            if ((strictUrlMatch || metadataScore.Score >= 80) && metadataScore.Score >= 65 && !metadataScore.HasHardConflict)
            {
                AddOfficialUrlDiagnostic($"Nhận URL bằng slug/title khớp chặt dù site chặn đọc: {source.Url}");
                AddOfficialUrlDiagnostic($"trusted URL selected: {source.Url}");
                logger.LogWarning(
                    "Accepted official product URL by strict URL match after page read failed. Product={Product}; Url={Url}; Reason={Reason}",
                    productName,
                    source.Url,
                    readResult.Message);
                verified.Add(source with
                {
                    Confidence = Math.Max(source.Confidence, metadataScore.Score),
                    MatchedFields = metadataScore.Reasons
                });
                continue;
            }

            var hardFallbackMatch = canAcceptByUrl && IsHardOfficialProductUrlFallback(source, request, productName);
            if (hardFallbackMatch)
            {
                AddOfficialUrlDiagnostic($"Nhận URL bằng hard fallback vì khớp bộ nhận diện mạnh: {source.Url}");
                AddOfficialUrlDiagnostic($"trusted URL selected: {source.Url}");
                logger.LogWarning(
                    "Accepted official product URL by hard fallback after official site blocked read. Product={Product}; Url={Url}; Reason={Reason}",
                    productName,
                    source.Url,
                    readResult.Message);
                verified.Add(source);
                continue;
            }

            AddOfficialUrlDiagnostic($"Loại URL: {source.Url} | {readResult.Message} | strict={strictUrlMatch} hard={hardFallbackMatch}");
            logger.LogWarning(
                "Rejected unverified official product URL. Product={Product}; Url={Url}; ReadReason={Reason}; CanAcceptByUrl={CanAcceptByUrl}; StrictUrlMatch={StrictUrlMatch}; HardFallbackMatch={HardFallbackMatch}",
                productName,
                source.Url,
                readResult.Message,
                canAcceptByUrl,
                strictUrlMatch,
                hardFallbackMatch);
        }

        return verified
            .GroupBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static bool IsImageFirstBlankRequest(ConfirmedProductRequest request) =>
        string.IsNullOrWhiteSpace(request.Brand) &&
        string.IsNullOrWhiteSpace(request.ProductName) &&
        string.IsNullOrWhiteSpace(request.ProductLine);

    private GroundedSource EnrichSource(GroundedSource source, ConfirmedProductRequest request, ProductUrlScore score)
    {
        var sourceType = SourceTypeFromRegistry(source.Url, request.Brand);
        if (sourceType == "unknown")
        {
            sourceType = productMatchScorer.SourceType(source.Url, request.Brand);
        }
        if (sourceType == "unknown" &&
            IsImageFirstBlankRequest(request) &&
            Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) &&
            IsRegisteredOfficialBrandHost(uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()))
        {
            sourceType = uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? "official-document"
                : "official";
        }

        return source with
        {
            SourceType = string.IsNullOrWhiteSpace(source.SourceType) ? sourceType : source.SourceType,
            Confidence = Math.Max(source.Confidence, score.Score),
            MatchedFields = source.MatchedFields ?? score.Reasons
        };
    }

    private static string SourceTypeFromRegistry(string url, string brand)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "unknown";
        }

        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var profile = ResolveTrustedBrandProfile(brand);
        if (profile is not null)
        {
            if (DomainMatchesAny(host, profile.OfficialDomains))
            {
                return uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "official-document" : "official";
            }

            if (DomainMatchesAny(host, profile.RegionalDomains))
            {
                return uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "official-document" : "official-regional";
            }
        }

        return DomainMatchesAny(host, BeautySourceRegistry.TrustedRetailers.AllDomains)
            ? "trusted-retailer"
            : "unknown";
    }

    private static bool DomainMatchesAny(string host, IEnumerable<string> domains)
    {
        var normalizedHost = host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        return domains.Any(domain =>
        {
            var normalizedDomain = domain.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
            return normalizedHost.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase) ||
                normalizedHost.EndsWith("." + normalizedDomain, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool IsBlockedGenericUnreadableLuxuryCandidate(
        GroundedSource source,
        ConfirmedProductRequest request,
        string productName)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri))
        {
            return true;
        }

        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var isStrictLuxuryHost = host.Equals("dior.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".dior.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("chanel.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".chanel.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("gucci.com", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".gucci.com", StringComparison.OrdinalIgnoreCase);
        if (!isStrictLuxuryHost)
        {
            return false;
        }

        var normalizedTitle = RemoveVietnameseDiacritics(source.Title ?? "").ToLowerInvariant();
        var genericTitles = new[]
        {
            "url trang san pham",
            "url san pham",
            "official product url",
            "product page url",
            "gemini de xuat"
        };
        if (!genericTitles.Any(normalizedTitle.Contains))
        {
            return false;
        }

        var haystack = RemoveVietnameseDiacritics($"{host} {uri.AbsolutePath} {source.Title}").ToLowerInvariant();
        if (!HasConflictingCategoryInSource(haystack, request.Category, productName) &&
            (CoreProductNameTokensMatch(haystack, request, productName) ||
                IsDiorGenericLipGlossMatch(haystack, request, productName)))
        {
            return false;
        }

        var brandTokens = RemoveVietnameseDiacritics(request.Brand)
            .ToLowerInvariant()
            .Split([' ', '-', '_', '.', '\'', '’'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .ToArray();
        return brandTokens.Length == 0 || brandTokens.All(token => !normalizedTitle.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStrictOfficialProductUrlMatch(
        GroundedSource source,
        ConfirmedProductRequest request,
        string productName)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (IsNonPublicCommerceRoute(uri))
        {
            return false;
        }

        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        if (!IsAcceptableProductHost(host, request.Brand) &&
            !(IsImageFirstBlankRequest(request) && IsKnownOfficialOrTrustedProductHost(host)))
        {
            return false;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path) || path.Length < 10)
        {
            return false;
        }

        var lastSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        if (lastSegment is "home" or "homepage" or "shop" or "products" or "product" or "collections" or "fragrance" or "perfume" or "makeup" or "skincare" or "eyes" or "eye" or "eyeshadow" or "eyeshadows" or "lips" or "lip" or "face" or "foundation" or "foundations")
        {
            return false;
        }

        var haystack = RemoveVietnameseDiacritics($"{host} {path} {source.Title}").ToLowerInvariant();
        var concentration = DetectFragranceConcentration($"{productName} {request.Variant} {request.Finish}");
        if (HasConflictingCategoryInSource(haystack, request.Category, productName))
        {
            return false;
        }

        if (!FragranceConcentrationMatches(haystack, concentration))
        {
            return false;
        }

        if (!ProductSizeMatchesSource(haystack, request.Size))
        {
            return false;
        }

        if (IsDiorGenericLipGlossMatch(haystack, request, productName))
        {
            return true;
        }

        if (CoreProductNameTokensMatch(haystack, request, productName))
        {
            return true;
        }

        var distinctiveTokens = BuildDistinctiveProductSourceTokens(request, productName);
        if (!DistinctiveProductTokensMatch(haystack, distinctiveTokens))
        {
            return false;
        }

        var tokens = BuildProductSourceTokens(request, productName);
        var matchCount = tokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        return matchCount >= Math.Min(2, tokens.Count);
    }

    private static bool IsHardOfficialProductUrlFallback(
        GroundedSource source,
        ConfirmedProductRequest request,
        string productName)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (IsNonPublicCommerceRoute(uri))
        {
            return false;
        }

        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        if (!IsAcceptableProductHost(host, request.Brand) &&
            !(IsImageFirstBlankRequest(request) && IsKnownOfficialOrTrustedProductHost(host)))
        {
            return false;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path) || path.Length < 10)
        {
            return false;
        }

        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lastSegment = pathSegments.LastOrDefault() ?? "";
        if (lastSegment is "home" or "homepage" or "shop" or "products" or "product" or "collections" or "search" or "fragrance" or "perfume" or "makeup" or "skincare" or "eyes" or "eye" or "eyeshadow" or "eyeshadows" or "lips" or "lip" or "face")
        {
            return false;
        }

        var haystack = RemoveVietnameseDiacritics($"{host} {path} {source.Title}").ToLowerInvariant();
        if (HasConflictingCategoryInSource(haystack, request.Category, productName))
        {
            return false;
        }

        var concentration = DetectFragranceConcentration($"{productName} {request.Variant} {request.Finish}");
        if (!FragranceConcentrationMatches(haystack, concentration) ||
            !ProductSizeMatchesSource(haystack, request.Size))
        {
            return false;
        }

        if (IsDiorGenericLipGlossMatch(haystack, request, productName) ||
            CoreProductNameTokensMatch(haystack, request, productName))
        {
            return true;
        }

        var tokens = BuildProductSourceTokens(request, productName)
            .Where(token => token.Length >= 4)
            .Where(token => token is not "dior" and not "christian" and not "ysl" and not "gucci" and not "chanel")
            .ToArray();
        if (tokens.Length == 0)
        {
            return false;
        }

        var matchCount = tokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        return matchCount >= Math.Min(2, tokens.Length);
    }

    private static bool IsDiorGenericLipGlossMatch(string haystack, ConfirmedProductRequest request, string productName)
    {
        var normalized = RemoveVietnameseDiacritics($"{request.Brand} {request.Category} {request.ProductName} {productName} {request.Variant}").ToLowerInvariant();
        if (!normalized.Contains("dior", StringComparison.OrdinalIgnoreCase) ||
            !normalized.Contains("lip", StringComparison.OrdinalIgnoreCase) ||
            !normalized.Contains("gloss", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("glow", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("oil", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return haystack.Contains("dior", StringComparison.OrdinalIgnoreCase) &&
            (haystack.Contains("lip-maximizer", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("lip maximizer", StringComparison.OrdinalIgnoreCase));
    }

    private static bool CoreProductNameTokensMatch(string haystack, ConfirmedProductRequest request, string productName)
    {
        var brandTokens = BuildProductSourceTokens(
                request with { ProductName = request.Brand, Variant = "", Shade = "", Size = "", Finish = "" },
                request.Brand)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var genericTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "product", "official", "beauty", "makeup", "skincare", "fragrance", "perfume",
            "lip", "lips", "balm", "gloss", "oil", "serum", "cream", "color", "colour",
            "reviver", "reviving", "hydrating", "custom", "nourishing", "natural", "son",
            "duong", "mau", "co", "christian", "paris"
        };
        var tokens = RemoveVietnameseDiacritics(productName)
            .ToLowerInvariant()
            .Split([' ', '-', '_', '/', '.', '\'', '"', '’'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .Where(token => !brandTokens.Contains(token))
            .Where(token => !genericTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        if (tokens.Length == 0)
        {
            return false;
        }

        var matchCount = tokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (tokens.Length <= 2)
        {
            return matchCount == tokens.Length;
        }

        return matchCount >= Math.Max(2, tokens.Length - 1);
    }

    private async Task<AiServiceResult<SaleContentResult>> WriteSaleContentWithoutSearchAsync(
        ConfirmedProductRequest request,
        string productName,
        string credential,
        string warningMessage,
        IReadOnlyList<GroundedSource>? fallbackSources,
        CancellationToken cancellationToken)
    {
        var verifiedFacts = """
        Không dùng Google Search trong lượt này.
        Chỉ dùng thông tin người dùng đã xác nhận, chữ nhìn thấy trên bao bì/OCR, tên sản phẩm, thương hiệu, dòng/phiên bản, màu/kết cấu, dung tích/trọng lượng và dữ liệu bán hàng đã nhập.
        Tuyệt đối không bịa thành phần, note hương, công dụng, chứng nhận, độ bám tỏa, xuất xứ, giải thưởng hoặc claim kỹ thuật nếu dữ liệu đầu vào không có.
        Nếu thiếu chi tiết chuyên sâu, hãy bán bằng gu phù hợp, hoàn cảnh sử dụng, cảm giác sở hữu và lý do hỏi tư vấn, nhưng không tự tạo thông tin mới.
        """;
        var salePrompt = BuildSalePrompt(request, productName, BuildSearchQuery(request, productName), BuildAntiRepeatInstruction(request), verifiedFacts);
        var prompt = $$"""
        {{salePrompt}}
        """;

        var body = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.52,
                topP = 0.82,
                maxOutputTokens = 3600,
                response_mime_type = "application/json",
                response_schema = SaleCreativeSchema()
            }
        };

        var gemini = await SendGeminiAsync(body, credential, retryJson: false, cancellationToken, allowTransientRetries: true);
        if (!gemini.Success)
        {
            return AiServiceResult<SaleContentResult>.Fail(gemini.StatusCode, gemini.Message);
        }

        var parseResult = TryParseJson<SaleCreativeResult>(gemini.Text, out var creativeContent);
        if (!parseResult || creativeContent is null)
        {
            return AiServiceResult<SaleContentResult>.Fail(502, "Gemini đã phản hồi nhưng JSON bài sale không hợp lệ. Vui lòng thử lại.");
        }

        if ((creativeContent.Highlights ?? []).Count != 4)
        {
            return AiServiceResult<SaleContentResult>.Fail(502, "Gemini chưa trả đúng 4 lợi ích bán hàng. Vui lòng thử lại.");
        }

        var saleContent = NormalizeSaleContent(MapCreativeToSaleContent(creativeContent, request), request);
        if (IsLowQualitySaleContent(saleContent))
        {
            var qualityRetry = await TryRegenerateSaleContentForQualityAsync(body, credential, request, cancellationToken);
            if (qualityRetry.Content is null)
            {
                return AiServiceResult<SaleContentResult>.Fail(422, "Bản nháp chưa đạt chất lượng bán hàng. Hệ thống đã chặn các câu quá chung chung, vui lòng bấm Viết lại để tạo bản gần khách hơn.");
            }

            saleContent = qualityRetry.Content;
        }

        saleContent = saleContent with
        {
            ResearchSuccessful = true,
            WarningMessage = warningMessage,
            VerifiedDetails = saleContent.VerifiedDetails with
            {
                Sources = fallbackSources ?? []
            }
        };

        await SaveSaleDraftAsync($"No-search fallback: {productName}", request.ProductName, ComposeMainArticle(saleContent), cancellationToken);
        RememberSaleContent(saleContent);
        return AiServiceResult<SaleContentResult>.Ok(saleContent);
    }

    private async Task<AiServiceResult<SaleContentResult>> WriteSaleContentFromOfficialUrlAsync(
        ConfirmedProductRequest request,
        string productName,
        string credential,
        CancellationToken cancellationToken)
    {
        var sourceResult = await ReadOfficialProductPageAsync(request.OfficialProductUrl, request, productName, cancellationToken);
        if (!sourceResult.Success || sourceResult.Source is null)
        {
            logger.LogWarning(
                "Official product URL could not be verified for sale content. Product={Product}; Url={Url}; Reason={Reason}",
                productName,
                request.OfficialProductUrl,
                sourceResult.Message);

            if (Uri.TryCreate(request.OfficialProductUrl, UriKind.Absolute, out var fallbackUri) &&
                IsAcceptableProductHost(fallbackUri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase), request.Brand))
            {
                var fallbackSource = new GroundedSource(
                    fallbackUri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase),
                    productName,
                    request.OfficialProductUrl,
                    productMatchScorer.SourceType(request.OfficialProductUrl, request.Brand),
                    65,
                    ["url", "trusted-host"]);
                return await WriteSaleContentWithoutSearchAsync(
                    request,
                    productName,
                    credential,
                    $"Nguồn tham khảo đã được xác minh trước đó nhưng website đang chặn đọc tự động. Bài không dùng claim ngoài nguồn đọc được. Chi tiết: {sourceResult.Message}",
                    [fallbackSource],
                    cancellationToken);
            }

            return AiServiceResult<SaleContentResult>.Fail(
                400,
                $"URL chưa được backend xác minh đúng sản phẩm nên chưa được viết bài sale. Chi tiết: {sourceResult.Message}");
        }

        var salePrompt = BuildSalePrompt(request, productName, BuildSearchQuery(request, productName), BuildAntiRepeatInstruction(request), sourceResult.Source.Content);
        var prompt = $$"""
        {{salePrompt}}
        """;

        var body = new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.56,
                topP = 0.86,
                maxOutputTokens = 3800,
                response_mime_type = "application/json",
                response_schema = SaleCreativeSchema()
            }
        };

        var gemini = await SendGeminiAsync(body, credential, retryJson: false, cancellationToken, allowTransientRetries: true);
        if (!gemini.Success)
        {
            return AiServiceResult<SaleContentResult>.Fail(gemini.StatusCode, gemini.Message);
        }

        var parseResult = TryParseJson<SaleCreativeResult>(gemini.Text, out var creativeContent);
        if (!parseResult || creativeContent is null)
        {
            return AiServiceResult<SaleContentResult>.Fail(502, "Gemini đã phản hồi nhưng JSON bài sale không hợp lệ. Vui lòng thử lại.");
        }

        if ((creativeContent.Highlights ?? []).Count != 4)
        {
            return AiServiceResult<SaleContentResult>.Fail(502, "Gemini chưa trả đúng 4 lợi ích bán hàng. Vui lòng thử lại.");
        }

        var saleContent = MapCreativeToSaleContent(creativeContent, request);
        saleContent = NormalizeSaleContent(saleContent, request);
        if (IsLowQualitySaleContent(saleContent))
        {
            var qualityRetry = await TryRegenerateSaleContentForQualityAsync(body, credential, request, cancellationToken);
            if (qualityRetry.Content is null)
            {
                return AiServiceResult<SaleContentResult>.Fail(422, "Bản nháp chưa đạt chất lượng bán hàng. Hệ thống đã chặn các câu quá chung chung, vui lòng bấm Viết lại để tạo bản gần khách hơn.");
            }

            saleContent = qualityRetry.Content;
        }

        saleContent = saleContent with
        {
            VerifiedDetails = saleContent.VerifiedDetails with
            {
                Sources = [new GroundedSource(sourceResult.Source.Website, sourceResult.Source.Title, sourceResult.Source.Url)]
            }
        };

        if (!saleContent.ResearchSuccessful)
        {
            return AiServiceResult<SaleContentResult>.Ok(saleContent with
            {
                Opening = "",
                ContentBlocks = [],
                ShortCaption = "",
                Headline = "",
                CallToAction = new CallToActionContent("", ""),
                Hashtags = [],
                WarningMessage = "Chưa tìm được đủ thông tin đáng tin cậy về sản phẩm này. Vui lòng nhập thêm tên hoặc đường dẫn sản phẩm."
            });
        }

        await SaveSaleDraftAsync($"Official URL fallback: {sourceResult.Source.Url}", request.ProductName, ComposeMainArticle(saleContent), cancellationToken);
        RememberSaleContent(saleContent);
        return AiServiceResult<SaleContentResult>.Ok(saleContent);
    }

    private async Task<(SaleContentResult? Content, string RawJson)> TryRegenerateSaleContentForQualityAsync(
        object originalBody,
        string credential,
        ConfirmedProductRequest request,
        CancellationToken cancellationToken)
    {
        var lastRawJson = "";
        for (var attempt = 1; attempt <= MaxQualityRewriteAttempts; attempt++)
        {
            var retryBody = AddQualityRewriteInstruction(originalBody, attempt);
            var retryGemini = await SendGeminiAsync(retryBody, credential, retryJson: false, cancellationToken, allowTransientRetries: true);
            if (!retryGemini.Success)
            {
                continue;
            }

            lastRawJson = retryGemini.RawJson;
            var parseResult = TryParseJson<SaleCreativeResult>(retryGemini.Text, out var creativeContent);
            if (!parseResult || creativeContent is null || (creativeContent.Highlights ?? []).Count != 4)
            {
                continue;
            }

            var saleContent = NormalizeSaleContent(MapCreativeToSaleContent(creativeContent, request), request);
            if (!IsLowQualitySaleContent(saleContent))
            {
                return (saleContent, retryGemini.RawJson);
            }
        }

        return (null, lastRawJson);
    }

    private async Task SaveSaleDraftAsync(string prompt, string sourceName, string content, CancellationToken cancellationToken)
    {
        db.AiDrafts.Add(new AiDraft
        {
            Type = "ai-sale-content",
            Prompt = $"Model: {GetGeminiModel()}\n{prompt}",
            SourceImagePrivatePath = sourceName,
            Content = content,
            Status = DraftStatus.Draft,
            CreatedByUserId = Guid.Empty
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<OfficialPageReadResult> ReadOfficialProductPageAsync(
        string url,
        ConfirmedProductRequest productRequest,
        string productName,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return OfficialPageReadResult.Fail("Đường dẫn sản phẩm chính thức không hợp lệ.");
        }

        if (IsUnavailableDiorLegacyProductRoute(uri))
        {
            return OfficialPageReadResult.Fail("URL Dior dạng cũ đã bị loại vì thường mở ra trang không khả dụng, không phải route sản phẩm hiện tại.");
        }

        if (IsNonPublicCommerceRoute(uri))
        {
            return OfficialPageReadResult.Fail("URL này là route nội bộ/không public của website chính hãng, không phải trang chi tiết sản phẩm ổn định.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("HoanMakeupAdmin/1.0");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var mayAcceptByUrlMatch = response.StatusCode is HttpStatusCode.Forbidden or
                    HttpStatusCode.Unauthorized or
                    HttpStatusCode.TooManyRequests or
                    HttpStatusCode.ServiceUnavailable or
                    HttpStatusCode.GatewayTimeout;
                AddOfficialUrlDiagnostic($"Site hãng trả HTTP {(int)response.StatusCode} khi đọc: {uri}");
                return OfficialPageReadResult.Fail(
                    $"URL chính hãng trả HTTP {(int)response.StatusCode}; sẽ chỉ nhận nếu URL/title khớp rất chặt với sản phẩm.",
                    mayAcceptByUrlMatch);
            }

            var html = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            var title = ExtractHtmlTitle(html, uri.Host);
            if (LooksLikeNotFoundPage(html, title))
            {
                AddOfficialUrlDiagnostic($"Site hãng báo không khả dụng/404: {uri}");
                return OfficialPageReadResult.Fail(
                    "URL chính hãng đang là trang Page Not Found/404; không được dùng làm nguồn sản phẩm.",
                    mayAcceptByUrlMatch: false);
            }

            var content = ExtractOfficialSourceContent(html, uri, title, productRequest, productName);
            if (content.Length < 120)
            {
                AddOfficialUrlDiagnostic($"Trang đọc được quá ít nội dung để xác minh: {uri}");
                return OfficialPageReadResult.Fail("Trang sản phẩm quá ít chữ hoặc bị chặn nội dung động; sẽ chỉ nhận nếu URL/title khớp rất chặt với sản phẩm.");
            }

            if (!OfficialSourceMatchesProduct(uri, title, content, productRequest, productName))
            {
                AddOfficialUrlDiagnostic($"Trang đọc được nhưng không khớp sản phẩm/phiên bản: {uri}");
                return OfficialPageReadResult.Fail(
                    "URL này chưa khớp đúng sản phẩm hoặc phiên bản/nồng độ đã xác nhận. Vui lòng dùng link trang sản phẩm chính xác.",
                    mayAcceptByUrlMatch: false);
            }

            return OfficialPageReadResult.Ok(new OfficialProductSource(
                Website: uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase),
                Title: title,
                Url: uri.ToString(),
                Content: content));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AddOfficialUrlDiagnostic($"Timeout khi đọc site hãng: {url}");
            return OfficialPageReadResult.Fail("Đọc trang sản phẩm mất quá nhiều thời gian; sẽ chỉ nhận nếu URL/title khớp rất chặt với sản phẩm.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not read official product URL.");
            AddOfficialUrlDiagnostic($"Không đọc được site hãng: {url} | {exception.GetType().Name}");
            return OfficialPageReadResult.Fail("Không đọc được đường dẫn sản phẩm chính thức; sẽ chỉ nhận nếu URL/title khớp rất chặt với sản phẩm.");
        }
    }

    private static string ExtractHtmlTitle(string html, string fallback)
    {
        var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value).Trim() : fallback;
    }

    private static bool LooksLikeNotFoundPage(string html, string title)
    {
        var normalized = RemoveVietnameseDiacritics($"{title} {html[..Math.Min(html.Length, 5000)]}")
            .ToLowerInvariant();
        return normalized.Contains("page not found") ||
            normalized.Contains("404 not found") ||
            normalized.Contains("error 404") ||
            normalized.Contains("requested page is not available") ||
            normalized.Contains("requested page is unavailable") ||
            normalized.Contains("page is not available") ||
            normalized.Contains("currently under maintenance") ||
            normalized.Contains("temporarily unavailable") ||
            normalized.Contains("please try to reconnect later") ||
            normalized.Contains("momentanement indisponible") ||
            normalized.Contains("maintenance") ||
            normalized.Contains("la page demandee n est pas accessible") ||
            normalized.Contains("this page does not exist") ||
            normalized.Contains("we can't find the page") ||
            normalized.Contains("we cant find the page") ||
            normalized.Contains("khong tim thay trang") ||
            normalized.Contains("trang khong ton tai");
    }

    private static bool IsNonPublicCommerceRoute(Uri uri)
    {
        if (IsKnownWrongDiorProductRoute(uri))
        {
            return true;
        }

        if (IsUnavailableDiorLegacyProductRoute(uri))
        {
            return true;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).ToLowerInvariant();
        var query = Uri.UnescapeDataString(uri.Query).ToLowerInvariant();
        if (path.Contains("/on/demandware.store/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/product-show", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/product-show", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (query.Contains("pid=", StringComparison.OrdinalIgnoreCase) &&
            (path.Contains("product-show", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(path.Trim('/'))))
        {
            return true;
        }

        return false;
    }

    private static bool IsKnownWrongDiorProductRoute(Uri uri)
    {
        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        if (!host.Equals("dior.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).ToLowerInvariant();
        return path.Contains("/beauty/products/miss-dior-blooming-bouquet-y0996000.html", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/beauty/products/poison-girl-y0996220.html", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/beauty/products/makeup/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/products/beauty/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnavailableDiorLegacyProductRoute(Uri uri)
    {
        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        if (!host.Equals("dior.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath).ToLowerInvariant();
        if (!path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(path, @"-y\d{5,}\.html$", RegexOptions.IgnoreCase))
        {
            return false;
        }

        // Dior has old category-specific beauty URLs that can still look like
        // product pages in search results, but open to "Requested page is not
        // available". Prefer the current /beauty/products/... canonical route.
        return path.Contains("/beauty/fragrance/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/beauty/makeup/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/beauty/skincare/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanAcceptByStrictUrlMatch(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (IsNonPublicCommerceRoute(uri))
        {
            return false;
        }

        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var path = Uri.UnescapeDataString(uri.AbsolutePath).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path.Trim('/')))
        {
            return false;
        }

        var blockedPathSignals = new[]
        {
            "/search",
            "/cart",
            "/checkout",
            "/account",
            "/login",
            "/wishlist",
            "/stores",
            "/store-locator"
        };
        if (blockedPathSignals.Any(signal => path.Contains(signal, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var knownOfficialLuxuryHosts = new[]
        {
            "dior.com",
            "yslbeautyus.com",
            "yslbeauty.com",
            "chanel.com",
            "gucci.com"
        };

        return knownOfficialLuxuryHosts.Any(domain =>
                host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)) ||
            path.Contains("product", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("products", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildGoogleResearchSourceContent(
        ConfirmedProductRequest productRequest,
        string productName,
        string researchText,
        IReadOnlyList<GroundedSource> sources)
    {
        var lines = new List<string>
        {
            "Nguồn research: Google Search grounding qua Gemini.",
            $"Tên sản phẩm người dùng xác nhận: {productName}"
        };

        AddSourceLine(lines, "Thương hiệu người dùng xác nhận", productRequest.Brand);
        AddSourceLine(lines, "Phiên bản/dòng người dùng xác nhận", productRequest.Variant);
        AddSourceLine(lines, "Mã màu người dùng xác nhận", productRequest.Shade);
        AddSourceLine(lines, "Finish/kết cấu người dùng xác nhận", productRequest.Finish);
        AddSourceLine(lines, "Loại sản phẩm người dùng xác nhận", productRequest.Category);
        AddSourceLine(lines, "Dung tích/trọng lượng người dùng xác nhận", productRequest.Size);

        if (sources.Count > 0)
        {
            lines.Add("Các nguồn Gemini đã grounding:");
            foreach (var source in sources.Take(5))
            {
                AddSourceLine(lines, "- Nguồn", $"{source.Title} | {source.Website} | {source.Url}");
            }
        }

        AddSourceLine(lines, "Bản tóm tắt research từ Google Search", researchText);
        lines.Add("Nguyên tắc dùng nguồn: chỉ dùng chi tiết khớp đúng sản phẩm/phiên bản; nếu thông tin nào không chắc thì viết theo hướng tư vấn gu và hoàn cảnh sử dụng, không bịa claim.");

        var content = string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        return content.Length <= 24_000 ? content : content[..24_000];
    }

    private static string ExtractOfficialSourceContent(
        string html,
        Uri uri,
        string title,
        ConfirmedProductRequest productRequest,
        string productName)
    {
        var lines = new List<string>
        {
            $"URL nguồn: {uri}",
            $"Tiêu đề trang: {title}"
        };

        AddSourceLine(lines, "Tên sản phẩm người dùng xác nhận", productName);
        AddSourceLine(lines, "Thương hiệu người dùng xác nhận", productRequest.Brand);
        AddSourceLine(lines, "Phiên bản/dòng người dùng xác nhận", productRequest.Variant);
        AddSourceLine(lines, "Mã màu người dùng xác nhận", productRequest.Shade);
        AddSourceLine(lines, "Finish/kết cấu người dùng xác nhận", productRequest.Finish);
        AddSourceLine(lines, "Loại sản phẩm người dùng xác nhận", productRequest.Category);
        AddSourceLine(lines, "Dung tích/trọng lượng người dùng xác nhận", productRequest.Size);
        AddSourceLine(lines, "Meta description", ExtractMetaContent(html, "description"));
        AddSourceLine(lines, "Meta keywords", ExtractMetaContent(html, "keywords"));
        AddSourceLine(lines, "OpenGraph title", ExtractMetaContent(html, "og:title"));
        AddSourceLine(lines, "OpenGraph description", ExtractMetaContent(html, "og:description"));
        AddSourceLine(lines, "Canonical URL", ExtractCanonicalUrl(html));
        AddSourceLine(lines, "Dữ liệu sản phẩm có cấu trúc", ExtractJsonLdProductText(html));
        AddSourceLine(lines, "Tiêu đề và đề mục trên trang", ExtractHeadingText(html));
        AddSourceLine(lines, "Thông số/bảng chi tiết", ExtractTableLikeText(html));
        AddSourceLine(lines, "Danh sách mô tả nổi bật", ExtractListText(html));

        var readableText = ExtractReadableText(html);
        AddSourceLine(lines, "Nội dung đọc được trên trang", readableText);

        var content = string.Join("\n", lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        return content.Length <= 28_000 ? content : content[..28_000];
    }

    private static string ExtractMetaContent(string html, string key)
    {
        var escapedKey = Regex.Escape(key);
        var patterns = new[]
        {
            $@"<meta[^>]+(?:name|property)=[""']{escapedKey}[""'][^>]+content=[""'](?<content>[^""']*)[""'][^>]*>",
            $@"<meta[^>]+content=[""'](?<content>[^""']*)[""'][^>]+(?:name|property)=[""']{escapedKey}[""'][^>]*>"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                return WebUtility.HtmlDecode(match.Groups["content"].Value).Trim();
            }
        }

        return "";
    }

    private static string ExtractCanonicalUrl(string html)
    {
        var match = Regex.Match(html, @"<link[^>]+rel=[""']canonical[""'][^>]+href=[""'](?<href>[^""']+)[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["href"].Value).Trim() : "";
    }

    private static string ExtractJsonLdProductText(string html)
    {
        var results = new List<string>();
        foreach (Match match in Regex.Matches(html, @"<script[^>]+type=[""']application/ld\+json[""'][^>]*>(?<json>[\s\S]*?)</script>", RegexOptions.IgnoreCase))
        {
            var json = WebUtility.HtmlDecode(match.Groups["json"].Value).Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                CollectJsonLdText(document.RootElement, results, depth: 0);
            }
            catch
            {
                var compact = Regex.Replace(json, @"\s+", " ").Trim();
                if (compact.Contains("Product", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(compact);
                }
            }
        }

        return CompactLines(results, 7_000);
    }

    private static void CollectJsonLdText(JsonElement element, List<string> results, int depth)
    {
        if (depth > 8 || results.Count > 80)
        {
            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    IsUsefulProductProperty(property.Name))
                {
                    AddCompactLine(results, $"{property.Name}: {property.Value.GetString()}");
                }
                else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    CollectJsonLdText(property.Value, results, depth + 1);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectJsonLdText(item, results, depth + 1);
            }
        }
    }

    private static bool IsUsefulProductProperty(string name)
    {
        var key = name.ToLowerInvariant();
        return key is "@type" or "name" or "brand" or "description" or "sku" or "mpn" or "category" or "color" or "material" or "size" or "model" or "itemcondition" or "availability" ||
            key.Contains("origin") ||
            key.Contains("variant") ||
            key.Contains("additional") ||
            key.Contains("property");
    }

    private static string ExtractHeadingText(string html)
    {
        var lines = Regex.Matches(html, @"<h[1-4][^>]*>(?<text>[\s\S]*?)</h[1-4]>", RegexOptions.IgnoreCase)
            .Select(match => CleanHtmlSnippet(match.Groups["text"].Value))
            .Where(value => value.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();
        return CompactLines(lines, 4_000);
    }

    private static string ExtractTableLikeText(string html)
    {
        var lines = new List<string>();
        foreach (Match row in Regex.Matches(html, @"<tr[^>]*>(?<row>[\s\S]*?)</tr>", RegexOptions.IgnoreCase))
        {
            var cells = Regex.Matches(row.Groups["row"].Value, @"<t[dh][^>]*>(?<cell>[\s\S]*?)</t[dh]>", RegexOptions.IgnoreCase)
                .Select(cell => CleanHtmlSnippet(cell.Groups["cell"].Value))
                .Where(value => value.Length > 0)
                .ToArray();
            if (cells.Length >= 2)
            {
                AddCompactLine(lines, $"{cells[0]}: {string.Join(" | ", cells.Skip(1))}");
            }
        }

        foreach (Match item in Regex.Matches(html, @"<dt[^>]*>(?<dt>[\s\S]*?)</dt>\s*<dd[^>]*>(?<dd>[\s\S]*?)</dd>", RegexOptions.IgnoreCase))
        {
            AddCompactLine(lines, $"{CleanHtmlSnippet(item.Groups["dt"].Value)}: {CleanHtmlSnippet(item.Groups["dd"].Value)}");
        }

        return CompactLines(lines, 6_000);
    }

    private static string ExtractListText(string html)
    {
        var lines = Regex.Matches(html, @"<li[^>]*>(?<text>[\s\S]*?)</li>", RegexOptions.IgnoreCase)
            .Select(match => CleanHtmlSnippet(match.Groups["text"].Value))
            .Where(value => value.Length >= 8 && value.Length <= 350)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();
        return CompactLines(lines, 7_000);
    }

    private static void AddSourceLine(List<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IsPlaceholder(value))
        {
            lines.Add($"{label}: {value.Trim()}");
        }
    }

    private static string ExtractReadableText(string html)
    {
        var text = Regex.Replace(html, @"<script[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<style[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        text = CleanHtmlSnippet(text);
        return text.Length <= 22_000 ? text : text[..22_000];
    }

    private static string CleanHtmlSnippet(string html)
    {
        var text = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</(?:p|div|li|tr|h[1-6])>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\s*\n\s*", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private static void AddCompactLine(List<string> lines, string value)
    {
        var clean = Regex.Replace(CleanText(value), @"\s+", " ").Trim();
        if (!string.IsNullOrWhiteSpace(clean) && !IsPlaceholder(clean) && !lines.Contains(clean, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add(clean);
        }
    }

    private static string CompactLines(IEnumerable<string> values, int maxLength)
    {
        var builder = new StringBuilder();
        foreach (var value in values)
        {
            var clean = Regex.Replace(CleanText(value), @"\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(clean) || IsPlaceholder(clean))
            {
                continue;
            }

            if (builder.Length + clean.Length + 3 > maxLength)
            {
                break;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("- ").Append(clean);
        }

        return builder.ToString();
    }

    private static SaleContentResult EmptySaleResult(ConfirmedProductRequest request, string warningMessage) =>
        new(
            "",
            "",
            "",
            [],
            "",
            new CallToActionContent("", ""),
            BuildContactInformation(request),
            [],
            new VerifiedDetails([], [], [], []),
            false,
            warningMessage);

    private static string BuildAntiRepeatInstruction(ConfirmedProductRequest request)
    {
        RecentSaleContent[] recent;
        lock (RecentContentLock)
        {
            recent = RecentSaleContents.ToArray();
        }

        var builder = new StringBuilder();
        if (recent.Length > 0)
        {
            builder.AppendLine("Không được sử dụng lại hoặc viết quá giống các nội dung gần đây sau:");
            builder.AppendLine($"- 5 tiêu đề gần nhất: {string.Join(" | ", recent.Select(item => item.Headline).Where(item => !string.IsNullOrWhiteSpace(item)).TakeLast(5))}");
            builder.AppendLine($"- 5 câu mở đầu gần nhất: {string.Join(" | ", recent.Select(item => item.Opening).Where(item => !string.IsNullOrWhiteSpace(item)).TakeLast(5))}");
            builder.AppendLine($"- 5 CTA gần nhất: {string.Join(" | ", recent.Select(item => item.CallToAction).Where(item => !string.IsNullOrWhiteSpace(item)).TakeLast(5))}");
        }

        if (request.IsRewrite)
        {
            builder.AppendLine("Người dùng đang bấm Viết lại. Hãy giữ nguyên thông tin đã xác minh nhưng chọn creativeDirection khác bài trước, đổi tiêu đề, câu mở đầu, thứ tự trình bày và CTA. Không chỉ thay vài từ đồng nghĩa.");
            if (!string.IsNullOrWhiteSpace(request.PreviousCreativeDirection))
            {
                builder.AppendLine($"CreativeDirection bài trước: {request.PreviousCreativeDirection}. Không dùng lại hướng này nếu có thể.");
            }
        }

        return builder.Length == 0 ? "Chưa có nội dung gần đây cần tránh lặp." : builder.ToString();
    }

    private static string BuildSalePrompt(
        ConfirmedProductRequest request,
        string productName,
        string searchQuery,
        string antiRepeatInstruction,
        string verifiedFacts) => $$"""
        Bạn là chuyên gia chiến lược content và bán hàng cao cấp trong lĩnh vực mỹ phẩm, làm đẹp, nước hoa, phụ kiện thời trang và dịch vụ làm đẹp.

        Hãy viết bài sale tiếng Việt chuyên nghiệp, premium, mềm mại, tinh tế nhưng vẫn có lực chốt đơn và đúng sản phẩm. Viết như một chuyên gia tư vấn làm đẹp cao cấp đang giúp khách hiểu vì sao sản phẩm đáng sở hữu, không viết như AI, catalogue hoặc bài review máy móc.

        Trước khi viết, hãy âm thầm đọc dữ liệu đã xác minh, xác định đúng tên sản phẩm, thương hiệu, dòng sản phẩm, phiên bản, màu/kết cấu nếu có, rồi chọn đúng 4 điểm bán mạnh nhất. Không hiển thị quá trình phân tích, nguồn, ghi chú nội bộ hoặc thông tin nghiên cứu.

        QUY TẮC NỘI DUNG:
        - Toàn phần sáng tạo khoảng 180 đến 230 từ để đủ chiều sâu bán hàng, nhưng vẫn phải gọn và dễ đọc.
        - Chỉ viết dựa trên ảnh, dữ liệu người dùng nhập và thông tin đã xác minh.
        - Nếu dữ liệu AI nhận diện/người dùng nhập mâu thuẫn với thông tin đã xác minh từ nguồn, ưu tiên thông tin đã xác minh.
        - Khi thông tin đã xác minh có URL nguồn và nội dung trang sản phẩm, phải đọc thật kỹ từng chi tiết: tên đầy đủ, loại sản phẩm, màu/phiên bản, xuất xứ, chất liệu/kết cấu, công dụng, mô tả, thông số, lưu ý, cách dùng và điểm khác biệt.
        - 4 highlights phải lấy từ chi tiết cụ thể đọc được trong nguồn, không được viết lợi ích chung chung có thể áp dụng cho mọi sản phẩm.
        - Không chỉ nêu công dụng. Bài phải nói rõ sản phẩm phù hợp với ai, hoàn cảnh nào và cảm xúc khách nhận được khi dùng/sở hữu hoặc khi tặng.
        - Phải xác định đúng loại sản phẩm trước khi viết: vỏ/case/refill/phụ kiện không được mô tả như son, serum, kem hoặc sản phẩm sử dụng trực tiếp.
        - Không bịa thành phần, công dụng, chứng nhận, xuất xứ, số giờ bền màu, khả năng chống lem, quà tặng, giá hoặc số lượng.
        - Nếu chưa chắc thông tin nào, bỏ thông tin đó.
        - Không dùng Markdown, HTML, ký tự gạch Unicode, tiêu đề phụ hoặc bullet ký tự.
        - Không viết giá, ưu đãi, quà tặng, số lượng, tên shop, số điện thoại, địa chỉ hoặc website.
        - Không trả creativeAngle ra nội dung hiển thị; creativeAngle chỉ để hệ thống tránh lặp khi viết lại.
        - Trước khi viết, phải tự chọn một big idea rõ ràng: sản phẩm này bán bằng cảm xúc gì, khoảnh khắc nào, lý do đáng tiền nào. Big idea này nằm trong creativeAngle và chi phối headline/opening/highlights.
        - Bài phải phát triển xa hơn phần mô tả tính năng: chuyển dữ liệu sản phẩm thành một cảnh sử dụng, một gu thẩm mỹ, một lý do mua/tặng và một cảm giác sở hữu.

        KHUNG COPYWRITING LV MAX CHO GEMINI FLASH LITE:
        - Hãy viết như một người bán hàng giỏi đang tư vấn trực tiếp, không như bài PR thương hiệu.
        - Bài sale phải nghe như người thật tư vấn tại shop: gần, rõ, có kinh nghiệm thử sản phẩm, không đọc như slogan campaign.
        - Trước khi trả JSON, tự chấm bản nháp theo 8 tiêu chí nội bộ, không hiển thị điểm: đúng sản phẩm, khách mới hiểu ngay, opening có hook, mỗi highlight có lợi ích mua hàng, note/công dụng được dịch sang cảm giác thật, không sáo, có lý do xuống tiền, CTA tự nhiên.
        - Nếu bất kỳ tiêu chí nào dưới 8/10, tự viết lại trước khi trả JSON.
        - Opening phải trả lời trong 2 giây đầu: "Sản phẩm này hợp với ai và vì sao đáng đọc tiếp?"
        - Mỗi highlight phải có một insight khách hàng rõ: sợ mùi quá gắt, muốn đi làm chỉn chu, cần món quà tinh tế, muốn makeup nhanh, sợ màu khó dùng, muốn da dễ chịu, muốn tóc sạch nhẹ.
        - Không để highlight chỉ là danh sách thành phần/note/tính năng. Mỗi ý phải biến đặc điểm thành lý do mua.
        - ProductNotice phải tạo lực mua thật: đáng mua vì dùng được dịp nào, hợp gu nào, đáng làm quà vì sao, đáng sở hữu vì điểm khác biệt nào.
        - Closing phải khiến khách muốn nhắn hỏi tư vấn, nhưng không lặp lại CTA và không ép mua.

        TẦNG WOW VÀ PHÙ HỢP CÁ NHÂN:
        - Bài không được chỉ "hay"; bài phải làm khách tự thấy sản phẩm này có khả năng hợp với mình.
        - Opening phải có một khoảnh khắc soi đúng người đọc: nàng/chàng đang cần gì, sợ gì, muốn đẹp/thơm/chỉn chu trong tình huống nào.
        - Mỗi bài phải có ít nhất 2 tín hiệu chọn đúng gu như: "hợp nhất khi...", "đáng thử nếu...", "nàng sẽ thích nếu...", "chàng sẽ hợp nếu...", "không hợp nếu nàng chỉ thích...".
        - Phải có một reason-to-believe rõ: vì sao chính sản phẩm này giải quyết nhu cầu đó, dựa trên note hương/kết cấu/màu/công dụng/thông số đã xác minh.
        - Wow đến từ sự cụ thể và khôn ngoan, không đến từ từ lớn. Hãy viết để khách nghĩ "đúng gu mình" thay vì "nghe sang nhưng không hiểu".
        - Mỗi highlight phải trả lời đủ 3 câu: khách nhận được gì, dùng trong hoàn cảnh nào, vì sao điểm này đáng quan tâm.
        - Nếu sản phẩm không hợp một nhóm gu nào đó, có thể nói mềm: "không dành cho nàng thích mùi thật ngọt/son thật lì/tông quá nổi" để tăng độ tin cậy.
        - Với nước hoa, phải có profile matching: sạch hay ngọt, ấm hay mát, mềm hay rõ, hợp đi làm/đi tối/tặng ai; nếu chưa chắc độ bám tỏa thì không khẳng định số giờ.
        - Câu yếu: "giúp nàng tự tin khẳng định bản thân". Câu tốt: "hợp nàng muốn đi làm vẫn sạch gọn, tối đi hẹn có thêm chút ấm và mềm trên da".
        - Bài đạt 90% phải có một câu làm khách gật đầu vì đúng gu: ví dụ "nếu nàng thích mùi rõ nhưng sợ bị gắt, đây là kiểu đáng thử".
        - Không dùng headline kiểu định nghĩa phong thái/tự do/cá tính. Headline phải bán cảm giác cụ thể: sạch ấm, hoa trắng mềm, ngọt vừa, đi làm lẫn đi tối, quà tặng tinh ý.
        - Với sản phẩm giá cao, phải làm rõ "vì sao đáng tiền" bằng tính dễ dùng, gu mùi/tông màu khó thay thế, độ hợp nhiều dịp hoặc giá trị làm quà; không chỉ nói dung tích lớn là kinh tế.
        - Không viết câu kết kiểu "đội ngũ hỗ trợ tư vấn"; hãy viết gần người mua hơn: "nàng có thể hỏi để được thử đúng gu sạch, ngọt hay ấm trước khi chọn".

        GIỌNG VĂN:
        - Premium, sang, mềm mại, có cảm xúc vừa đủ và rất chuyên nghiệp.
        - Tư vấn như người bán hàng cao cấp: hiểu sản phẩm, hiểu nhu cầu làm đẹp, biết nhấn vào điểm khiến khách muốn sở hữu.
        - Đánh mạnh vào điểm tốt nhất của sản phẩm trước, sau đó mới mở rộng sang các lợi ích phụ.
        - Câu văn mượt, có nhịp, không khô cứng nhưng vẫn dễ lướt.
        - Ưu tiên lợi ích thật khách nhận được: đẹp hơn ở đâu, tiện hơn thế nào, dùng trong hoàn cảnh nào, cảm giác sở hữu ra sao.
        - Bắt buộc dùng xưng hô theo đúng đối tượng sản phẩm và người mua quà, tạo cảm giác đang tư vấn riêng cho khách.
        - Nếu sản phẩm dành cho nữ hoặc thiên nữ: dùng "nàng/các nàng" là xưng hô chính; có thể thêm góc "chàng tặng nàng" nếu sản phẩm hợp làm quà.
        - Nếu sản phẩm dành cho nam hoặc thiên nam: dùng "chàng/các chàng" khi nói về người sử dụng; đồng thời có thể viết góc "nàng/các nàng tặng cho chàng" khi sản phẩm hợp làm quà.
        - Nếu sản phẩm unisex: chọn một góc bán rõ ràng theo thông tin nguồn; có thể dùng "nàng chọn cho mình", "nàng tặng chàng" hoặc "chàng tặng nàng" nhưng phải tự nhiên.
        - Ít nhất opening hoặc closing phải có đúng nhóm xưng hô đã chọn: "nàng/các nàng" hoặc "chàng/các chàng".
        - Không dùng lối nói xa cách như "khách hàng", "người dùng", "phái đẹp", "quý ông" nếu có thể nói gần hơn bằng "nàng", "các nàng", "chàng", "các chàng".
        - Mỗi câu phải có giá trị bán hàng hoặc giá trị tư vấn; bỏ mọi câu trang trí không cần thiết.
        - Không lạm dụng các cụm: không chỉ... mà còn, đừng bỏ lỡ, mua ngay, chốt đơn ngay, gọi ngay, sở hữu ngay hôm nay.
        - Không lạm dụng các từ: đẳng cấp, hoàn hảo, thời thượng, kiêu kỳ, tỏa sáng, tuyệt vời, vượt trội, biểu tượng, danh tiếng, hào nhoáng, tuyên ngôn, khí chất, cá tính.
        - Không viết nhạt kiểu "phù hợp nhiều phong cách", "thiết kế sang trọng", "chất lượng cao", "lựa chọn lý tưởng" nếu không nối được với lợi ích cụ thể.
        - Không biến bài sale thành lời khen hình tượng phụ nữ như "mạnh mẽ", "độc lập", "làm chủ", "khẳng định bản thân" nếu không nói được mùi giúp khách dùng trong tình huống nào.
        - Dùng từ khôn ngoan thay cho từ phô: "đáng thử nếu", "hợp nhất khi", "điểm hay là", "gu này hợp với", "nên chọn nếu", "không dành cho nàng chỉ thích".

        BỘ LỌC 90% TRƯỚC KHI TRẢ LỜI:
        - Tự đọc lại bài như một khách chưa biết sản phẩm. Nếu opening chưa làm khách muốn đọc tiếp, viết lại.
        - Nếu bài có nhiều hơn 1 câu có thể dùng cho bất kỳ sản phẩm cùng loại nào, viết lại.
        - Nếu có highlight chỉ khen thiết kế, thương hiệu hoặc cảm xúc chung mà không giúp khách quyết định mua, thay bằng gu dùng/dịp dùng/lý do đáng thử.
        - Nếu title highlight là thuật ngữ như "hương hoa", "nốt hương", "thiết kế", "phong thái", "cảm giác", "tinh dầu", phải đổi sang lợi ích cụ thể.
        - Ít nhất một ý phải tạo độ tin cậy bằng tiêu chí chọn/không chọn: hợp nếu thích gì, không hợp nếu chỉ thích gì.
        - Câu nào có chữ "giúp nàng" phải kiểm tra phía sau có lợi ích thật, không phải lời khen mơ hồ.

        CẤM VĂN AI SÁO RỖNG:
        - Không dùng các cụm nghe phô hoặc buồn cười như: "cỗ máy thời gian", "biểu tượng của sự tinh tế", "khí chất vượt thời gian", "trên cổ tay thanh mảnh", "tôn vinh vẻ đẹp", "khẳng định dấu ấn", "nâng tầm phong cách", "tuyệt tác", "kiệt tác", "vẻ đẹp vượt thời gian", "hào nhoáng".
        - Không dùng các câu yếu, thiếu lực bán như: "hy vọng nàng sẽ tìm thấy", "món phụ kiện chân ái", "người bạn đồng hành trung thành", "lưu giữ những cột mốc", "mảnh ghép còn thiếu", "mảnh ghép hoàn hảo", "kết hợp hoàn hảo", "những khoảnh khắc đáng nhớ", "đánh thức vẻ ngoài", "dấu ấn riêng", "đồng hành cùng nàng", "tự tin tỏa sáng", "bí quyết", "cân nhắc sở hữu", "vừa đủ chỉnh chu lại vừa đủ thu hút", "nếu nàng đã yêu", "nếu nàng đã biết", "vườn hoa Gucci Bloom", "hoa cỏ ban ngày", "những phiên bản ban ngày", "tuyên ngôn tự do", "khẳng định cá tính riêng", "khẳng định bản thân", "định hình cá tính", "định nghĩa phong thái", "bản sắc riêng", "phong thái đương đại", "khí chất sang trọng", "thiết kế nghệ thuật", "đội ngũ hỗ trợ".
        - Không mở bài kiểu văn hoa xa thực tế: "Trên đôi môi...", "Trên làn da...", "Trên cổ tay..." nếu câu đó không tạo lợi ích bán hàng cụ thể.
        - Không biến sản phẩm thành triết lý hoặc biểu tượng lớn lao. Hãy nói đời thường hơn: người sử dụng thấy đẹp ở đâu, tiện ở đâu, hợp dịp nào, vì sao đáng mua.
        - Nếu là đồng hồ/phụ kiện, không gọi là "cỗ máy" hoặc "biểu tượng"; hãy nói về độ chỉn chu, dễ phối đồ, cảm giác tự tin khi đeo, phù hợp đi làm/đi tiệc/gặp khách.
        - Nếu câu nghe như quảng cáo xa xỉ sáo rỗng, phải viết lại gần người mua hơn và cụ thể hơn.

        LUẬT VIẾT CHO KHÁCH MỚI:
        - Luôn giả định người đọc chưa biết thương hiệu, chưa biết dòng sản phẩm, chưa biết phiên bản này khác gì bản khác.
        - Opening phải tự giải thích sản phẩm bằng lợi ích dễ hiểu: đây là loại sản phẩm gì, cảm giác chính là gì, hợp hoàn cảnh nào, vì sao khách nên quan tâm.
        - Không mở bài bằng kiến thức nội bộ của fan thương hiệu như "nếu nàng đã yêu dòng...", "phiên bản này tiếp nối...", "vườn hoa của thương hiệu..."; người mới sẽ không hiểu và không có lý do mua.
        - Nếu cần nhắc dòng/phiên bản, phải giải thích ngay bằng ngôn ngữ đời thường. Ví dụ: "Ambrosia D'Oro là kiểu nước hoa hoa trắng ấm, hợp khi nàng muốn đi hẹn hò hoặc dự tiệc với cảm giác nữ tính nhưng không quá ngọt."
        - Không dùng khái niệm mơ hồ như "hoa cỏ" nếu không giải thích thành mùi cụ thể. Hãy nói "hoa trắng", "mùi sạch", "mùi ngọt ấm", "mùi gỗ mềm", "mùi dễ gần" theo dữ liệu nguồn.
        - Các note hương khó hiểu phải được chuyển sang cảm giác dễ hình dung. Ví dụ "lavender/hoa oải hương" nên viết là "chút lavender sạch, hơi thảo mộc giúp mùi bớt ngọt" thay vì chỉ ném chữ "oải hương Pháp".
        - Mỗi bài phải đọc được độc lập: khách nhìn bài lần đầu vẫn hiểu sản phẩm bán gì, hợp với mình không, và vì sao đáng hỏi tư vấn.

        CÁCH VIẾT PREMIUM:
        - headline phải có cảm giác cao cấp, đúng điểm mạnh nhất, không quá quảng cáo, tối đa 10 từ nếu có thể.
        - opening mở mềm, nói trực tiếp với nàng/các nàng hoặc chàng/các chàng theo đúng sản phẩm, nhưng phải tự giới thiệu giá trị sản phẩm cho người chưa biết gì về dòng đó.
        - Mỗi highlight gồm: tên lợi ích ngắn + mô tả lợi ích cụ thể, chuyển chi tiết sản phẩm thành lý do mua hàng và cảm xúc sử dụng.
        - Title highlight phải là lợi ích khách hiểu ngay, không phải thuật ngữ mùi. Không viết title như "Nốt hương chiều sâu", "Hương hoa đặc trưng", "Thiết kế ấn tượng", "Tinh dầu oải hương". Viết tốt hơn: "Sạch nhưng không lạnh", "Ấm nhẹ khi đi tối", "Đi làm vẫn dễ chịu", "Hợp nàng sợ mùi gắt".
        - Title highlight nên giống lời tư vấn ngắn, ví dụ "Sạch hơn nhờ lavender", "Ấm mềm trên da", "Đi làm không quá gắt", "Đáng thử nếu thích mùi rõ".
        - 4 highlights phải có 4 vai trò khác nhau, không được cùng một kiểu mô tả: 1 ý về điểm khác biệt của sản phẩm, 1 ý về trải nghiệm khi dùng, 1 ý về bối cảnh/gu phù hợp, 1 ý về lý do đáng mua hoặc đáng tặng.
        - Cả 4 highlights đều phải có ít nhất một trong ba yếu tố: ai phù hợp, hoàn cảnh sử dụng, cảm xúc/khí chất khi dùng, sở hữu hoặc nhận quà.
        - Ít nhất 2 trong 4 highlights phải trực tiếp dùng đúng nhóm xưng hô đã chọn: "nàng/các nàng" hoặc "chàng/các chàng".
        - productNotice nếu có phải nâng bài lên một tầng nữa: vì sao sản phẩm này đáng xuống tiền, đáng giữ lại, đáng mua trọn bộ, đáng làm quà hoặc đáng có trong bộ sưu tập.
        - closing phải chắc, nói với nàng/các nàng hoặc chàng/các chàng và khép lại bằng một hình ảnh sở hữu rõ ràng; không dùng "hy vọng", không chúc chung chung.
        - CTA tư vấn nhẹ nhàng, sang, hướng khách nhắn để được chọn đúng màu/phiên bản/cách dùng/tình trạng sản phẩm, không ép mua.

        NGÔN NGỮ TƯ VẤN THẬT:
        - Ưu tiên câu cụ thể như: "hợp nàng cần mùi sạch nhưng vẫn có độ ấm", "dùng đi làm không bị quá gắt", "đi tối có điểm nhấn hơn", "hợp nàng không thích mùi quá ngọt".
        - Tránh câu hình tượng như: "tuyên ngôn", "khẳng định", "làm chủ", "định hình", "nâng tầm", "đầy cá tính" vì khách không biết mùi thật ra như thế nào.
        - Không khen chai/lọ như một highlight chính của nước hoa, trừ khi bán quà tặng và phải nói rõ lợi ích: nhìn lịch sự khi tặng, hộp/chai đẹp để biếu.

        CÔNG THỨC MỞ BÀI BẮT BUỘC:
        - Opening không được mở bằng tên sản phẩm đơn thuần hoặc so sánh với dòng cũ.
        - Opening nên theo một trong các mẫu tư duy sau, nhưng viết tự nhiên theo sản phẩm:
        - "Nếu nàng muốn [kết quả/cảm giác cụ thể] khi [hoàn cảnh], [tên/phiên bản] là kiểu [sản phẩm] đáng thử."
        - "Hợp với nàng [nỗi muốn/nỗi sợ cụ thể], vì [điểm thật của sản phẩm] giúp [lợi ích nhìn/cảm nhận được]."
        - "Đây là lựa chọn cho nàng cần [workflow/dịp dùng], muốn [cảm giác] nhưng vẫn [điều kiện quan trọng: không gắt/không phô/dễ dùng]."

        CẤP ĐỘ BÀI SALE VƯỢT TRỘI:
        - Không chỉ viết "đây là lựa chọn..." rồi lặp lại note/công dụng. Mỗi đoạn phải làm khách thấy sản phẩm có cá tính riêng.
        - Bài tốt phải có nhịp: mở bài tạo ham muốn, highlights chứng minh bằng chi tiết, productNotice tạo lý do xuống tiền, closing để khách thấy mình nên hỏi ngay.
        - Dùng hình ảnh cảm xúc vừa đủ, ví dụ "mùi hoa trắng ấm hơn khi chiều xuống", "một lớp hương khiến nàng chỉn chu hơn trong buổi tối", nhưng không biến thành thơ dài.
        - Nếu sản phẩm là nước hoa, phải mô tả hành trình mùi theo cảm giác dễ hiểu: mở đầu, thân hương, nền hương, độ hợp dịp; không chỉ liệt kê note.
        - Nếu là sản phẩm cao cấp, bài phải gợi được giá trị sở hữu: cảm giác dùng hàng có gu, bản thân được chăm chút hơn, món quà có sự tinh ý, nhưng không khoe khoang.
        - Nếu có phiên bản cụ thể, headline/hashtag/highlight phải bám đúng phiên bản đó; không kéo sang phiên bản khác trong cùng dòng sản phẩm.

        KHUNG BÀI SALE PHẢI CÓ LỰC:
        - headline: nêu đúng điểm hấp dẫn nhất, không chỉ ghép tên sản phẩm.
        - opening: chạm một nhu cầu cụ thể của người mua/người dùng và nói rõ sản phẩm giải quyết nhu cầu đó thế nào, ví dụ muốn đi làm chỉn chu, đi tiệc có điểm nhấn, makeup nhanh vẫn đẹp, da dễ chịu hơn hoặc chọn quà tinh tế.
        - 4 highlights: mỗi ý phải trả lời "vì sao khách nên quan tâm?", không chỉ nói đặc điểm.
        - Mỗi highlight phải có công thức: đặc điểm/công dụng thật của sản phẩm + tình huống của người dùng/người mua quà + cảm giác/lý do muốn mua.
        - Không được chỉ viết "chất son mềm mượt", "thiết kế nhỏ gọn", "dễ phối đồ"; phải nói rõ điều đó giải quyết nhu cầu nào cho khách.
        - productNotice/closing: tạo lý do đáng thử hoặc đáng hỏi tư vấn, nhưng không được lặp lời kêu gọi nhắn tin vì callToAction đã làm việc đó.
        - CTA: mời khách nhắn để được tư vấn chọn đúng phiên bản, phối với phong cách, cách dùng hoặc kiểm tra tình trạng sản phẩm.
        - Bài đạt chuẩn sale phải có đủ 3 lớp: ai nên mua hoặc mua tặng ai, điểm nào làm sản phẩm đáng tiền, và bước tiếp theo khách nên làm.
        - Nếu form có số lượng còn lại ít, có thể viết closing theo hướng khách nên nhắn kiểm tra tình trạng trước khi chốt nhưng không tự bịa số lượng.
        - Với sản phẩm giá trị cao, phải nói rõ vì sao đáng cân nhắc: tính dễ dùng, độ giữ giá/giá trị sưu tầm nếu nguồn có, tình trạng/phiên bản/màu hiếm nếu nguồn có, khả năng phối trong nhiều bối cảnh.

        VÍ DỤ CHUYỂN CÂU YẾU SANG CÂU CÓ LỰC:
        - Không viết: "Mỗi chiếc đồng hồ là một người bạn đồng hành trung thành."
        - Viết tốt hơn: "Đây là lựa chọn hợp với nàng muốn một chiếc đồng hồ đi làm vẫn lịch sự, lên tiệc vẫn có điểm nhấn."
        - Không viết: "Hy vọng nàng tìm thấy món phụ kiện chân ái."
        - Viết tốt hơn: "Nếu nàng thích phụ kiện sang nhưng không phô, mẫu này rất đáng để thử trên tay."
        - Không viết: "Hãy để chiếc Rolex này cùng nàng tạo nên những khoảnh khắc đáng nhớ."
        - Viết tốt hơn: "Nếu nàng cần một chiếc đồng hồ vừa đi làm gọn gàng vừa lên tiệc đủ nổi bật, mẫu này đáng để hỏi tình trạng ngay."
        - Không viết: "Chất son mềm mượt giúp nàng cảm thấy thoải mái."
        - Viết tốt hơn: "Hợp nàng sợ son lì làm môi khô, vì chất son lướt nhẹ giúp môi nhìn mịn mà vẫn dễ dùng mỗi ngày."
        - Không viết: "Thiết kế mini tiện lợi để mang theo."
        - Viết tốt hơn: "Hợp nàng hay dặm son sau ăn hoặc trước cuộc hẹn, vì size mini bỏ túi nhỏ nào cũng gọn."
        - Không viết: "Bộ đôi này là lựa chọn lý tưởng cho ngày dài."
        - Viết tốt hơn: "Hợp nàng không có nhiều thời gian makeup buổi sáng: một món xử lý nền, một món kéo sắc môi tươi lại rất nhanh."
        - Không viết: "Công thức lành tính nên không lo hại da môi."
        - Viết tốt hơn: "Nếu nguồn có nói thành phần dưỡng, hãy viết: chất son có dưỡng giúp môi dễ chịu hơn khi dùng hằng ngày."

        CHÂN DUNG KHÁCH HÀNG VÀ CẢM XÚC:
        - Hãy tự suy luận từ loại sản phẩm và thông tin đã xác minh để xác định nhóm khách phù hợp, nhưng không bịa công dụng.
        - Có thể nói về nàng/chàng thích vẻ tự nhiên, muốn diện mạo gọn gàng, cần món nhỏ xinh trong túi, đi làm/đi chơi/dự tiệc, thích cảm giác chỉn chu hoặc muốn món quà tinh tế.
        - Hãy viết để khách thấy mình trong bài bằng đúng xưng hô đã chọn, ví dụ "nàng thích...", "các nàng cần...", "chàng muốn...", "khi chàng...".
        - Bài phải làm khách tự nhận ra mình phù hợp: nêu rõ nỗi muốn, thói quen hoặc tình huống của người mua/người dùng trước khi nói lợi ích.
        - Đánh vào cảm xúc mua hàng: cảm giác được nâng niu, tự tin hơn, chỉn chu hơn, có gu hơn, yên tâm hơn, muốn lấy ra dùng mỗi ngày.
        - Cảm xúc phải cụ thể và gần đời, không bay bổng quá mức. Ví dụ: "nàng thấy gương mặt tươi hơn", "nàng tự tin hơn khi gặp khách", "nàng thấy món đồ trong túi có gu hơn".
        - Với sản phẩm skincare: chạm vào cảm giác da dễ chịu, yên tâm chăm da, phù hợp nàng muốn chu trình gọn mà vẫn chỉn chu.
        - Với makeup/son/lip gloss: chạm vào nỗi muốn môi/gương mặt tươi hơn, sợ màu khó dùng, sợ môi khô/lộ vân, cần dặm nhanh sau ăn, đi làm/hẹn hò/chụp hình vẫn chỉn chu.
        - Với combo/bộ đôi/bộ trang điểm: bán theo routine hoàn chỉnh, không kể rời rạc từng món. Phải nói rõ bộ này giúp nàng tiết kiệm thời gian, dễ có nền gọn + môi tươi + mắt/má chỉn chu, hợp đi làm, đi chơi, gặp khách hoặc cần makeup nhanh vẫn đẹp.
        - Với combo, headline/opening phải nêu lợi ích mua trọn bộ: đỡ phải phối lẻ từng món, các bước ăn ý với nhau, chuẩn bị nhanh hơn.
        - Với combo, mỗi highlight nên nối các món với nhau nếu hợp lý: nền làm mặt sạch gọn, son kéo sắc mặt tươi, má/mắt tạo điểm nhấn, cả bộ giúp nàng hoàn thiện layout nhanh hơn.
        - Với combo, productNotice hoặc closing phải nêu lý do mua cả set thay vì mua lẻ, nhưng không tự bịa tiết kiệm bao nhiêu tiền nếu form không có.
        - Không dùng claim hình ảnh quá đà như "làn da em bé", "căng bóng như da em bé" nếu nguồn không ghi rõ.
        - Không tự khẳng định "an toàn", "không hại da/môi", "không gây kích ứng" nếu nguồn không có bằng chứng rõ.
        - Với nước hoa: chạm vào gu mùi và bối cảnh dùng thật; nói nàng hợp mùi này khi đi làm, gặp khách, hẹn hò, cuối tuần hoặc cần mùi sạch nhẹ, không nồng, dễ gần. Không viết thơ kiểu "đánh thức ngày mới", "dấu ấn riêng", "đồng hành cùng nàng".
        - Nước hoa phải nói được mùi này tạo cảm giác gì quanh nàng: sạch, tươi, mềm, nữ tính, gần gũi, dễ chịu, sang nhẹ, ấm áp hoặc cuốn hút; tránh văn bay bổng khiến khách không hình dung được mùi.
        - Không chỉ liệt kê note hương kiểu "hoa cam, hoa nhài, oải hương Pháp". Phải nói note đó tạo cảm giác gì: sạch hơn, mềm hơn, bớt ngọt, ấm hơn, hợp đi làm hay đi tối.
        - Nếu nói về thiết kế chai, tránh "hào nhoáng", "phụ kiện giá trị". Chỉ nói khi có giá trị bán hàng rõ: chai đẹp để làm quà, đặt trên bàn trang điểm nhìn sang, cầm chắc tay, nhận diện thương hiệu tốt.
        - Với nước hoa, 4 highlights nên ưu tiên: 1 gu mùi dễ hiểu, 1 cảm giác khi xịt, 1 dịp dùng, 1 lý do đáng mua/tặng. Không dùng một highlight riêng chỉ để khen thiết kế chai nếu không có thông tin quà tặng.
        - Với nước hoa, ít nhất 3 highlights phải nói về mùi trên da, gu người dùng, dịp dùng hoặc độ dễ chịu; tối đa 1 câu nhắc chai/hộp và chỉ khi phục vụ quà tặng.
        - Với YSL Libre EDP hoặc mùi tương tự, không viết "tuyên ngôn tự do", "định hình cá tính", "khẳng định bản thân". Viết tốt hơn: "Hợp nàng muốn một mùi sạch, hơi ấm và đủ rõ để đi làm lẫn đi tối mà không quá ngọt."
        - Với YSL Libre EDP hoặc mùi tương tự, không viết "Nốt hương chiều sâu", "vững vàng suốt ngày dài". Viết tốt hơn: "Ấm nhẹ khi đi tối — Vanilla và xạ hương làm mùi mềm hơn, hợp nàng muốn có điểm nhấn mà không bị ngọt gắt."
        - Với YSL Libre EDP hoặc mùi tương tự, không viết "hương hoa đặc trưng", "oải hương Pháp", "phong thái đương đại", "định nghĩa phong thái tự do". Viết tốt hơn: "Lavender sạch làm mùi bớt ngọt, hoa cam thêm độ sáng, vanilla giúp phần cuối ấm hơn trên da."
        - Với YSL Libre EDP hoặc mùi tương tự, nên có câu chọn gu: "đáng thử nếu nàng muốn mùi sạch, hơi ấm, rõ vừa đủ; không hợp nếu nàng chỉ thích mùi thật ngọt hoặc thật nhẹ."
        - Với nước hoa nữ có chiều sâu như hương hoa trắng, gỗ, nhựa thơm, mật, gia vị hoặc phiên bản intense/ambrosia/limited: hãy bán bằng cảm giác buổi tối, đi tiệc, hẹn hò, dịp đặc biệt, sự nữ tính có chiều sâu; đừng viết như nước hoa ban ngày tươi sáng.
        - Với nước hoa, opening phải có công thức: "Nếu nàng đang tìm một mùi..." + gu mùi cụ thể + hoàn cảnh dùng + cảm giác người khác nhận ra. Không bắt đầu bằng việc so sánh với phiên bản cũ nếu khách mới chưa biết phiên bản cũ.
        - Opening nước hoa phải tránh các cụm mơ hồ như "hoa cỏ", "phiên bản ban ngày", "chiều sâu hơn những phiên bản..." vì khách mới không hiểu. Hãy viết thẳng kiểu: "Nếu nàng muốn một mùi hoa trắng ấm, mềm và đủ cuốn hút cho buổi tối..."
        - Với nước hoa nam: chạm vào gu mùi của chàng, bối cảnh chàng dùng và giá trị làm quà. Có thể viết "nàng tặng cho chàng" hoặc "các chàng muốn..." nhưng phải dựa trên dữ liệu nguồn.
        - Nước hoa nam phải nói được mùi tạo cảm giác gì quanh chàng: sạch, nam tính, trầm, ấm, tự tin, chỉnh chu, dễ gần hoặc cuốn hút; tránh biến thành văn xa xỉ sáo rỗng.
        - Với sản phẩm nữ hợp làm quà: có thể thêm góc "chàng tặng nàng" để bài có cảm xúc hơn, nhưng không làm lệch đối tượng sử dụng chính.
        - Với sản phẩm tóc: chạm vào nỗi sợ tóc bết, tóc xẹp, tóc khô xơ, khó vào nếp; nói cụ thể nàng dùng để tóc nhìn sạch, nhẹ, mềm hoặc dễ chải hơn theo thông tin nguồn.
        - Không dùng claim quá đà như "cân bằng dầu nhờn vượt trội", "giảm gãy rụng", "trị gàu", "phục hồi hư tổn" nếu nguồn không nói rõ.
        - Với vỏ/case/phụ kiện: chạm vào cảm giác nâng tầm món đồ, gu thẩm mỹ, sự chỉn chu khi lấy ra sử dụng, giá trị làm quà.
        - Tránh nói chung chung kiểu "phù hợp mọi cô gái"; phải có hình ảnh khách hàng cụ thể và lý do cụ thể.

        BỐ CỤC JSON BẮT BUỘC:
        - creativeAngle: hướng triển khai nội bộ, ngắn gọn, không được đưa ra frontend.
        - headline: tiêu đề tối đa 12 từ, có lực, đúng điểm bán mạnh nhất, không dùng dấu hai chấm.
        - opening: 1 đến 2 câu, đi thẳng vào nhu cầu, hoàn cảnh dùng hoặc lợi ích chính.
        - highlights: đúng 4 object. Mỗi object có icon, title, description. Icon bắt buộc là emoji thật, không dùng chữ tiếng Anh như sparkles, droplet, palette, check. Mỗi lợi ích dùng một emoji khác nhau, title ngắn, description cụ thể 14 đến 24 từ.
        - productNotice: một câu ngắn tạo mong muốn sở hữu hoặc lưu ý dùng hàng đúng sản phẩm; để chuỗi rỗng nếu không cần.
        - closing: một câu kết tinh tế, không lặp CTA, không dùng "nhắn tin", "inbox", "liên hệ" vì callToAction đã có riêng.
        - callToAction: đúng một object có icon và text. CTA ngắn, có lợi ích tư vấn, không dùng "mua ngay", "chốt đơn ngay", "gọi ngay".
        - hashtags: 5 đến 7 hashtag, luôn có dấu #, viết liền, liên quan trực tiếp sản phẩm.
        - Hashtag phải khớp đúng thương hiệu, dòng và phiên bản. Không dùng hashtag của phiên bản gần giống nhưng khác tên, ví dụ không đổi Ambrosia D'Oro thành AmbrosiaDiFiori.
        - Gợi ý emoji cho icon: ✨ 💧 🎨 ✅ 💄 🌸 🛡️ 🪞 🫧 🌿 ☀️ 🌹 💫 🖤.

        HƯỚNG TRIỂN KHAI THEO LOẠI SẢN PHẨM:
        - Son môi: màu sắc, thần thái, hoàn thiện makeup, dịp sử dụng, giá trị khi làm quà.
        - Serum/dưỡng da: tình trạng da, cảm giác dùng, lợi ích chăm sóc, sự tiện trong chu trình.
        - Kem chống nắng: bảo vệ da, trải nghiệm trên da, dùng hằng ngày.
        - Nước hoa: phong cách, cảm xúc, dấu ấn cá nhân, hoàn cảnh sử dụng.
        - Tóc: diện mạo tóc, trải nghiệm chăm sóc, sự tự tin.
        - Dịch vụ makeup: thần thái, sự kiện, tay nghề, trải nghiệm, sự an tâm.

        GÓC VIẾT QUÀ TẶNG THEO GIỚI TÍNH:
        - Tự xác định sản phẩm dành cho nam, nữ hay unisex từ tên, category, mô tả nguồn và bối cảnh sử dụng.
        - Sản phẩm nam: ưu tiên các cụm tự nhiên như "các chàng", "chàng", "nàng chọn tặng chàng", "các nàng muốn người thương có mùi hương/diện mạo chỉn chu hơn".
        - Sản phẩm nữ: ưu tiên "nàng", "các nàng"; nếu hợp quà tặng, thêm "chàng tặng nàng" hoặc "chàng chọn cho nàng" ở opening, productNotice hoặc closing.
        - Không dùng cả hai hướng nam/nữ lẫn lộn trong cùng một câu; bài phải có một đối tượng sử dụng chính rõ ràng.
        - Với hàng giới hạn, hàng sưu tầm hoặc sản phẩm giá trị cao, hãy nêu lý do đáng mua/tặng: phiên bản đặc biệt, độ khác biệt, tính hiếm, cảm giác nhận quà, nhưng chỉ dùng khi nguồn hoặc tên sản phẩm hỗ trợ.

        MẪU TƯ DUY CHO NƯỚC HOA NỮ CAO CẤP:
        - Nếu sản phẩm là nước hoa nữ, hãy ưu tiên concept có chiều sâu như: "mùi hương dành cho nàng muốn buổi tối có sức hút hơn", "một bó hoa trắng ấm, dày và gợi cảm hơn bản ban ngày", "món quà tinh tế cho nàng thích mùi nữ tính nhưng không quá ngọt".
        - Với Gucci Bloom Ambrosia D'Oro hoặc sản phẩm tương tự, không viết "nếu nàng đã yêu vườn hoa Gucci Bloom". Viết tốt hơn: "Nếu nàng muốn một mùi hoa trắng ấm, mềm và đủ cuốn hút cho buổi tối, Ambrosia D'Oro là lựa chọn rất đáng thử."
        - Highlight 1 nói về linh hồn mùi hương, highlight 2 nói về độ ấm/độ sâu, highlight 3 nói về dịp dùng, highlight 4 nói về giá trị sở hữu hoặc làm quà.
        - Có thể dùng cụm "chàng chọn tặng nàng" nếu sản phẩm hợp làm quà, nhưng chỉ 1 lần là đủ để bài sang và tự nhiên.

        KHI CÓ LINK SẢN PHẨM:
        - Coi nội dung đọc được từ link là nguồn chính.
        - Chuyển hóa thông tin trang thành bài sale VIP Pro Max, không bê nguyên văn dài dòng.
        - Nếu link cho biết sản phẩm chỉ là vỏ/case/phụ kiện, bài sale phải bán đúng vỏ/case/phụ kiện đó.
        - Nếu nguồn có màu, họa tiết, chất liệu, xuất xứ, phân loại, đối tượng dùng hoặc tình trạng sản phẩm, hãy ưu tiên đưa vào headline/opening/highlights.
        - Không nhắc "theo trang", "nguồn cho biết", "tôi đã đọc", "thông tin đã xác minh" trong bài.

        DỮ LIỆU SẢN PHẨM:
        Tên sản phẩm AI/người dùng đang xác nhận: {{productName}}
        Thương hiệu: {{request.Brand}}
        Dòng/Phiên bản: {{request.Variant}}
        Mã màu: {{request.Shade}}
        Finish/Kết cấu: {{request.Finish}}
        Loại sản phẩm: {{request.Category}}
        Kích thước/Dung tích: {{request.Size}}
        Truy vấn xác minh: {{searchQuery}}
        Thông tin đã xác minh: {{verifiedFacts}}

        CHỐNG LẶP:
        {{antiRepeatInstruction}}

        Chỉ trả JSON hợp lệ theo đúng schema:
        {
          "creativeAngle": "",
          "headline": "",
          "opening": "",
          "highlights": [
            {"icon": "✨", "title": "", "description": ""},
            {"icon": "💧", "title": "", "description": ""},
            {"icon": "🎨", "title": "", "description": ""},
            {"icon": "✅", "title": "", "description": ""}
          ],
          "productNotice": "",
          "closing": "",
          "callToAction": {"icon": "", "text": ""},
          "hashtags": []
        }

        Không thêm trường khác. Không thêm nội dung ngoài JSON.
        """;

    private static void RememberSaleContent(SaleContentResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Headline) && string.IsNullOrWhiteSpace(result.Opening))
        {
            return;
        }

        lock (RecentContentLock)
        {
            RecentSaleContents.Enqueue(new RecentSaleContent(
                result.Headline.Trim(),
                result.Opening.Trim(),
                FormatCallToAction(result.CallToAction)));

            while (RecentSaleContents.Count > 5)
            {
                RecentSaleContents.Dequeue();
            }
        }
    }

    private static string ExtractOpening(string article)
    {
        var firstBlock = article
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(block => block.Trim())
            .FirstOrDefault(block => !string.IsNullOrWhiteSpace(block)) ?? article.Trim();
        return firstBlock.Length <= 180 ? firstBlock : firstBlock[..180];
    }

    private static string ComposeMainArticle(SaleContentResult result)
    {
        var sections = new List<string>();
        AddSection(sections, result.Headline);
        AddSection(sections, result.Opening);

        foreach (var block in result.ContentBlocks)
        {
            var blockLines = new List<string>();
            AddSection(blockLines, block.Title);

            if (block.Type.Equals("highlights", StringComparison.OrdinalIgnoreCase) && block.Items.Count > 0)
            {
                blockLines.AddRange(block.Items.Select(FormatHighlightItem).Where(item => !string.IsNullOrWhiteSpace(item)));
            }
            else
            {
                AddSection(blockLines, block.Text);
                if (block.Items.Count > 0)
                {
                    blockLines.AddRange(block.Items.Select(FormatHighlightItem).Where(item => !string.IsNullOrWhiteSpace(item)));
                }
            }

            AddSection(sections, string.Join(Environment.NewLine, blockLines.Where(line => !string.IsNullOrWhiteSpace(line))));
        }

        AddSection(sections, FormatCallToAction(result.CallToAction));

        var contactLines = new List<string>();
        AddSection(contactLines, result.Contact.ShopName);
        AddSection(contactLines, result.Contact.Phone);
        AddSection(contactLines, result.Contact.Address);
        AddSection(contactLines, result.Contact.Website);
        AddSection(sections, string.Join(Environment.NewLine, contactLines));

        if (result.Hashtags.Count > 0)
        {
            AddSection(sections, string.Join(" ", result.Hashtags));
        }

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections.Where(section => !string.IsNullOrWhiteSpace(section)));
    }

    private static void AddSection(List<string> sections, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sections.Add(value.Trim());
        }
    }

    private static string FormatHighlightItem(HighlightItem item)
    {
        var icon = CleanInline(item.Icon);
        var title = CleanInline(item.BenefitTitle);
        var text = CleanText(item.Text);
        return string.Join(" ", new[] { icon, title }.Where(part => !string.IsNullOrWhiteSpace(part))) +
            (string.IsNullOrWhiteSpace(text) ? "" : $" — {text}");
    }

    private static string FormatCallToAction(CallToActionContent callToAction)
    {
        var icon = CleanInline(callToAction.Icon);
        var text = CleanText(callToAction.Text);
        return string.Join(" ", new[] { icon, text }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private string? GetCredential()
    {
        var credential = configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        return string.IsNullOrWhiteSpace(credential) ? null : credential.Trim();
    }

    private string GetGeminiModel()
    {
        var model = configuration["Gemini:Model"] ?? Environment.GetEnvironmentVariable("GEMINI_MODEL");
        return string.IsNullOrWhiteSpace(model) ? DefaultGeminiModel : model.Trim();
    }

    private IReadOnlyList<string> GetGeminiModelCandidates()
    {
        var primary = GetGeminiModel();
        var allowFallbacks = configuration.GetValue<bool?>("Gemini:AllowModelFallbacks") ??
            bool.TryParse(Environment.GetEnvironmentVariable("GEMINI_ALLOW_MODEL_FALLBACKS"), out var envAllowFallbacks) && envAllowFallbacks;
        if (!allowFallbacks)
        {
            return [primary];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var models = new List<string>();

        void AddModel(string value)
        {
            var clean = value.Trim();
            if (!string.IsNullOrWhiteSpace(clean) && seen.Add(clean))
            {
                models.Add(clean);
            }
        }

        AddModel(primary);
        foreach (var fallback in GeminiFallbackModels)
        {
            AddModel(fallback);
        }

        return models;
    }

    private async Task<ImageReadResult> ReadValidatedImageAsync(IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length <= 0)
        {
            return ImageReadResult.Fail(400, "Vui lòng tải ảnh sản phẩm để AI nhận diện.");
        }

        if (image.Length > MaxImageBytes)
        {
            return ImageReadResult.Fail(400, "Ảnh không hợp lệ. Chỉ nhận JPG, PNG, WebP dưới 8MB.");
        }

        await using var stream = image.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var mimeType = DetectMimeType(bytes);

        if (mimeType is null)
        {
            return ImageReadResult.Fail(400, "Ảnh không hợp lệ. Chỉ nhận JPG, PNG hoặc WebP.");
        }

        return ImageReadResult.Ok(new ValidatedImage(mimeType, Convert.ToBase64String(bytes)));
    }

    private static string? DetectMimeType(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        return null;
    }

    private async Task<GeminiCallResult> SendGeminiAsync(
        object body,
        string credential,
        bool retryJson,
        CancellationToken cancellationToken,
        bool allowTransientRetries = true,
        TimeSpan? requestTimeout = null)
    {
        var attempts = retryJson ? 2 : 1;
        GeminiCallResult? lastResult = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var requestBody = attempt == 1
                ? body
                : AddJsonRepairInstruction(body);

            lastResult = await SendGeminiWithTransientRetriesAsync(requestBody, credential, cancellationToken, allowTransientRetries, requestTimeout);
            if (!lastResult.Success)
            {
                return lastResult;
            }

            if (LooksLikeJson(lastResult.Text))
            {
                return lastResult;
            }
        }

        return lastResult ?? GeminiCallResult.Fail(502, "Gemini chưa trả về dữ liệu hợp lệ.", "");
    }

    private async Task<GeminiCallResult> SendGeminiWithTransientRetriesAsync(
        object body,
        string credential,
        CancellationToken cancellationToken,
        bool allowTransientRetries,
        TimeSpan? requestTimeout)
    {
        var delays = allowTransientRetries
            ? new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) }
            : new[] { TimeSpan.Zero };
        var models = GetGeminiModelCandidates();
        var usesGoogleSearch = RequestUsesGoogleSearch(body);
        GeminiCallResult? lastModelResult = null;
        if (usesGoogleSearch && IsGeminiSearchCircuitOpen(out var retryAfter))
        {
            logger.LogWarning(
                "[CONTENT] Gemini Search circuit breaker đang mở, trả HTTP 429 ngay. RetryAfterSeconds={RetryAfterSeconds}",
                retryAfter);
            throw new GeminiQuotaExceededException(
                "Gemini Search hiện đã vượt hạn mức. Vui lòng kiểm tra quota hoặc thử lại sau.",
                retryAfter);
        }

        foreach (var model in models)
        {
            for (var attempt = 0; attempt < delays.Length; attempt++)
            {
                if (delays[attempt] > TimeSpan.Zero)
                {
                    await Task.Delay(delays[attempt], cancellationToken);
                }

                await WaitForGeminiTurnAsync(model, usesGoogleSearch, cancellationToken);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(requestTimeout ?? TimeSpan.FromSeconds(75));

                try
                {
                    using var message = CreateGeminiRequestMessage(credential, model);
                    message.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

                    using var response = await httpClient.SendAsync(message, timeoutCts.Token);
                    var raw = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        return GeminiCallResult.Ok(ExtractResponseText(raw), raw);
                    }

                    var status = (int)response.StatusCode;
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        var detail = ExtractGeminiError(raw);
                        if (GeminiRateLimitPolicy.IsQuotaExceeded(raw, detail))
                        {
                            if (usesGoogleSearch)
                            {
                                OpenGeminiSearchCircuitBreaker();
                            }

                            logger.LogWarning(
                                "[CONTENT] Gemini Search quota exceeded. Model={Model}; HTTP={Status}; DùngGoogleSearch={UsesGoogleSearch}; ChiTiet={Detail}",
                                model,
                                status,
                                usesGoogleSearch,
                                ToUserFacingOfficialUrlDiagnostic(detail));
                            if (usesGoogleSearch)
                            {
                                throw new GeminiQuotaExceededException(
                                    "Gemini Search hiện đã vượt hạn mức. Vui lòng kiểm tra quota hoặc thử lại sau.");
                            }

                            return GeminiCallResult.Fail(
                                429,
                                "Gemini API đã hết hạn mức. Vui lòng kiểm tra Usage, Rate limits và Billing của project.",
                                raw);
                        }

                        var retryDelay = GeminiRateLimitPolicy.TryGetShortRetryDelay(response, raw);
                        lastModelResult = GeminiCallResult.Fail(
                            429,
                            BuildGeminiErrorMessage(raw, status, model));

                        if (retryDelay is { } delay && attempt == 0 && allowTransientRetries)
                        {
                            logger.LogWarning(
                                "[CONTENT] Gemini bị giới hạn theo phút, retry đúng 1 lần. Model={Model}; HTTP={Status}; Lần={Attempt}/{MaxAttempts}; DùngGoogleSearch={UsesGoogleSearch}; Chờ={RetryAfterSeconds}s; ChiTiet={Detail}",
                                model,
                                status,
                                1,
                                2,
                                usesGoogleSearch,
                                Math.Ceiling(delay.TotalSeconds),
                                ToUserFacingOfficialUrlDiagnostic(detail));
                            await Task.Delay(delay, cancellationToken);
                            continue;
                        }

                        logger.LogWarning(
                            "[CONTENT] Gemini bị giới hạn nhưng không có thời gian retry ngắn, backend dừng ngay. Model={Model}; HTTP={Status}; Lần={Attempt}/{MaxAttempts}; DùngGoogleSearch={UsesGoogleSearch}; ChiTiet={Detail}",
                            model,
                            status,
                            attempt + 1,
                            1,
                            usesGoogleSearch,
                            ToUserFacingOfficialUrlDiagnostic(detail));
                        return lastModelResult;
                    }

                    if (response.StatusCode == HttpStatusCode.NotFound && !model.Equals(models[^1], StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning(
                            "[CONTENT] Model Gemini hiện tại chưa khả dụng, thử model dự phòng. Model={Model}; HTTP={Status}; ModelDuPhong={NextModels}; ChiTiet={Detail}",
                            model,
                            status,
                            string.Join(", ", models.SkipWhile(item => !item.Equals(model, StringComparison.OrdinalIgnoreCase)).Skip(1)),
                            ToUserFacingOfficialUrlDiagnostic(ExtractGeminiError(raw)));
                        break;
                    }

                    if (IsTransient(response.StatusCode) && attempt < delays.Length - 1)
                    {
                        logger.LogWarning(
                            "[CONTENT] Gemini lỗi tạm thời, backend retry theo giới hạn. Model={Model}; HTTP={Status}; Lần={Attempt}/{MaxAttempts}; DùngGoogleSearch={UsesGoogleSearch}; ChiTiet={Detail}",
                            model,
                            status,
                            attempt + 1,
                            delays.Length,
                            usesGoogleSearch,
                            ToUserFacingOfficialUrlDiagnostic(ExtractGeminiError(raw)));
                        continue;
                    }

                    var mappedStatus = MapStatusCode(status);
                    var errorMessage = BuildGeminiErrorMessage(raw, status, model);
                    logger.LogWarning(
                        "[CONTENT] Gọi Gemini thất bại. Model={Model}; HTTP={Status}; HTTPTraVe={MappedStatus}; Lần={Attempt}/{MaxAttempts}; DùngGoogleSearch={UsesGoogleSearch}; ChiTiet={Detail}",
                        model,
                        status,
                        mappedStatus,
                        attempt + 1,
                        delays.Length,
                        usesGoogleSearch,
                        ToUserFacingOfficialUrlDiagnostic(ExtractGeminiError(raw)));
                    return GeminiCallResult.Fail(mappedStatus, errorMessage);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(
                        "[CONTENT] Gọi Gemini quá thời gian. Model={Model}; Lần={Attempt}/{MaxAttempts}; DùngGoogleSearch={UsesGoogleSearch}",
                        model,
                        attempt + 1,
                        delays.Length,
                        usesGoogleSearch);
                    return GeminiCallResult.Fail(504, "Yêu cầu Gemini mất quá nhiều thời gian, vui lòng thử lại.");
                }
                catch (GeminiQuotaExceededException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "[CONTENT] Không kết nối được Gemini API. Model={Model}; Lần={Attempt}/{MaxAttempts}; DùngGoogleSearch={UsesGoogleSearch}",
                        model,
                        attempt + 1,
                        delays.Length,
                        usesGoogleSearch);
                    return GeminiCallResult.Fail(503, "Không kết nối được Gemini API. Hãy kiểm tra mạng và thử lại.");
                }
            }
        }

        return lastModelResult ?? GeminiCallResult.Fail(503, $"Gemini vẫn đang bận sau nhiều lần thử với các model: {string.Join(", ", models)}. Nếu lượt này có Google Search, backend sẽ tự fallback khi có thể; vui lòng đợi khoảng 1 phút rồi thử lại.");
    }

    private async Task WaitForGeminiTurnAsync(string model, bool usesGoogleSearch, CancellationToken cancellationToken)
    {
        TimeSpan wait;
        lock (GeminiCooldownLock)
        {
            var now = DateTimeOffset.UtcNow;
            var earliest = GeminiNextRequestAt;
            wait = earliest - now;
            var reservedAt = wait > TimeSpan.Zero ? earliest : now;
            GeminiNextRequestAt = reservedAt.Add(GeminiMinCallSpacing);
        }

        if (wait <= TimeSpan.Zero)
        {
            return;
        }

        logger.LogInformation(
            "[CONTENT] Đang chờ lượt gọi Gemini. Model={Model}; DùngGoogleSearch={UsesGoogleSearch}; Chờ={WaitSeconds}s",
            model,
            usesGoogleSearch,
            Math.Ceiling(wait.TotalSeconds));
        await Task.Delay(wait, cancellationToken);
    }

    private static void OpenGeminiSearchCircuitBreaker()
    {
        lock (GeminiSearchCircuitLock)
        {
            var until = DateTimeOffset.UtcNow.Add(GeminiSearchCircuitDuration);
            if (until > GeminiSearchCircuitOpenUntil)
            {
                GeminiSearchCircuitOpenUntil = until;
            }
        }
    }

    private static bool IsGeminiSearchCircuitOpen(out int? retryAfterSeconds)
    {
        lock (GeminiSearchCircuitLock)
        {
            var remaining = GeminiSearchCircuitOpenUntil - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                retryAfterSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
                return true;
            }
        }

        retryAfterSeconds = null;
        return false;
    }

    private static bool RequestUsesGoogleSearch(object body)
    {
        try
        {
            return JsonSerializer.Serialize(body, JsonOptions).Contains("google_search", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static HttpRequestMessage CreateGeminiRequestMessage(string credential, string model)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        message.Headers.Add("x-goog-api-key", credential);
        return message;
    }

    private static object AddJsonRepairInstruction(object body)
    {
        var json = JsonSerializer.SerializeToNode(body, JsonOptions)!.AsObject();
        var contents = json["contents"]?.AsArray();
        var firstParts = contents?[0]?["parts"]?.AsArray();
        firstParts?.Add(new { text = "Lần trả lời trước không parse được JSON. Hãy trả lại đúng JSON thuần theo schema, không markdown, không chữ giải thích." });
        return json;
    }

    private static object AddQualityRewriteInstruction(object body, int attempt)
    {
        var json = JsonSerializer.SerializeToNode(body, JsonOptions)!.AsObject();
        var generationConfig = json["generationConfig"]?.AsObject();
        if (generationConfig is not null)
        {
            generationConfig["temperature"] = 0.42;
            generationConfig["topP"] = 0.78;
        }

        var contents = json["contents"]?.AsArray();
        var firstParts = contents?[0]?["parts"]?.AsArray();
        firstParts?.Add(new
        {
            text = $$"""
            Bản nháp trước bị chặn vì dùng câu quá chung chung hoặc sáo. Hãy viết lại hoàn toàn:
            Đây là vòng tự sửa số {{attempt}}/{{MaxQualityRewriteAttempts}}. Hãy viết gần khách hơn, ít khẩu hiệu hơn.
            - Opening phải nói thẳng khách cần gì, mùi/công dụng gì, hợp dịp nào, vì sao đáng đọc tiếp.
            - Mỗi highlight phải có insight khách hàng + đặc điểm thật + lợi ích mua hàng.
            - Bản sửa phải có mirror moment + reason-to-believe + fit signal: người đọc phải tự thấy gu/hoàn cảnh của mình trong bài.
            - Thêm ngôn ngữ tư vấn khôn ngoan như "hợp nhất khi", "đáng thử nếu", "nàng sẽ thích nếu", "nên chọn nếu", "không hợp nếu..." khi phù hợp.
            - Tránh các cụm: hào nhoáng, tuyên ngôn, khẳng định cá tính, khẳng định bản thân, định hình cá tính, định nghĩa phong thái, bản sắc riêng, phong thái đương đại, tự tin tỏa sáng, dấu ấn riêng, hoa cỏ ban ngày.
            - Không dùng title highlight dạng thuật ngữ như "Nốt hương chiều sâu", "Hương hoa đặc trưng", "Thiết kế ấn tượng", "Tinh dầu oải hương". Title phải là lợi ích khách hiểu ngay.
            - Không dùng "đội ngũ hỗ trợ tư vấn"; CTA/closing phải nói như shop tư vấn trực tiếp, gần và rõ.
            - Với nước hoa, bản sửa phải có ít nhất một câu chọn gu: hợp nếu thích gì và không hợp nếu chỉ thích gì.
            - Nếu bí cách viết, dùng khung này: "Đáng thử nếu nàng thích [gu cụ thể], vì [chi tiết thật] cho cảm giác [dễ hình dung] khi [dịp dùng]."
            - Không dùng câu "vững vàng suốt ngày dài" hoặc các câu tương tự.
            - Note hương khó hiểu phải chuyển thành cảm giác dễ hình dung.
            - Với nước hoa, không dùng highlight riêng để khen thiết kế chai nếu không phục vụ mua quà. Ưu tiên gu mùi, cảm giác xịt, dịp dùng, lý do đáng mua.
            - Viết như tư vấn thử mùi tại shop, không như slogan chiến dịch thương hiệu.
            - ProductNotice phải tạo lý do xuống tiền thật, không viết câu trang trí.
            - Không đổi thông tin sản phẩm, giá, dung tích, phiên bản.
            Chỉ trả JSON đúng schema.
            """
        });
        return json;
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}');
    }

    private static bool TryParseJson<T>(string text, out T? value)
    {
        value = default;
        try
        {
            value = JsonSerializer.Deserialize<T>(CleanJson(text), JsonOptions);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string CleanJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
            {
                trimmed = trimmed[(firstNewLine + 1)..lastFence].Trim();
            }
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace ? trimmed[firstBrace..(lastBrace + 1)] : trimmed;
    }

    private static string ExtractResponseText(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson);
        if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        builder.AppendLine(text.GetString());
                    }
                }
            }

            return builder.ToString().Trim();
        }

        return "";
    }

    private static IReadOnlyList<GroundedSource> ExtractGroundingSources(string rawJson)
    {
        var sources = new List<GroundedSource>();
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                return sources;
            }

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("groundingMetadata", out var metadata) ||
                    !metadata.TryGetProperty("groundingChunks", out var chunks) ||
                    chunks.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var chunk in chunks.EnumerateArray())
                {
                    if (chunk.TryGetProperty("web", out var web))
                    {
                        AddSource(sources, GetString(web, "uri"), GetString(web, "title"));
                    }
                    else if (chunk.TryGetProperty("image", out var image))
                    {
                        AddSource(sources, GetString(image, "sourceUri"), GetString(image, "title"));
                    }
                }
            }
        }
        catch
        {
            return sources;
        }

        return sources
            .GroupBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(8)
            .ToList();
    }

    private static void AddSource(List<GroundedSource> sources, string url, string title)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (IsNonPublicCommerceRoute(uri))
        {
            return;
        }

        sources.Add(new GroundedSource(
            Website: uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase),
            Title: string.IsNullOrWhiteSpace(title) ? uri.Host : title,
            Url: uri.ToString()));
    }

    private static IReadOnlyList<GroundedSource> ExtractSourcesFromResearchText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var sources = new List<GroundedSource>();
        foreach (Match match in Regex.Matches(text, @"https?://[^\s""'<>),]+", RegexOptions.IgnoreCase))
        {
            var url = match.Value.Trim().TrimEnd('.', ',', ';', ':');
            AddSource(sources, url, "URL trang sản phẩm Gemini đề xuất");
        }

        return sources;
    }

    private static IReadOnlyList<GroundedSource> ExtractSourcesFromProductPageUrlLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var sources = new List<GroundedSource>();
        foreach (Match match in Regex.Matches(
            text,
            @"productPageUrl\s*:\s*(?<url>https?://[^\s""'<>),]+)",
            RegexOptions.IgnoreCase))
        {
            var url = match.Groups["url"].Value.Trim().TrimEnd('.', ',', ';', ':');
            AddSource(sources, url, "URL trang sản phẩm chính hãng");
        }

        return sources;
    }

    private static IReadOnlyList<GroundedSource> FilterProductPageSources(
        IEnumerable<GroundedSource> sources,
        ConfirmedProductRequest request,
        string productName)
    {
        var tokens = BuildProductSourceTokens(request, productName);
        var distinctiveTokens = BuildDistinctiveProductSourceTokens(request, productName);
        var concentration = DetectFragranceConcentration($"{productName} {request.Variant} {request.Finish}");
        return (sources ?? [])
            .Where(source => IsLikelyProductPageSource(source, tokens, distinctiveTokens, concentration, request.Brand, request.Category, productName, request.Size))
            .GroupBy(source => source.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(8)
            .ToArray();
    }

    private static bool IsLikelyProductPageSource(
        GroundedSource source,
        IReadOnlyList<string> tokens,
        IReadOnlyList<string> distinctiveTokens,
        string concentration,
        string brand,
        string category,
        string productName,
        string size)
    {
        if (!Uri.TryCreate(source.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (IsNonPublicCommerceRoute(uri))
        {
            return false;
        }

        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').ToLowerInvariant();
        if (BeautySourceRegistry.BlockedDomains.Any(blocked => host.Contains(blocked, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (host is "google.com" or "google.com.vn" || string.IsNullOrWhiteSpace(path) || path.Length < 8)
        {
            return false;
        }

        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pathSegments.Length == 0)
        {
            return false;
        }

        var lastSegment = pathSegments[^1];
        if (lastSegment is "home" or "homepage" or "shop" or "products" or "product" or "collections" or "fragrance" or "perfume" or "makeup" or "skincare" or "eyes" or "eye" or "eyeshadow" or "eyeshadows" or "lips" or "lip" or "face")
        {
            return false;
        }

        var haystack = RemoveVietnameseDiacritics($"{host} {path} {source.Title}").ToLowerInvariant();
        var productHint = RemoveVietnameseDiacritics($"{brand} {string.Join(" ", tokens)} {string.Join(" ", distinctiveTokens)}").ToLowerInvariant();
        if (HasConflictingCategoryInSource(haystack, category, $"{productName} {productHint}"))
        {
            return false;
        }

        if (!FragranceConcentrationMatches(haystack, concentration))
        {
            return false;
        }

        if (!ProductSizeMatchesSource(haystack, size))
        {
            return false;
        }

        var isAcceptableHost = IsAcceptableProductHost(host, brand);
        if (!isAcceptableHost)
        {
            return false;
        }

        var hasDistinctiveTokenMatch = DistinctiveProductTokensMatch(haystack, distinctiveTokens);
        var matchCount = tokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        var brandTokens = RemoveVietnameseDiacritics(brand)
            .ToLowerInvariant()
            .Split([' ', '-', '_', '/', '.', '\'', '"', '’', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var genericLikelyTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "official", "product", "beauty", "makeup", "skincare", "fragrance", "perfume",
            "eau", "parfum", "toilette", "spray", "natural", "vaporisateur", "nuoc", "hoa",
            "lip", "lips", "son", "balm", "gloss", "oil", "cream", "serum"
        };
        var productSpecificTokens = tokens
            .Where(token => !brandTokens.Contains(token))
            .Where(token => !genericLikelyTokens.Contains(token))
            .ToArray();
        var productSpecificMatchCount = productSpecificTokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        var hasProductDetailPath = pathSegments.Any(segment =>
            segment.Contains("product", StringComparison.OrdinalIgnoreCase) ||
            segment.Contains("p-", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(segment, @"\bY\d{5,}\b", RegexOptions.IgnoreCase) ||
            segment.Contains("sku", StringComparison.OrdinalIgnoreCase)) ||
            pathSegments.Length >= 2;

        if (distinctiveTokens.Count > 0)
        {
            return hasDistinctiveTokenMatch || (hasProductDetailPath && productSpecificMatchCount >= 1);
        }

        return hasProductDetailPath &&
            (productSpecificTokens.Length == 0 || productSpecificMatchCount >= 1) &&
            matchCount >= Math.Min(1, tokens.Count);
    }

    private static bool IsOfficialBrandHost(string host, string brand)
    {
        var normalizedHost = host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        var officialDomains = BuildOfficialDomainSearchHints(brand);
        foreach (var officialDomain in officialDomains)
        {
            var normalizedDomain = officialDomain.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
            if (normalizedHost.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase) ||
                normalizedHost.EndsWith("." + normalizedDomain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return HostLooksLikeOfficialBrand(normalizedHost, brand);
    }

    private static bool IsAcceptableProductHost(string host, string brand) =>
        IsOfficialBrandHost(host, brand) || IsTrustedProductPageHost(host);

    private static bool IsKnownOfficialOrTrustedProductHost(string host) =>
        IsRegisteredOfficialBrandHost(host) || IsTrustedProductPageHost(host);

    private static bool IsRegisteredOfficialBrandHost(string host)
    {
        var normalizedHost = host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        foreach (var domain in BrandOfficialRegistry.SelectMany(profile => profile.Domains))
        {
            var normalizedDomain = domain.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
            if (normalizedHost.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase) ||
                normalizedHost.EndsWith("." + normalizedDomain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrustedProductPageHost(string host)
    {
        var normalizedHost = host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        return TrustedProductPageDomains.Any(domain =>
            normalizedHost.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
            normalizedHost.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HostLooksLikeOfficialBrand(string host, string brand)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(brand))
        {
            return false;
        }

        var compactHost = Regex.Replace(RemoveVietnameseDiacritics(host).ToLowerInvariant(), @"[^a-z0-9]", "");
        var normalizedBrand = RemoveVietnameseDiacritics(brand).ToLowerInvariant();
        var compactNormalizedBrand = Regex.Replace(normalizedBrand, @"[^a-z0-9]", "");
        if (compactNormalizedBrand.Length >= 3 && compactHost.Contains(compactNormalizedBrand, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((normalizedBrand.Contains("yves saint laurent", StringComparison.OrdinalIgnoreCase) ||
                normalizedBrand.Contains("ysl", StringComparison.OrdinalIgnoreCase)) &&
            (compactHost.Contains("yslbeauty", StringComparison.OrdinalIgnoreCase) ||
                compactHost.Equals("yslcom", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var brandTokens = RemoveVietnameseDiacritics(brand)
            .ToLowerInvariant()
            .Split([' ', '-', '_', '.', '\'', '’', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .Where(token => token is not "the" and not "and" and not "beauty" and not "paris" and not "new" and not "york")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (brandTokens.Length == 0)
        {
            return false;
        }

        var compactBrand = string.Concat(brandTokens);
        if (compactBrand.Length >= 5 && compactHost.Contains(compactBrand, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var matchedTokens = brandTokens.Count(token => compactHost.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (brandTokens.Length == 1)
        {
            return matchedTokens == 1;
        }

        return matchedTokens >= Math.Min(2, brandTokens.Length);
    }

    private static string DetectFragranceConcentration(string value)
    {
        var normalized = RemoveVietnameseDiacritics(value).ToLowerInvariant();
        if (Regex.IsMatch(normalized, @"\b(eau\s+de\s+parfum|edp)\b"))
        {
            return "edp";
        }

        if (Regex.IsMatch(normalized, @"\b(eau\s+de\s+toilette|edt)\b"))
        {
            return "edt";
        }

        if (Regex.IsMatch(normalized, @"\b(le\s+parfum|parfum)\b"))
        {
            return "parfum";
        }

        return "";
    }

    private static bool FragranceConcentrationMatches(string haystack, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var hasEdt = haystack.Contains("eau-de-toilette") || haystack.Contains("eau de toilette") || Regex.IsMatch(haystack, @"\bedt\b");
        var hasEdp = haystack.Contains("eau-de-parfum") || haystack.Contains("eau de parfum") || Regex.IsMatch(haystack, @"\bedp\b");
        var hasLeParfum = haystack.Contains("le-parfum") || haystack.Contains("le parfum");

        return expected switch
        {
            "edp" => hasEdp || (!hasEdt && !hasLeParfum),
            "edt" => hasEdt || (!hasEdp && !hasLeParfum),
            "parfum" => hasLeParfum || (!hasEdt && !hasEdp),
            _ => true
        };
    }

    private static bool OfficialSourceMatchesProduct(
        Uri uri,
        string title,
        string content,
        ConfirmedProductRequest request,
        string productName)
    {
        var host = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        if (!IsAcceptableProductHost(host, request.Brand) &&
            !(IsImageFirstBlankRequest(request) && IsKnownOfficialOrTrustedProductHost(host)))
        {
            return false;
        }

        if (IsImageFirstBlankRequest(request))
        {
            return !ProductMatchScorerLooksLikeContainer(uri);
        }

        var tokens = BuildProductSourceTokens(request, productName);
        var distinctiveTokens = BuildDistinctiveProductSourceTokens(request, productName);
        var concentration = DetectFragranceConcentration($"{productName} {request.Variant} {request.Finish}");
        var haystack = RemoveVietnameseDiacritics($"{uri.Host} {uri.AbsolutePath} {title} {content[..Math.Min(content.Length, 2500)]}").ToLowerInvariant();
        if (HasConflictingCategoryInSource(haystack, request.Category, productName))
        {
            return false;
        }

        if (!FragranceConcentrationMatches(haystack, concentration))
        {
            return false;
        }

        if (!ProductSizeMatchesSource(haystack, request.Size))
        {
            return false;
        }

        if (!DistinctiveProductTokensMatch(haystack, distinctiveTokens))
        {
            return false;
        }

        if (tokens.Count == 0)
        {
            return true;
        }

        var matchCount = tokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        return matchCount >= Math.Min(2, tokens.Count);
    }

    private static bool DistinctiveProductTokensMatch(string haystack, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return false;
        }

        var matchCount = tokens.Count(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        var required = tokens.Count <= 2 ? tokens.Count : Math.Max(2, tokens.Count - 1);
        return matchCount >= required;
    }

    private static bool HasConflictingCategoryInSource(string haystack, string category, string productName)
    {
        var normalizedCategory = RemoveVietnameseDiacritics($"{category} {productName}").ToLowerInvariant();
        var expectsLipMakeup = Regex.IsMatch(normalizedCategory, @"\b(lipstick|lip\s+gloss|lip\s+balm|son|moi)\b", RegexOptions.IgnoreCase);
        if (expectsLipMakeup &&
            (haystack.Contains("poison-girl", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("poison girl", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("dior-paradise", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("eau-de-parfum", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("eau de parfum", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("/fragrance/", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("/perfume/", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("perfume", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (normalizedCategory.Contains("dior", StringComparison.OrdinalIgnoreCase) &&
            normalizedCategory.Contains("lip", StringComparison.OrdinalIgnoreCase) &&
            normalizedCategory.Contains("gloss", StringComparison.OrdinalIgnoreCase) &&
            !normalizedCategory.Contains("glow", StringComparison.OrdinalIgnoreCase) &&
            (haystack.Contains("lip-glow-oil", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("lip glow oil", StringComparison.OrdinalIgnoreCase) ||
                (Regex.IsMatch(haystack, @"\blip[-\s]+glow\b") && !haystack.Contains("maximizer", StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        if (normalizedCategory.Contains("lip glow", StringComparison.OrdinalIgnoreCase) &&
            !normalizedCategory.Contains("oil", StringComparison.OrdinalIgnoreCase) &&
            (haystack.Contains("lip-glow-oil", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("lip glow oil", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("lip-maximizer", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("lip maximizer", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (normalizedCategory.Contains("lip maximizer", StringComparison.OrdinalIgnoreCase) &&
            (haystack.Contains("lip-glow-oil", StringComparison.OrdinalIgnoreCase) ||
                haystack.Contains("lip glow oil", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(haystack, @"\blip[-\s]+glow\b") && !haystack.Contains("maximizer", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var expectsFragrance = normalizedCategory.Contains("nuoc hoa", StringComparison.OrdinalIgnoreCase) ||
            normalizedCategory.Contains("fragrance", StringComparison.OrdinalIgnoreCase) ||
            normalizedCategory.Contains("perfume", StringComparison.OrdinalIgnoreCase) ||
            normalizedCategory.Contains("eau de parfum", StringComparison.OrdinalIgnoreCase) ||
            normalizedCategory.Contains("eau de toilette", StringComparison.OrdinalIgnoreCase);
        if (!expectsFragrance)
        {
            return false;
        }

        return haystack.Contains("foundation", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("cushion", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("/makeup/face/", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("concealer", StringComparison.OrdinalIgnoreCase) ||
            haystack.Contains("powder", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasConflictingProductVariantToken(string haystack, IReadOnlyCollection<string> productTokens)
    {
        var variants = new[]
        {
            "mademoiselle", "noir", "chance", "tendre", "fraiche", "fiori", "intense",
            "absolu", "absolue", "elixir", "homme", "sport"
        };

        return variants.Any(token =>
            haystack.Contains(token, StringComparison.OrdinalIgnoreCase) &&
            !productTokens.Contains(token, StringComparer.OrdinalIgnoreCase));
    }

    private static bool ProductSizeMatchesSource(string haystack, string expectedSize)
    {
        var expectedSizes = ExtractProductSizes(expectedSize);
        if (expectedSizes.Count == 0)
        {
            return true;
        }

        var sourceSizes = ExtractProductSizes(haystack);
        if (sourceSizes.Count == 0)
        {
            return true;
        }

        foreach (var expected in expectedSizes)
        {
            var comparableSourceSizes = sourceSizes
                .Where(source => string.Equals(source.Unit, expected.Unit, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (comparableSourceSizes.Length > 0 &&
                comparableSourceSizes.All(source => Math.Abs(source.Amount - expected.Amount) > 0.01m))
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct ProductSizeToken(decimal Amount, string Unit);

    private static IReadOnlyList<ProductSizeToken> ExtractProductSizes(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var normalized = RemoveVietnameseDiacritics(value).ToLowerInvariant();
        var sizes = new List<ProductSizeToken>();
        foreach (Match match in Regex.Matches(
            normalized,
            @"(?<amount>\d{1,4}(?:[\.,]\d+)?)\s*-?\s*(?<unit>fl\.?\s*oz\.?|ml|g|gr|gram)\b",
            RegexOptions.IgnoreCase))
        {
            var amountText = match.Groups["amount"].Value.Replace(',', '.');
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                continue;
            }

            var unit = match.Groups["unit"].Value
                .Replace(".", "", StringComparison.Ordinal)
                .Replace(" ", "", StringComparison.Ordinal);
            unit = unit switch
            {
                "gram" or "gr" => "g",
                "floz" => "floz",
                _ => unit
            };
            sizes.Add(new ProductSizeToken(amount, unit));
        }

        return sizes
            .GroupBy(size => $"{size.Amount:0.###}{size.Unit}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<string> BuildProductSourceTokens(ConfirmedProductRequest request, string productName)
    {
        var raw = string.Join(" ", new[] { productName, request.Brand, request.Variant, request.Shade, request.Size }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var normalizedRaw = RemoveVietnameseDiacritics(raw).ToLowerInvariant();
        var tokens = normalizedRaw
            .Split([' ', '-', '_', '/', '.', '\'', '"', '’'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .Where(token => token is not "eau" and not "parfum" and not "spray" and not "the" and not "official" and not "product" and not "paris")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        if (normalizedRaw.Contains("yves saint laurent") && !tokens.Contains("ysl", StringComparer.OrdinalIgnoreCase))
        {
            tokens.Add("ysl");
        }

        return tokens.ToArray();
    }

    private static IReadOnlyList<string> BuildDistinctiveProductSourceTokens(ConfirmedProductRequest request, string productName)
    {
        var brandTokens = BuildProductSourceTokens(
                request with { Variant = "", Shade = "", Size = "", Finish = "" },
                request.Brand)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var raw = string.Join(" ", new[] { productName, request.Variant, request.Shade }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var genericTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "eau", "de", "du", "la", "le", "les", "des", "d", "l", "parfum", "perfume", "spray",
            "toilette", "cologne", "edp", "edt", "ml", "fl", "oz", "for", "her", "him", "women",
            "men", "woman", "man", "fragrance", "official", "product", "beauty", "natural",
            "vaporisateur", "vaporizateur", "nước", "nuoc", "hoa", "paris"
        };

        var tokens = RemoveVietnameseDiacritics(raw)
            .ToLowerInvariant()
            .Split([' ', '-', '_', '/', '.', '\'', '"', '’'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3)
            .Where(token => !Regex.IsMatch(token, @"^\d+(ml)?$"))
            .Where(token => !genericTokens.Contains(token))
            .Where(token => !brandTokens.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();

        return tokens;
    }

    private static string GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string NormalizeItemForm(string itemForm, string category, string productName)
    {
        var normalized = RemoveVietnameseDiacritics($"{itemForm} {category} {productName}")
            .ToLowerInvariant();

        if (normalized.Contains("case") ||
            normalized.Contains("casing") ||
            normalized.Contains("vo son") ||
            normalized.Contains("vo hop") ||
            normalized.Contains("fashion case"))
        {
            return "case";
        }

        if (normalized.Contains("refill") ||
            normalized.Contains("loi son") ||
            normalized.Contains("loi thay") ||
            normalized.Contains("ruot son"))
        {
            return "refill";
        }

        if (normalized.Contains("accessory") ||
            normalized.Contains("phu kien") ||
            normalized.Contains("holder"))
        {
            return "accessory";
        }

        if (normalized.Contains("unknown") ||
            normalized.Contains("khong ro"))
        {
            return "unknown";
        }

        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : "full-product";
    }

    private static ProductIdentificationResult NormalizeIdentification(ProductIdentificationResult result)
    {
        var confidence = Math.Clamp(result.Confidence, 0, 100);
        var productName = CleanIdentificationField(result.ProductName);
        var brand = NormalizeOfficialBrandName(CleanIdentificationField(result.Brand));
        var productLine = CleanIdentificationField(result.ProductLine);
        var variant = CleanIdentificationField(result.Variant);
        var shade = CleanIdentificationField(result.Shade);
        var finish = CleanIdentificationField(result.Finish);
        var category = CleanIdentificationField(result.Category);
        var itemForm = NormalizeItemForm(result.ItemForm, category, productName);
        var size = CleanIdentificationField(result.Size);
        var visibleText = (result.VisibleText ?? [])
            .Select(CleanIdentificationField)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
        var searchQuery = CleanIdentificationField(result.SearchQuery);
        var correction = BuildVisibleTextCorrection(productName, brand, variant, category, size, visibleText);
        if (correction.HasCorrection)
        {
            productName = correction.ProductName;
            brand = correction.Brand;
            variant = correction.Variant;
            category = correction.Category;
            size = correction.Size;
            searchQuery = "";
            confidence = Math.Max(confidence, 92);
        }
        if (IsOnlyBrandLogoRecognition(productName, brand, variant, shade, visibleText))
        {
            productName = "";
            variant = "";
            shade = "";
            searchQuery = "";
            confidence = Math.Min(confidence, 60);
        }
        if (IsBrandWithOnlyGenericPlaceText(productName, brand))
        {
            productName = "";
            productLine = "";
            variant = "";
            searchQuery = "";
            confidence = Math.Min(confidence, 65);
        }
        var ocrMismatch = !correction.HasCorrection && HasVisibleTextMismatch(productName, variant, visibleText);

        var looksTooGeneric =
            string.IsNullOrWhiteSpace(productName) ||
            productName.Equals(brand, StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(productName, @"^(sản phẩm|san pham|mỹ phẩm|my pham|son|lipstick|case|vỏ son|vo son)\s+\w{0,20}$", RegexOptions.IgnoreCase);
        var hasUsableIdentity = !string.IsNullOrWhiteSpace(brand) &&
            (!string.IsNullOrWhiteSpace(productName) ||
                !string.IsNullOrWhiteSpace(productLine) ||
                visibleText.Any(text => !string.Equals(text, brand, StringComparison.OrdinalIgnoreCase)));
        var hasItemFormConflict =
            (itemForm is "case" && Regex.IsMatch($"{category} {productName}", @"\b(refill|lõi|loi|lipstick|son hoàn chỉnh|son hoan chinh)\b", RegexOptions.IgnoreCase)) ||
            (itemForm is "refill" && Regex.IsMatch($"{category} {productName}", @"\b(case|vỏ|vo|full-product|son hoàn chỉnh|son hoan chinh)\b", RegexOptions.IgnoreCase));
        var needsConfirmation = !correction.HasCorrection &&
            (!hasUsableIdentity || hasItemFormConflict || (result.NeedsConfirmation && looksTooGeneric));
        var message = needsConfirmation
            ? hasItemFormConflict
                ? "AI chưa chắc đây là vỏ/lõi hay sản phẩm hoàn chỉnh. Vui lòng xác nhận đúng loại sản phẩm trước."
                : string.IsNullOrWhiteSpace(productName) && string.IsNullOrWhiteSpace(productLine)
                    ? "Ảnh chỉ đọc được thương hiệu/logo, chưa thấy tên dòng hoặc mã màu đủ chắc để tìm URL chính hãng."
                    : "AI chưa nhận diện chắc chắn sản phẩm. Vui lòng xác nhận tên sản phẩm hoặc tải ảnh rõ hơn."
            : correction.HasCorrection
                ? $"Đã nhận diện theo chữ trên bao bì: {productName}."
                : result.Message;

        return result with
        {
            ProductName = productName,
            Brand = brand,
            ProductLine = productLine,
            Variant = variant,
            Shade = shade,
            Finish = finish,
            Category = category,
            ItemForm = itemForm,
            Size = size,
            VisibleText = visibleText,
            Confidence = confidence,
            SearchQuery = string.IsNullOrWhiteSpace(searchQuery) ? BuildIdentificationSearchQuery(productName, brand, category, variant, shade) : searchQuery,
            NeedsConfirmation = needsConfirmation,
            Message = CleanText(message)
        };
    }

    private static VisibleTextCorrection BuildVisibleTextCorrection(
        string productName,
        string brand,
        string variant,
        string category,
        string size,
        IReadOnlyList<string> visibleText)
    {
        var joined = string.Join(" ", visibleText);
        var normalized = NormalizeOcrText(joined);
        var correctedBrand = brand;
        var correctedVariant = variant;
        var correctedCategory = category;
        var correctedSize = size;
        var correctedProductName = productName;
        var hasCorrection = false;

        if (IsOnlyBrandLogoRecognition(productName, brand, variant, "", visibleText))
        {
            return new VisibleTextCorrection(false, "", brand, "", category, size);
        }

        if (Regex.IsMatch(normalized, @"\bGUCCI\b"))
        {
            correctedBrand = "Gucci";
        }

        if (Regex.IsMatch(normalized, @"\bBLOOM\b") && Regex.IsMatch(normalized, @"\bAMBROSIA\s+D\s*ORO\b"))
        {
            correctedVariant = "Ambrosia D'Oro";
            correctedCategory = string.IsNullOrWhiteSpace(correctedCategory) ? "Nước hoa" : correctedCategory;
            hasCorrection = true;
        }
        else if (Regex.IsMatch(normalized, @"\bBLOOM\b") && Regex.IsMatch(normalized, @"\bAMBROSIA\s+DI\s+FIORI\b"))
        {
            correctedVariant = "Ambrosia Di Fiori";
            correctedCategory = string.IsNullOrWhiteSpace(correctedCategory) ? "Nước hoa" : correctedCategory;
            hasCorrection = true;
        }
        else if (Regex.IsMatch(normalized, @"\bCREED\b") && Regex.IsMatch(normalized, @"\bABSOLU\s+AVENTUS\b"))
        {
            correctedBrand = "Creed";
            correctedVariant = "Absolu Aventus";
            correctedCategory = string.IsNullOrWhiteSpace(correctedCategory) ? "Nước hoa" : correctedCategory;
            hasCorrection = true;
        }
        else if (Regex.IsMatch(normalized, @"\bLIBRE\b") && Regex.IsMatch(normalized, @"\bBERRY\s+CRUSH\b"))
        {
            correctedBrand = "Yves Saint Laurent";
            correctedVariant = "Berry Crush";
            correctedCategory = "Nước hoa";
            hasCorrection = true;
        }
        else if ((Regex.IsMatch(normalized, @"\bDIOR\b") || Regex.IsMatch(normalized, @"\bCD\b")) &&
            Regex.IsMatch(normalized, @"\bLIP\s+GLOW\s+OIL\b"))
        {
            correctedBrand = "Dior";
            correctedVariant = "Dior Addict";
            correctedCategory = "Lip oil";
            hasCorrection = true;
        }
        else if ((Regex.IsMatch(normalized, @"\bDIOR\b") || Regex.IsMatch(normalized, @"\bCD\b")) &&
            Regex.IsMatch(normalized, @"\bLIP\s+GLOW\b"))
        {
            correctedBrand = "Dior";
            correctedVariant = "Lip Glow";
            correctedCategory = "Son dưỡng có màu";
            hasCorrection = true;
        }
        else if (Regex.IsMatch(normalized, @"\bDIOR\s+ADDICT\b") &&
            (Regex.IsMatch(normalized, @"\bSHINE\s+LIPSTICK\b") ||
                Regex.IsMatch(normalized, @"\bHYDRATING\s+SHINE\b") ||
                Regex.IsMatch(normalized, @"\bROUGE\s+BRILLANT\b")))
        {
            correctedBrand = "Dior";
            correctedVariant = "Dior Addict";
            correctedCategory = "Lipstick";
            hasCorrection = true;
        }

        if (Regex.IsMatch(normalized, @"\bEAU\s+DE\s+PARFUM\b") && string.IsNullOrWhiteSpace(correctedCategory))
        {
            correctedCategory = "Nước hoa";
        }

        var sizeMatch = Regex.Match(joined, @"(?<size>\d{1,3})\s*(?<unit>ml|mL|ML)\b");
        if (sizeMatch.Success)
        {
            correctedSize = $"{sizeMatch.Groups["size"].Value} ml";
        }

        if (!hasCorrection)
        {
            var productSupported = IsFieldSupportedByOcr(productName, normalized);
            var variantSupported = IsFieldSupportedByOcr(variant, normalized);
            var visibleProductName = BuildProductNameFromVisibleText(visibleText, correctedBrand);
            if ((!productSupported || !variantSupported) && !string.IsNullOrWhiteSpace(visibleProductName))
            {
                correctedProductName = visibleProductName;
                correctedVariant = variantSupported ? variant : "";
                hasCorrection = true;
            }
        }

        if (!hasCorrection)
        {
            return new VisibleTextCorrection(false, productName, brand, variant, category, size);
        }

        correctedProductName = BuildCorrectedProductName(correctedProductName, correctedBrand, correctedVariant, normalized);
        return new VisibleTextCorrection(
            true,
            correctedProductName,
            string.IsNullOrWhiteSpace(correctedBrand) ? brand : correctedBrand,
            correctedVariant,
            correctedCategory,
            correctedSize);
    }

    private static string BuildCorrectedProductName(string productName, string brand, string variant, string normalizedVisibleText)
    {
        if (Regex.IsMatch(normalizedVisibleText, @"\bGUCCI\b") && Regex.IsMatch(normalizedVisibleText, @"\bBLOOM\b") && !string.IsNullOrWhiteSpace(variant))
        {
            return $"Gucci Bloom {variant}";
        }

        if (Regex.IsMatch(normalizedVisibleText, @"\bCREED\b") && Regex.IsMatch(normalizedVisibleText, @"\bABSOLU\s+AVENTUS\b"))
        {
            return "Creed Absolu Aventus";
        }

        if (Regex.IsMatch(normalizedVisibleText, @"\bLIBRE\b") && Regex.IsMatch(normalizedVisibleText, @"\bBERRY\s+CRUSH\b"))
        {
            return "Yves Saint Laurent Libre Berry Crush";
        }

        if ((Regex.IsMatch(normalizedVisibleText, @"\bDIOR\b") || Regex.IsMatch(normalizedVisibleText, @"\bCD\b")) &&
            Regex.IsMatch(normalizedVisibleText, @"\bLIP\s+GLOW\s+OIL\b"))
        {
            return "Dior Addict Lip Glow Oil";
        }

        if ((Regex.IsMatch(normalizedVisibleText, @"\bDIOR\b") || Regex.IsMatch(normalizedVisibleText, @"\bCD\b")) &&
            Regex.IsMatch(normalizedVisibleText, @"\bLIP\s+GLOW\b"))
        {
            return "Dior Addict Lip Glow";
        }

        if (Regex.IsMatch(normalizedVisibleText, @"\bDIOR\s+ADDICT\b") &&
            (Regex.IsMatch(normalizedVisibleText, @"\bSHINE\s+LIPSTICK\b") ||
                Regex.IsMatch(normalizedVisibleText, @"\bHYDRATING\s+SHINE\b") ||
                Regex.IsMatch(normalizedVisibleText, @"\bROUGE\s+BRILLANT\b")))
        {
            return "Dior Addict Hydrating Shine Lipstick";
        }

        if (!string.IsNullOrWhiteSpace(productName))
        {
            return IsBrandWithOnlyGenericPlaceText(productName, brand) ? "" : productName;
        }

        if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(variant))
        {
            return $"{brand} {variant}";
        }

        return "";
    }

    private static bool HasVisibleTextMismatch(string productName, string variant, IReadOnlyList<string> visibleText)
    {
        var normalized = NormalizeOcrText(string.Join(" ", visibleText));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return !IsFieldSupportedByOcr(productName, normalized) || !IsFieldSupportedByOcr(variant, normalized);
    }

    private static bool IsOnlyBrandLogoRecognition(
        string productName,
        string brand,
        string variant,
        string shade,
        IReadOnlyList<string> visibleText)
    {
        var normalizedBrand = NormalizeOcrText(brand);
        var normalizedFields = NormalizeOcrText(string.Join(" ", new[]
        {
            productName,
            variant,
            shade,
            string.Join(" ", visibleText)
        }));
        if (string.IsNullOrWhiteSpace(normalizedBrand) || string.IsNullOrWhiteSpace(normalizedFields))
        {
            return false;
        }

        var allowedLogoWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            normalizedBrand,
            "CD",
            "DIOR",
            "YSL",
            "MAC",
            "SK",
            "II"
        };
        var productWords = normalizedFields
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => !allowedLogoWords.Contains(word))
            .ToArray();
        return productWords.Length == 0;
    }

    private static bool IsBrandWithOnlyGenericPlaceText(string productName, string brand)
    {
        if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(brand))
        {
            return false;
        }

        var normalizedName = NormalizeOcrText(productName);
        var normalizedBrand = NormalizeOcrText(brand);
        if (string.IsNullOrWhiteSpace(normalizedName) ||
            string.IsNullOrWhiteSpace(normalizedBrand) ||
            !normalizedName.StartsWith(normalizedBrand, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = normalizedName[normalizedBrand.Length..].Trim();
        return rest is "" or "NEW YORK" or "NY" or "PARIS" or "LONDON" or "MILANO";
    }

    private static bool IsFieldSupportedByOcr(string value, string normalizedVisibleText)
    {
        var words = MeaningfulOcrWords(value).ToArray();
        if (words.Length == 0 || string.IsNullOrWhiteSpace(normalizedVisibleText))
        {
            return true;
        }

        var supportedCount = words.Count(normalizedVisibleText.Contains);
        return supportedCount >= Math.Max(1, (int)Math.Ceiling(words.Length * 0.65));
    }

    private static IEnumerable<string> MeaningfulOcrWords(string value) =>
        NormalizeOcrText(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length > 1 && !word.All(char.IsDigit) && !OcrStopWords.Contains(word));

    private static string BuildProductNameFromVisibleText(IReadOnlyList<string> visibleText, string fallbackBrand)
    {
        var parts = new List<string>();
        var normalizedBrand = NormalizeOcrText(fallbackBrand);
        foreach (var line in visibleText.Where(value => !IsOcrDescriptorLine(value)).Take(5))
        {
            var normalizedLine = NormalizeOcrText(line);
            if (string.IsNullOrWhiteSpace(normalizedLine))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(normalizedBrand) && normalizedLine.Equals(normalizedBrand, StringComparison.OrdinalIgnoreCase))
            {
                if (!parts.Any(part => NormalizeOcrText(part).Equals(normalizedBrand, StringComparison.OrdinalIgnoreCase)))
                {
                    parts.Add(ToTitleCase(fallbackBrand));
                }
                continue;
            }

            if (MeaningfulOcrWords(line).Any() && !parts.Any(part => NormalizeOcrText(part).Equals(normalizedLine, StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add(ToTitleCase(line));
            }
        }

        return CleanInline(string.Join(" ", parts));
    }

    private static bool IsOcrDescriptorLine(string value)
    {
        var normalized = NormalizeOcrText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"\b\d{1,3}\s*(ML|FL|OZ)\b"))
        {
            return true;
        }

        return Regex.IsMatch(normalized, @"\b(EAU DE PARFUM|EAU DE TOILETTE|VAPORISATEUR|NATURAL SPRAY|INGREDIENTS|MADE IN|BATCH|LOT)\b");
    }

    private static string ToTitleCase(string value)
    {
        var clean = CleanInline(value);
        if (string.IsNullOrWhiteSpace(clean))
        {
            return "";
        }

        return CultureInfo.GetCultureInfo("vi-VN").TextInfo.ToTitleCase(clean.ToLowerInvariant())
            .Replace("D Oro", "D'Oro", StringComparison.OrdinalIgnoreCase)
            .Replace("Ml", "ml", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeOcrText(string value)
    {
        var normalized = RemoveVietnameseDiacritics(value).ToUpperInvariant();
        normalized = normalized.Replace("’", "'").Replace("`", "'");
        normalized = Regex.Replace(normalized, @"D\s*['’]?\s*ORO", "D ORO");
        normalized = Regex.Replace(normalized, @"[^A-Z0-9]+", " ");
        return Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static string CleanIdentificationField(string value)
    {
        var clean = CleanInline(value);
        return IsPlaceholder(clean) ? "" : clean;
    }

    private static string BuildIdentificationSearchQuery(string productName, string brand, string category, string variant, string shade)
    {
        var parts = new[] { brand, productName, category, variant, shade, "official" }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(" ", parts);
    }

    private static SaleContentResult NormalizeSaleContent(SaleContentResult result, ConfirmedProductRequest request)
    {
        var verifiedDetails = result.VerifiedDetails ?? new VerifiedDetails([], [], [], []);
        var blocks = (result.ContentBlocks ?? [])
            .Select(block => NormalizeContentBlock(block, request))
            .Where(block => !IsEmptyContentBlock(block))
            .Take(6)
            .ToArray();

        return result with
        {
            CreativeDirection = CleanInline(result.CreativeDirection),
            Headline = CleanText(result.Headline),
            Opening = CleanText(result.Opening),
            ContentBlocks = blocks,
            ShortCaption = "",
            CallToAction = NormalizeCallToAction(result.CallToAction),
            Contact = BuildContactInformation(request),
            Hashtags = NormalizeHashtags(result.Hashtags, request),
            VerifiedDetails = verifiedDetails with
            {
                Claims = NormalizeClaims(verifiedDetails.Claims),
                Usage = NormalizeDetailList(verifiedDetails.Usage),
                Warnings = NormalizeDetailList(verifiedDetails.Warnings)
            },
            WarningMessage = CleanText(result.WarningMessage)
        };
    }

    private static SaleContentResult MapCreativeToSaleContent(SaleCreativeResult creative, ConfirmedProductRequest request)
    {
        var blocks = new List<ContentBlock>();
        var highlights = (creative.Highlights ?? [])
            .Select(item => new HighlightItem(item.Icon, item.Title, item.Description))
            .ToArray();

        if (highlights.Length > 0)
        {
            blocks.Add(new ContentBlock("highlights", "", "", highlights));
        }

        if (!string.IsNullOrWhiteSpace(creative.ProductNotice))
        {
            blocks.Add(new ContentBlock("paragraph", "", creative.ProductNotice, []));
        }

        if (!string.IsNullOrWhiteSpace(creative.Closing))
        {
            blocks.Add(new ContentBlock("paragraph", "", creative.Closing, []));
        }

        return new SaleContentResult(
            creative.CreativeAngle,
            creative.Headline,
            creative.Opening,
            blocks,
            "",
            creative.CallToAction ?? new CallToActionContent("", ""),
            BuildContactInformation(request),
            creative.Hashtags ?? [],
            new VerifiedDetails([], [], [], []),
            true,
            "");
    }

    private static bool ContainsForbiddenSaleCliche(SaleContentResult result)
    {
        var values = new[]
            {
                result.Headline,
                result.Opening,
                result.ShortCaption,
                result.CallToAction?.Text ?? ""
            }
            .Concat(result.ContentBlocks.SelectMany(block => new[]
            {
                block.Title,
                block.Text
            }.Concat(block.Items.SelectMany(item => new[] { item.BenefitTitle, item.Text }))))
            .Where(value => !string.IsNullOrWhiteSpace(value));
        var text = string.Join(" ", values);

        var normalized = RemoveVietnameseDiacritics(text).ToLowerInvariant();
        var forbidden = new[]
        {
            "co may thoi gian",
            "bieu tuong cua su tinh te",
            "khi chat vuot thoi gian",
            "tren co tay thanh manh",
            "ton vinh ve dep",
            "khang dinh dau an",
            "nang tam phong cach",
            "ve dep vuot thoi gian",
            "tuyet tac",
            "kiet tac",
            "hy vong nang se tim thay",
            "phu kien chan ai",
            "nguoi ban dong hanh trung thanh",
            "luu giu nhung cot moc",
            "manh ghep con thieu",
            "manh ghep hoan hao",
            "ket hop hoan hao",
            "khoanh khac dang nho",
            "tao nen nhung khoanh khac",
            "bi quyet",
            "can nhac so huu",
            "vua du chinh chu",
            "vua du thu hut",
            "lua chon ly tuong",
            "tu tin toa sang",
            "khong lo hai da",
            "khong lo hai moi",
            "an toan moi ngay",
            "lan da em be",
            "nhu da em be",
            "vuot troi",
            "spa tai nha",
            "danh thuc ve ngoai",
            "danh thuc ngay moi",
            "dau an rieng",
            "dong hanh cung nang",
            "moi ngay that hung khoi",
            "neu nang da yeu",
            "neu nang da biet",
            "vuon hoa gucci bloom",
            "vuon hoa cua gucci bloom",
            "phien ban truoc",
            "hoa co ban ngay",
            "nhung phien ban ban ngay",
            "nhung phien ban hoa co",
            "chieu sau hon nhung phien ban",
            "mang chieu sau hon nhung",
            "hao nhoang",
            "tuyen ngon tu do",
            "khang dinh ca tinh rieng",
            "khang dinh ban than",
            "dinh hinh ca tinh",
            "dinh nghia phong thai",
            "dinh nghia phong cach",
            "phong thai tu do",
            "phong thai duong dai",
            "ban sac rieng",
            "mui huong dinh hinh",
            "khi chat sang trong",
            "thiet ke nghe thuat",
            "thiet ke an tuong",
            "thiet ke day nghe thuat",
            "ve ngoai sang trong",
            "not huong chieu sau",
            "not huong cuoi am ap",
            "huong hoa dac trung",
            "huong thom giup",
            "tinh dau oai huong",
            "vung vang suot ngay dai",
            "vung vang",
            "lam chu moi tinh huong",
            "manh me va chan thuc",
            "nguoi phu nu hien dai",
            "phu kien gia tri",
            "oai huong phap",
            "tran day nang luong",
            "day loi cuon",
            "doi ngu ho tro",
            "cam thay hai long moi khi cam",
            "gan bo lau dai trong chu trinh"
        };

        return forbidden.Any(normalized.Contains);
    }

    private static bool IsLowQualitySaleContent(SaleContentResult result)
    {
        if (ContainsForbiddenSaleCliche(result))
        {
            return true;
        }

        var opening = RemoveVietnameseDiacritics(result.Opening ?? "").ToLowerInvariant();
        var weakOpening = new[]
        {
            "neu nang dang tim kiem mot mui huong",
            "neu nang muon tim mot mui huong",
            "day chinh la lua chon",
            "lua chon giup nang",
            "mang den cho nang",
            "phu hop voi nhieu phong cach",
            "chinh la lua chon giup nang",
            "giup nang them phan",
            "dua nang buoc vao"
        };
        if (weakOpening.Any(opening.Contains) && !HasConcreteBuyerContext(opening))
        {
            return true;
        }

        var highlightTexts = result.ContentBlocks
            .Where(block => block.Type.Equals("highlights", StringComparison.OrdinalIgnoreCase))
            .SelectMany(block => block.Items)
            .Select(item => RemoveVietnameseDiacritics($"{item.BenefitTitle} {item.Text}").ToLowerInvariant())
            .ToArray();

        if (highlightTexts.Length == 0)
        {
            return true;
        }

        var allText = string.Join(" ", new[] { opening }.Concat(highlightTexts));
        var fitSignals = new[]
        {
            "hop nhat",
            "dang thu neu",
            "nang se thich neu",
            "chang se thich neu",
            "neu nang thich",
            "neu chang thich",
            "khong hop neu",
            "khong danh cho",
            "hop gu",
            "dung gu",
            "gu nay",
            "nen chon neu",
            "muon mot mui",
            "can mot mui",
            "thich mui",
            "so mui",
            "hop de",
            "di lam van",
            "di toi co",
            "ngay lam viec",
            "cong so",
            "buoi toi",
            "cho buoi",
            "khi di",
            "tren da",
            "sach",
            "sach gon",
            "hoi am",
            "am va mem",
            "mem tren da",
            "ro vua du",
            "du ro",
            "bot ngot",
            "khong qua",
            "khong qua ngot",
            "khong bi gat",
            "khong bi pho",
            "de gan",
            "di hen",
            "gap khach"
        };
        if (fitSignals.Count(allText.Contains) < 1)
        {
            return true;
        }

        var usefulHighlights = highlightTexts.Count(HasConcreteBuyerContext);
        if (usefulHighlights < Math.Min(3, highlightTexts.Length))
        {
            return true;
        }

        var weakHighlightTitles = new[]
        {
            "not huong",
            "huong hoa dac trung",
            "thiet ke",
            "tinh dau",
            "phong cach rieng",
            "cam giac duong dai",
            "phong thai",
            "khi chat",
            "tuyen ngon",
            "ban sac"
        };

        return highlightTexts.Any(text => weakHighlightTitles.Any(text.StartsWith));
    }

    private static bool HasConcreteBuyerContext(string normalizedText)
    {
        var signals = new[]
        {
            "di lam",
            "di tiec",
            "di hen",
            "hen ho",
            "gap khach",
            "buoi toi",
            "cuoi tuan",
            "lam qua",
            "tang",
            "sau an",
            "makeup nhanh",
            "da de chiu",
            "khong gat",
            "khong qua ngot",
            "bot ngot",
            "de dung",
            "chin chu",
            "tu van",
            "chon dung",
            "hop voi nang",
            "hop nhat",
            "hop gu",
            "dung gu",
            "dang thu neu",
            "nang se thich neu",
            "khong hop neu",
            "khong danh cho",
            "nen chon neu",
            "nang muon",
            "nang can",
            "nang so",
            "muon mot mui",
            "can mot mui",
            "thich mui",
            "so mui",
            "hop de",
            "mui sach",
            "mui am",
            "mui mem",
            "sach",
            "hoi am",
            "mem tren da",
            "ro vua du",
            "du ro",
            "bot gat",
            "di toi",
            "ngay lam viec",
            "cong so",
            "cho buoi",
            "khi di",
            "khong nong",
            "khong pho",
            "khong qua phu",
            "khong bi gat",
            "khong bi pho",
            "sach gon",
            "am va mem",
            "de gan",
            "thu mui",
            "giu mui",
            "bam mui",
            "toa mui",
            "tren da",
            "chang muon",
            "cac chang"
        };

        return signals.Any(normalizedText.Contains);
    }

    private static string RemoveVietnameseDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Replace('đ', 'd').Replace('Đ', 'D');
    }

    private static ContentBlock NormalizeContentBlock(ContentBlock block, ConfirmedProductRequest request)
    {
        var type = CleanInline(block.Type);
        if (string.IsNullOrWhiteSpace(type))
        {
            type = "paragraph";
        }

        var items = (block.Items ?? [])
            .Select(NormalizeHighlightItem)
            .Where(item => !IsEmptyHighlightItem(item))
            .Take(type.Equals("highlights", StringComparison.OrdinalIgnoreCase) ? 4 : 8)
            .ToArray();

        return block with
        {
            Type = type.Equals("offer", StringComparison.OrdinalIgnoreCase) ? "paragraph" : type,
            Title = CleanText(block.Title),
            Text = CleanText(block.Text),
            Items = items
        };
    }

    private static bool IsEmptyContentBlock(ContentBlock block) =>
        string.IsNullOrWhiteSpace(block.Title) &&
        string.IsNullOrWhiteSpace(block.Text) &&
        (block.Items is null || block.Items.Count == 0);

    private static ContactInformation BuildContactInformation(ConfirmedProductRequest request) =>
        new(
            FormatContactLine("🏡", request.ShopName),
            FormatContactLine("📞", request.Phone),
            FormatContactLine("📍", request.Address),
            FormatContactLine("🌐", request.Website));

    private static string FormatContactLine(string icon, string value)
    {
        var cleanValue = CleanInline(value);
        return string.IsNullOrWhiteSpace(cleanValue) ? "" : $"{icon} {cleanValue}";
    }

    private static HighlightItem NormalizeHighlightItem(HighlightItem item) =>
        new(NormalizeIcon(item.Icon), CleanInline(item.BenefitTitle), CleanText(item.Text));

    private static string NormalizeIcon(string value)
    {
        var clean = CleanInline(value).Trim(':', '-', '_', ' ');
        if (string.IsNullOrWhiteSpace(clean))
        {
            return "✨";
        }

        return clean.ToLowerInvariant() switch
        {
            "sparkle" or "sparkles" or "star" or "stars" or "shine" or "glow" => "✨",
            "droplet" or "drop" or "water" or "moisture" or "hydration" => "💧",
            "palette" or "color" or "colour" or "shade" => "🎨",
            "check" or "checkmark" or "tick" or "done" => "✅",
            "lipstick" or "lip" or "makeup" => "💄",
            "flower" or "rose" or "floral" => "🌸",
            "shield" or "protect" or "protection" => "🛡️",
            "mirror" => "🪞",
            "bubble" or "bubbles" or "foam" => "🫧",
            "leaf" or "nature" or "natural" => "🌿",
            "sun" or "sunny" => "☀️",
            "moon" or "night" => "🌙",
            "heart" => "💗",
            "gift" => "🎁",
            "bag" or "shopping" or "cart" => "🛍️",
            "message" or "mail" or "inbox" => "💌",
            _ => clean
        };
    }

    private static bool IsEmptyHighlightItem(HighlightItem item) =>
        string.IsNullOrWhiteSpace(item.Icon) &&
        string.IsNullOrWhiteSpace(item.BenefitTitle) &&
        string.IsNullOrWhiteSpace(item.Text);

    private static CallToActionContent NormalizeCallToAction(CallToActionContent value)
    {
        if (value is null)
        {
            return new CallToActionContent("", "");
        }

        return new CallToActionContent(CleanInline(value.Icon), CleanText(value.Text));
    }

    private static IReadOnlyList<VerifiedClaim> NormalizeClaims(IReadOnlyList<VerifiedClaim> claims) =>
        (claims ?? [])
            .Select(claim => new VerifiedClaim(
                CleanText(claim.Claim),
                CleanInline(claim.SourceUrl),
                CleanInline(claim.SourceTitle),
                CleanInline(claim.MatchedProduct),
                CleanInline(claim.MatchedVariant),
                Math.Clamp(claim.Confidence, 0, 100)))
            .Where(claim => !string.IsNullOrWhiteSpace(claim.Claim) && claim.Confidence >= 85)
            .Take(12)
            .ToArray();

    private static IReadOnlyList<string> NormalizeHashtags(IReadOnlyList<string> hashtags, ConfirmedProductRequest request)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        AddHashtag(result, seen, request.Brand);
        AddHashtag(result, seen, request.ProductName);
        AddHashtag(result, seen, request.Variant);
        AddHashtag(result, seen, request.Category);

        foreach (var raw in hashtags ?? [])
        {
            var parts = raw.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var tag = Regex.Replace(part, @"[^\p{L}\p{N}_#]", "");
                if (string.IsNullOrWhiteSpace(tag) || IsPlaceholder(tag))
                {
                    continue;
                }

                if (!tag.StartsWith('#'))
                {
                    tag = $"#{tag}";
                }

                if (tag.Length > 1 && seen.Add(tag))
                {
                    result.Add(tag);
                }

                if (result.Count == 7)
                {
                    return result;
                }
            }
        }

        return result;
    }

    private static void AddHashtag(List<string> result, HashSet<string> seen, string value)
    {
        var tag = BuildHashtag(value);
        if (string.IsNullOrWhiteSpace(tag) || !seen.Add(tag))
        {
            return;
        }

        result.Add(tag);
    }

    private static string BuildHashtag(string value)
    {
        var clean = CleanInline(value);
        if (string.IsNullOrWhiteSpace(clean) || IsPlaceholder(clean))
        {
            return "";
        }

        var ascii = RemoveVietnameseDiacritics(clean);
        ascii = Regex.Replace(ascii, @"[^A-Za-z0-9]+", " ").Trim();
        if (string.IsNullOrWhiteSpace(ascii))
        {
            return "";
        }

        var words = ascii
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(5)
            .Select(word => char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word[1..] : ""));
        var tag = "#" + string.Concat(words);
        return tag.Length > 1 ? tag : "";
    }

    private static IReadOnlyList<string> NormalizeDetailList(IReadOnlyList<string> values) =>
        (values ?? [])
            .Select(CleanListLine)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(12)
            .ToArray();

    private static string CleanListLine(string value)
    {
        var clean = CleanText(value);
        clean = Regex.Replace(clean, @"^\s*[-•*]+\s*", "");
        return IsPlaceholder(clean) ? "" : clean;
    }

    private static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var clean = value.Normalize(NormalizationForm.FormKD).Replace("\r\n", "\n").Trim();
        clean = clean.Replace("**", "").Replace("__", "").Replace("~~", "");
        clean = Regex.Replace(clean, @"[\u0335\u0336]", "");
        clean = Regex.Replace(clean, @"</?(?:strong|b|del|s)>", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"[ \t]+", " ");
        clean = Regex.Replace(clean, @"\n{3,}", "\n\n");
        return IsPlaceholder(clean) ? "" : clean;
    }

    private static string CleanInline(string value) =>
        Regex.Replace(CleanText(value), @"\s+", " ").Trim();

    private void BeginContentLogSection(string workflow, string subject)
    {
        officialUrlDiagnostics.Clear();
        var cleanWorkflow = string.IsNullOrWhiteSpace(workflow) ? "CONTENT" : workflow.Trim();
        var cleanSubject = ShortDiagnostic(subject ?? "");
        var startedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

        logger.LogInformation("[CONTENT]");
        logger.LogInformation("[CONTENT] ============================================================");
        logger.LogInformation(
            "[CONTENT] BẮT ĐẦU {Workflow} | {StartedAt} | {Subject}",
            cleanWorkflow,
            startedAt,
            string.IsNullOrWhiteSpace(cleanSubject) ? "không có tiêu đề" : cleanSubject);
        logger.LogInformation("[CONTENT] ------------------------------------------------------------");
    }

    private void AddOfficialUrlDiagnostic(string message)
    {
        var clean = ToUserFacingOfficialUrlDiagnostic(ShortDiagnostic(message));
        if (string.IsNullOrWhiteSpace(clean) ||
            officialUrlDiagnostics.Any(item => item.Equals(clean, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        officialUrlDiagnostics.Add(clean);
        logger.LogInformation("[CONTENT] {Message}", clean);
    }

    private string BuildOfficialUrlFailureMessage(string fallback)
    {
        if (officialUrlDiagnostics.Count == 0)
        {
            return fallback;
        }

        return $"{fallback} Nhật ký lỗi: {string.Join(" | ", officialUrlDiagnostics)}";
    }

    private void LogOfficialUrlOutcome(
        string mode,
        ConfirmedProductRequest request,
        string productName,
        OfficialProductUrlResult result)
    {
        var diagnostics = officialUrlDiagnostics.Count == 0
            ? ""
            : string.Join(" | ", officialUrlDiagnostics);
        if (string.IsNullOrWhiteSpace(result.Url))
        {
            logger.LogWarning(
                "[CONTENT] Tìm URL thất bại. Kieu={Mode}; SanPham={Product}; ThuongHieu={Brand}; PhienBan={Variant}; Mau={Shade}; DanhMuc={Category}; ThongBao={Message}; NhatKy={Diagnostics}",
                mode,
                productName,
                request.Brand,
                request.Variant,
                request.Shade,
                request.Category,
                result.Message,
                diagnostics);
            return;
        }

        logger.LogInformation(
            "[CONTENT] Tìm URL thành công. Kieu={Mode}; SanPham={Product}; ThuongHieu={Brand}; PhienBan={Variant}; Mau={Shade}; DanhMuc={Category}; URL={Url}; TieuDe={Title}; Website={Website}; NhatKy={Diagnostics}",
            mode,
            productName,
            request.Brand,
            request.Variant,
            request.Shade,
            request.Category,
            result.Url,
            result.Title,
            result.Website,
            diagnostics);
    }

    private static string ShortDiagnostic(string value)
    {
        var clean = string.IsNullOrWhiteSpace(value)
            ? ""
            : Regex.Replace(value.Replace("\r\n", " ").Replace('\n', ' ').Trim(), @"\s+", " ");
        return clean.Length <= 220 ? clean : clean[..220] + "...";
    }

    private static string ToUserFacingOfficialUrlDiagnostic(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var clean = value;
        clean = Regex.Replace(clean, @"For more information on this error, head to:\s*https?:\/\/\S+", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"To monitor your current usage, head to:\s*https?:\/\/\S+", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\*\s*quota exceeded for metric:[^|.]+(?:\.[^|]*)?", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\*\s*giới hạn lượt xử lý exceeded for metric:[^|.]+(?:\.[^|]*)?", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"quota exceeded for metric:[^|.]+(?:\.[^|]*)?", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"giới hạn lượt xử lý exceeded for metric:[^|.]+(?:\.[^|]*)?", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"Please retry in [^|.]+\.?", "Vui lòng chờ một lát rồi thử lại.", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"You exceeded your current quota, please check your plan and billing details\.?", "Hoàn Doãn Beauty & Academy đang vượt giới hạn lượt xử lý hiện tại.", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"You exceeded your current [^.,|]+, please check your plan and billing details\.?", "Hoàn Doãn Beauty & Academy đang vượt giới hạn lượt xử lý hiện tại.", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"Gemini(?:/Search)?", "Hoàn Doãn Beauty & Academy", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"Google Search/Hoàn Doãn Beauty & Academy", "Hoàn Doãn Beauty & Academy", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\bmodel\s+gemini-[A-Za-z0-9.\-_]+", "hệ thống AI", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"gemini-[A-Za-z0-9.\-_]+", "hệ thống AI", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"RPM/TPM", "lượt xử lý", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"quota", "giới hạn lượt xử lý", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"API key", "kết nối hệ thống", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"https?:\/\/\S+", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"Hoàn Doãn Beauty & Academy đang giới hạn lượt xử lý:\s*Hoàn Doãn Beauty & Academy đang vượt giới hạn lượt xử lý hiện tại\.?", "Hoàn Doãn Beauty & Academy đang giới hạn lượt xử lý hiện tại.", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"HTTP\s*429\s*-\s*", "", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"HTTP\s*403", "website hãng đang chặn đọc tự động", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"hard-code/known", "lưu sẵn", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\bcrawl\b", "đọc website", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"timeout", "phản hồi quá lâu", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"quá tải/phản hồi quá lâu", "quá tải hoặc phản hồi quá lâu", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^image received$", "Đã nhận ảnh từ frontend", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^image uploaded$", "Ảnh đã được gửi vào workflow tìm nguồn", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^image analyzed in ", "Đã phân tích ảnh trong ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^image analysis failed:", "Phân tích ảnh thất bại:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^visibleText extracted:", "Chữ đọc được từ ảnh:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^visible text extracted:", "Chữ đọc được từ ảnh:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^predicted product:", "Tên sản phẩm dự đoán:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^search queries generated:", "Query tìm kiếm đã tạo:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^Search query:", "Query tìm kiếm:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^query completed in ", "Query hoàn tất trong ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^grounding URLs received:", "Số URL Grounding nhận được:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^grounding URL:", "URL Grounding:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^candidates scored:", "Số URL ứng viên đã chấm điểm:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^candidate score:", "Điểm URL ứng viên:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^trusted URL selected:", "Đã chọn URL tin cậy:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^URL Context read:", "Đã đọc URL Context:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^URL Context verified:", "Đã xác minh URL Context:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^URL verified:", "URL đã xác minh:", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^identification saved$", "Đã lưu nhận diện chính thức", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^official identification saved$", "Đã lưu nhận diện chính thức", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"^image URL workflow completed in ", "Workflow tìm URL từ ảnh hoàn tất trong ", RegexOptions.IgnoreCase);
        clean = Regex.Replace(clean, @"\s+", " ").Trim().Trim(' ', '-', ':');
        return clean.Length <= 180 ? clean : clean[..180] + "...";
    }

    private static bool IsPlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var clean = value.Trim().Trim('*').Trim();
        var lower = clean.ToLowerInvariant();
        return lower is "n/a" or "na" or "null" or "undefined" or "-" or "none" ||
            lower is "unknown" or "unclear" or "not sure" ||
            lower.Contains("chưa cập nhật") ||
            lower.Contains("chua cap nhat") ||
            lower.Contains("đang cập nhật") ||
            lower.Contains("dang cap nhat") ||
            lower.Contains("không rõ") ||
            lower.Contains("khong ro") ||
            lower.Contains("không chắc") ||
            lower.Contains("khong chac") ||
            lower.Contains("chưa rõ") ||
            lower.Contains("chua ro") ||
            lower.Contains("không có dữ liệu") ||
            lower.Contains("khong co du lieu") ||
            lower.Contains("chỉ hiển thị khi có dữ liệu") ||
            lower.Contains("chi hien thi khi co du lieu");
    }

    private static string BuildSearchQuery(ConfirmedProductRequest request, string productName)
    {
        var variantParts = string.Join(" ", new[] { request.Brand, request.Variant, request.Shade, request.Finish }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()));
        var query = FirstNonEmpty(request.SearchQuery, $"{productName} {variantParts} official ingredients benefits usage");
        var officialDomains = BuildOfficialDomainSearchHints(request.Brand);
        if (officialDomains.Length > 0)
        {
            var domainQuery = string.Join(" OR ", officialDomains.Select(domain => $"site:{domain}"));
            query = $"({domainQuery}) {productName} {variantParts} official product page";
        }

        if (!string.IsNullOrWhiteSpace(request.OfficialProductUrl))
        {
            query = $"{query} {request.OfficialProductUrl}";
        }

        return query.Trim();
    }

    private static string NormalizeOfficialBrandName(string brand)
    {
        var profile = FindOfficialBrandProfile(brand);
        return profile?.CanonicalName ?? brand.Trim();
    }

    private static BrandOfficialProfile? FindOfficialBrandProfile(string brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return null;
        }

        var normalized = NormalizeBrandAlias(brand);
        var compact = Regex.Replace(normalized, @"[^a-z0-9]", "");
        foreach (var profile in BrandOfficialRegistry)
        {
            if (BrandAliasMatches(normalized, compact, profile.CanonicalName) ||
                profile.Aliases.Any(alias => BrandAliasMatches(normalized, compact, alias)))
            {
                return profile;
            }
        }

        return null;
    }

    private static bool BrandAliasMatches(string normalizedBrand, string compactBrand, string alias)
    {
        var normalizedAlias = NormalizeBrandAlias(alias);
        var compactAlias = Regex.Replace(normalizedAlias, @"[^a-z0-9]", "");
        if (string.IsNullOrWhiteSpace(compactAlias))
        {
            return false;
        }

        if (normalizedBrand.Equals(normalizedAlias, StringComparison.OrdinalIgnoreCase) ||
            compactBrand.Equals(compactAlias, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (compactAlias.Length >= 4 && compactBrand.Contains(compactAlias, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var aliasTokens = normalizedAlias
            .Split([' ', '-', '_', '.', '\'', '’', '&'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .Where(token => token is not "the" and not "and" and not "beauty" and not "paris" and not "new" and not "york")
            .ToArray();
        if (aliasTokens.Length == 0)
        {
            return false;
        }

        var matchedTokens = aliasTokens.Count(token => normalizedBrand.Contains(token, StringComparison.OrdinalIgnoreCase));
        return aliasTokens.Length == 1 ? matchedTokens == 1 : matchedTokens >= Math.Min(2, aliasTokens.Length);
    }

    private static string NormalizeBrandAlias(string value) =>
        Regex.Replace(RemoveVietnameseDiacritics(value).ToLowerInvariant(), @"[·•]", " ").Trim();

    private static string[] BuildOfficialDomainSearchHints(string brand)
    {
        var officialProfile = FindOfficialBrandProfile(brand);
        if (officialProfile is not null)
        {
            return officialProfile.Domains;
        }

        var normalized = RemoveVietnameseDiacritics(brand).ToLowerInvariant();
        if (normalized.Contains("armani"))
        {
            return ["armanibeauty.com"];
        }

        if (normalized.Contains("burberry"))
        {
            return ["burberry.com"];
        }

        if (normalized.Contains("bvlgari") || normalized.Contains("bulgari"))
        {
            return ["bulgari.com"];
        }

        if (normalized.Contains("calvin klein"))
        {
            return ["calvinklein.us", "calvinklein.com"];
        }

        if (normalized.Contains("carolina herrera"))
        {
            return ["carolinaherrera.com"];
        }

        if (normalized.Contains("clinique"))
        {
            return ["clinique.com"];
        }

        if (normalized.Contains("sk-ii") || normalized.Contains("skii") || normalized.Contains("sk ii"))
        {
            return ["sk-ii.com"];
        }

        if (Regex.IsMatch(normalized, @"\bmac\b") || normalized.Contains("m·a·c"))
        {
            return ["maccosmetics.com"];
        }

        if (Regex.IsMatch(normalized, @"\b3ce\b") || normalized.Contains("stylenanda"))
        {
            return ["stylenanda.com"];
        }

        if (normalized.Contains("yves saint laurent") || normalized.Contains("ysl"))
        {
            return ["yslbeautyus.com", "yslbeauty.com"];
        }

        if (normalized.Contains("gucci"))
        {
            return ["gucci.com"];
        }

        if (normalized.Contains("creed"))
        {
            return ["creedfragrances.com"];
        }

        if (normalized.Contains("dior"))
        {
            return ["dior.com"];
        }

        if (normalized.Contains("dolce") || normalized.Contains("gabbana"))
        {
            return ["dolcegabbana.com"];
        }

        if (normalized.Contains("estee lauder") || normalized.Contains("estée lauder"))
        {
            return ["esteelauder.com"];
        }

        if (normalized.Contains("givenchy"))
        {
            return ["givenchybeauty.com"];
        }

        if (normalized.Contains("hermes") || normalized.Contains("hermès"))
        {
            return ["hermes.com"];
        }

        if (normalized.Contains("jo malone"))
        {
            return ["jomalone.com"];
        }

        if (normalized.Contains("chanel"))
        {
            return ["chanel.com"];
        }

        if (normalized.Contains("lancome") || normalized.Contains("lancôme"))
        {
            return ["lancome-usa.com", "lancome.com"];
        }

        if (normalized.Contains("le labo"))
        {
            return ["lelabofragrances.com"];
        }

        if (normalized.Contains("maison francis") || normalized.Contains("mfk"))
        {
            return ["franciskurkdjian.com"];
        }

        if (normalized.Contains("tom ford"))
        {
            return ["tomfordbeauty.com"];
        }

        if (normalized.Contains("valentino"))
        {
            return ["valentino-beauty.com"];
        }

        if (normalized.Contains("versace"))
        {
            return ["versace.com"];
        }

        return [];
    }

    private static string BuildOfficialDomainSearchHint(string brand) =>
        BuildOfficialDomainSearchHints(brand).FirstOrDefault() ?? "";

    private static string BuildOptionalSalesInfo(ConfirmedProductRequest request)
    {
        var lines = new List<string>();
        AddOptional(lines, "Giá bán", request.Price);
        AddOptional(lines, "Giá khuyến mãi", request.SalePrice);
        AddOptional(lines, "Quà tặng", request.Gift);
        AddOptional(lines, "Tên shop", request.ShopName);
        AddOptional(lines, "Số điện thoại", request.Phone);
        AddOptional(lines, "Địa chỉ", request.Address);
        AddOptional(lines, "Website", request.Website);
        AddOptional(lines, "Số lượng còn lại", request.RemainingQuantity);
        return lines.Count == 0 ? "Người dùng chưa nhập giá, khuyến mãi, quà tặng, shop, điện thoại hoặc số lượng." : string.Join("\n", lines);
    }

    private static void AddOptional(List<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"- {label}: {value.Trim()}");
        }
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static int MapStatusCode(int geminiStatusCode) => geminiStatusCode switch
    {
        400 => 400,
        401 or 403 => 403,
        404 => 404,
        429 => 429,
        500 or 502 or 503 or 504 => 503,
        _ => 502
    };

    private string BuildGeminiErrorMessage(string rawJson, int statusCode, string? modelName = null)
    {
        var detail = ExtractGeminiError(rawJson);
        var model = string.IsNullOrWhiteSpace(modelName) ? GetGeminiModel() : modelName;
        return statusCode switch
        {
            400 => "Dữ liệu gửi lên Hoàn Doãn Beauty & Academy không hợp lệ. Vui lòng kiểm tra ảnh hoặc thông tin sản phẩm.",
            401 or 403 => "Kết nối Hoàn Doãn Beauty & Academy chưa hợp lệ hoặc chưa có quyền sử dụng.",
            404 => "Hoàn Doãn Beauty & Academy chưa sẵn sàng với cấu hình xử lý hiện tại.",
            429 => string.IsNullOrWhiteSpace(detail)
                ? "Hoàn Doãn Beauty & Academy đang giới hạn lượt xử lý. Hệ thống đã thử chuyển hướng xử lý dự phòng nếu có; vui lòng đợi khoảng 1 phút rồi thử lại."
                : $"Hoàn Doãn Beauty & Academy đang giới hạn lượt xử lý: {ToUserFacingOfficialUrlDiagnostic(detail)}",
            500 or 502 or 503 or 504 => "Hoàn Doãn Beauty & Academy đang quá tải, vui lòng thử lại.",
            _ => string.IsNullOrWhiteSpace(detail) ? "Hoàn Doãn Beauty & Academy chưa xử lý được yêu cầu." : $"Hoàn Doãn Beauty & Academy báo lỗi: {ToUserFacingOfficialUrlDiagnostic(detail)}"
        };
    }

    private static string ExtractGeminiError(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? "";
            }
        }
        catch
        {
            return "";
        }

        return "";
    }

    private static object ProductIdentificationSchema() => new
    {
        type = "OBJECT",
        properties = new
        {
            productName = new { type = "STRING" },
            brand = new { type = "STRING" },
            productLine = new { type = "STRING" },
            variant = new { type = "STRING" },
            shade = new { type = "STRING" },
            finish = new { type = "STRING" },
            category = new { type = "STRING" },
            itemForm = new { type = "STRING", @enum = new[] { "full-product", "case", "refill", "accessory", "unknown" } },
            size = new { type = "STRING" },
            visibleText = new { type = "ARRAY", items = new { type = "STRING" } },
            confidence = new { type = "INTEGER" },
            searchQuery = new { type = "STRING" },
            needsConfirmation = new { type = "BOOLEAN" },
            message = new { type = "STRING" }
        },
        required = new[] { "productName", "brand", "productLine", "variant", "shade", "finish", "category", "itemForm", "size", "visibleText", "confidence", "searchQuery", "needsConfirmation", "message" }
    };

    private static object SaleCreativeSchema() => new
    {
        type = "OBJECT",
        properties = new
        {
            creativeAngle = new { type = "STRING" },
            headline = new { type = "STRING" },
            opening = new { type = "STRING" },
            highlights = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        icon = new { type = "STRING" },
                        title = new { type = "STRING" },
                        description = new { type = "STRING" }
                    },
                    required = new[] { "icon", "title", "description" }
                }
            },
            productNotice = new { type = "STRING" },
            closing = new { type = "STRING" },
            callToAction = new
            {
                type = "OBJECT",
                properties = new
                {
                    icon = new { type = "STRING" },
                    text = new { type = "STRING" }
                },
                required = new[] { "icon", "text" }
            },
            hashtags = new { type = "ARRAY", items = new { type = "STRING" } }
        },
        required = new[] { "creativeAngle", "headline", "opening", "highlights", "productNotice", "closing", "callToAction", "hashtags" }
    };

    private static object SaleContentSchema() => new
    {
        type = "OBJECT",
        properties = new
        {
            creativeDirection = new { type = "STRING" },
            headline = new { type = "STRING" },
            opening = new { type = "STRING" },
            contentBlocks = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        type = new { type = "STRING" },
                        title = new { type = "STRING" },
                        text = new { type = "STRING" },
                        items = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    icon = new { type = "STRING" },
                                    benefitTitle = new { type = "STRING" },
                                    text = new { type = "STRING" }
                                },
                                required = new[] { "icon", "benefitTitle", "text" }
                            }
                        }
                    },
                    required = new[] { "type", "title", "text", "items" }
                }
            },
            callToAction = new
            {
                type = "OBJECT",
                properties = new
                {
                    icon = new { type = "STRING" },
                    text = new { type = "STRING" }
                },
                required = new[] { "icon", "text" }
            },
            contact = new
            {
                type = "OBJECT",
                properties = new
                {
                    shopName = new { type = "STRING" },
                    phone = new { type = "STRING" },
                    address = new { type = "STRING" },
                    website = new { type = "STRING" }
                },
                required = new[] { "shopName", "phone", "address", "website" }
            },
            hashtags = new { type = "ARRAY", items = new { type = "STRING" } },
            shortCaption = new { type = "STRING" },
            verifiedDetails = new
            {
                type = "OBJECT",
                properties = new
                {
                    claims = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                claim = new { type = "STRING" },
                                sourceUrl = new { type = "STRING" },
                                sourceTitle = new { type = "STRING" },
                                matchedProduct = new { type = "STRING" },
                                matchedVariant = new { type = "STRING" },
                                confidence = new { type = "INTEGER" }
                            },
                            required = new[] { "claim", "sourceUrl", "sourceTitle", "matchedProduct", "matchedVariant", "confidence" }
                        }
                    },
                    usage = new { type = "ARRAY", items = new { type = "STRING" } },
                    warnings = new { type = "ARRAY", items = new { type = "STRING" } },
                    sources = new { type = "ARRAY", items = new { type = "OBJECT" } }
                },
                required = new[] { "claims", "usage", "warnings", "sources" }
            },
            researchSuccessful = new { type = "BOOLEAN" },
            warningMessage = new { type = "STRING" }
        },
        required = new[] { "creativeDirection", "headline", "opening", "contentBlocks", "callToAction", "contact", "hashtags", "shortCaption", "verifiedDetails", "researchSuccessful", "warningMessage" }
    };

    private static TrustedBeautySourceRegistry LoadTrustedBeautySourceRegistry()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "App_Data", "trusted-beauty-source-registry.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "trusted-beauty-source-registry.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "backend", "Beauty.Api", "App_Data", "trusted-beauty-source-registry.json")
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var registry = JsonSerializer.Deserialize<TrustedBeautySourceRegistry>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return registry?.Normalize() ?? TrustedBeautySourceRegistry.Empty;
            }
            catch
            {
                return TrustedBeautySourceRegistry.Empty;
            }
        }

        return TrustedBeautySourceRegistry.Empty;
    }
}

public sealed record ProductIdentificationResult(
    string ProductName,
    string Brand,
    string ProductLine,
    string Variant,
    string Shade,
    string Finish,
    string Category,
    string ItemForm,
    string Size,
    IReadOnlyList<string> VisibleText,
    int Confidence,
    string SearchQuery,
    bool NeedsConfirmation,
    string Message);

public sealed record OfficialProductUrlResult(string Url, string Title, string Website, string Message)
{
    public string BestUrl => Url;
    public string Brand { get; init; } = "";
    public string SourceType { get; init; } = "";
    public int Confidence { get; init; }
    public int MatchScore => Confidence;
    public IReadOnlyList<string> MatchedFields { get; init; } = [];
    public IReadOnlyList<GroundedSource> Sources { get; init; } = [];
    public IReadOnlyList<GroundedSource> AlternativeSources => Sources;
    public ProductIdentificationResult? Identification { get; init; }
}

public sealed record SaleContentResult(
    string CreativeDirection,
    string Headline,
    string Opening,
    IReadOnlyList<ContentBlock> ContentBlocks,
    string ShortCaption,
    CallToActionContent CallToAction,
    ContactInformation Contact,
    IReadOnlyList<string> Hashtags,
    VerifiedDetails VerifiedDetails,
    bool ResearchSuccessful,
    string WarningMessage);

public sealed record SaleCreativeResult(
    string CreativeAngle,
    string Headline,
    string Opening,
    IReadOnlyList<SaleCreativeHighlight> Highlights,
    string ProductNotice,
    string Closing,
    CallToActionContent CallToAction,
    IReadOnlyList<string> Hashtags);

public sealed record SaleCreativeHighlight(string Icon, string Title, string Description);

public sealed record ContentBlock(
    string Type,
    string Title,
    string Text,
    IReadOnlyList<HighlightItem> Items);

public sealed record HighlightItem(string Icon, string BenefitTitle, string Text);

public sealed record CallToActionContent(string Icon, string Text);

public sealed record ContactInformation(string ShopName, string Phone, string Address, string Website);

public sealed record VerifiedDetails(
    IReadOnlyList<VerifiedClaim> Claims,
    IReadOnlyList<string> Usage,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<GroundedSource> Sources);

public sealed record VerifiedClaim(
    string Claim,
    string SourceUrl,
    string SourceTitle,
    string MatchedProduct,
    string MatchedVariant,
    int Confidence);

public sealed record GroundedSource(
    string Website,
    string Title,
    string Url,
    string SourceType = "",
    int Confidence = 0,
    IReadOnlyList<string>? MatchedFields = null);

public sealed record RecentSaleContent(string Headline, string Opening, string CallToAction);

public sealed record VisibleTextCorrection(
    bool HasCorrection,
    string ProductName,
    string Brand,
    string Variant,
    string Category,
    string Size);

public sealed record OfficialProductSource(string Website, string Title, string Url, string Content);

public sealed record OfficialPageReadResult(bool Success, OfficialProductSource? Source, string Message, bool MayAcceptByUrlMatch)
{
    public static OfficialPageReadResult Ok(OfficialProductSource source) => new(true, source, "", false);
    public static OfficialPageReadResult Fail(string message, bool mayAcceptByUrlMatch = true) => new(false, null, message, mayAcceptByUrlMatch);
}

public sealed record AiServiceResult<T>(bool Success, T? Data, string Message, int StatusCode)
{
    public static AiServiceResult<T> Ok(T data) => new(true, data, "", 200);
    public static AiServiceResult<T> Fail(int statusCode, string message) => new(false, default, message, statusCode);
}

public sealed record GeminiCallResult(bool Success, string Text, string RawJson, string Message, int StatusCode)
{
    public static GeminiCallResult Ok(string text, string rawJson) => new(true, text, rawJson, "", 200);
    public static GeminiCallResult Fail(int statusCode, string message, string rawJson = "") => new(false, "", rawJson, message, statusCode);
}

public sealed class GeminiQuotaExceededException(string message, int? retryAfterSeconds = null) : Exception(message)
{
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}

public sealed record ValidatedImage(string MimeType, string Base64);

public sealed record ImageReadResult(bool Success, ValidatedImage? Image, string Message, int StatusCode)
{
    public static ImageReadResult Ok(ValidatedImage image) => new(true, image, "", 200);
    public static ImageReadResult Fail(int statusCode, string message) => new(false, null, message, statusCode);
}

public sealed record TrustedSearchStage(string LogName, IReadOnlyList<string> Domains);

public sealed record TrustedBeautySourceRegistry(
    IReadOnlyList<TrustedBeautyBrand> Brands,
    TrustedRetailerRegistry TrustedRetailers,
    IReadOnlyList<string> BlockedDomains)
{
    public static TrustedBeautySourceRegistry Empty { get; } = new([], TrustedRetailerRegistry.Empty, []);

    public TrustedBeautySourceRegistry Normalize() => this with
    {
        Brands = (Brands ?? [])
            .Where(brand => brand.Enabled)
            .Select(brand => brand.Normalize())
            .Where(brand => !string.IsNullOrWhiteSpace(brand.Brand))
            .ToArray(),
        TrustedRetailers = (TrustedRetailers ?? TrustedRetailerRegistry.Empty).Normalize(),
        BlockedDomains = (BlockedDomains ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
    };
}

public sealed record TrustedBeautyBrand(
    string Brand,
    string[] Aliases,
    string[] OfficialDomains,
    string[] RegionalDomains,
    string[] Categories,
    bool Enabled)
{
    public TrustedBeautyBrand Normalize() => this with
    {
        Brand = Brand?.Trim() ?? "",
        Aliases = NormalizeList(Aliases),
        OfficialDomains = NormalizeDomains(OfficialDomains),
        RegionalDomains = NormalizeDomains(RegionalDomains),
        Categories = NormalizeList(Categories)
    };

    private static string[] NormalizeList(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string[] NormalizeDomains(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().Trim('/').Replace("https://", "", StringComparison.OrdinalIgnoreCase).Replace("http://", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed record TrustedRetailerRegistry(
    string[] Priority,
    string[] DepartmentStores,
    string[] Fallback)
{
    public static TrustedRetailerRegistry Empty { get; } = new([], [], []);

    public string[] AllDomains => Priority
        .Concat(DepartmentStores)
        .Concat(Fallback)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public TrustedRetailerRegistry Normalize() => this with
    {
        Priority = NormalizeDomains(Priority),
        DepartmentStores = NormalizeDomains(DepartmentStores),
        Fallback = NormalizeDomains(Fallback)
    };

    private static string[] NormalizeDomains(IEnumerable<string>? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().Trim('/').Replace("https://", "", StringComparison.OrdinalIgnoreCase).Replace("http://", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public sealed record BrandOfficialProfile(string CanonicalName, string[] Aliases, string[] Domains);

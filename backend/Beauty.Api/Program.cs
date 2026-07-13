using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Beauty.Api.Data;
using Beauty.Api.Endpoints;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var keysDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

builder.Services.AddDbContext<BeautyDbContext>(options =>
{
    var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (useInMemoryDatabase)
    {
        options.UseInMemoryDatabase("BeautyDev");
        return;
    }

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseNpgsql(connectionString);
        return;
    }

    var databasePath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "beauty.db");
    options.UseSqlite($"Data Source={databasePath}");
});

builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<AppointmentAvailabilityService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductMatchScorer>();
builder.Services.AddHttpClient<PublicUrlValidator>();
builder.Services.AddHttpClient<GeminiVisualSearchProvider>();
builder.Services.AddScoped<IVisualSearchProvider>(provider => provider.GetRequiredService<GeminiVisualSearchProvider>());
builder.Services.AddScoped<VisualProductSearchService>();
builder.Services.AddHttpClient<TrustedProductIndexService>();
builder.Services.AddHttpClient<AiDraftService>();
builder.Services.AddHostedService<DatabaseBootstrapper>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var adminAuthOptions = AdminAuthOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(adminAuthOptions);
builder.Services.AddScoped<AdminAuthService>();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddFixedWindowLimiter("uploads", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(10);
        limiter.QueueLimit = 0;
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (adminAuthOptions.IsConfigured)
        {
            options.TokenValidationParameters = AdminAuthService.BuildValidationParameters(adminAuthOptions);
            options.RequireHttpsMetadata = false;
        }
        else
        {
            options.Authority = builder.Configuration["Jwt:Authority"];
            options.Audience = builder.Configuration["Jwt:Audience"];
            options.RequireHttpsMetadata = true;
        }
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Customer", policy => policy.RequireRole(nameof(UserRole.Customer), nameof(UserRole.Staff), nameof(UserRole.Admin)));
    options.AddPolicy("Staff", policy => policy.RequireRole(nameof(UserRole.Staff), nameof(UserRole.Admin)));
    options.AddPolicy("Admin", policy => policy.RequireRole(nameof(UserRole.Admin)));
});

var app = builder.Build();

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

app.MapGet("/api/products", async (BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var products = await db.Products
        .OrderBy(product => product.Name)
        .Select(product => new
        {
            product.Id,
            product.Slug,
            product.Name,
            Price = product.Price,
            SalePrice = product.SaleApproved ? product.SalePrice : null,
            product.Stock,
            product.ImagePath
        })
        .ToListAsync(cancellationToken);
    return Results.Ok(products);
});

app.MapGet("/api/products/{slug}", async (string slug, BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var product = await db.Products
        .Where(item => item.Slug == slug)
        .Select(item => new
        {
            item.Id,
            item.Slug,
            item.Name,
            Price = item.Price,
            SalePrice = item.SaleApproved ? item.SalePrice : null,
            item.Stock,
            item.ImagePath
        })
        .FirstOrDefaultAsync(cancellationToken);

    return product is null ? Results.NotFound() : Results.Ok(product);
});

app.MapPost("/api/appointments", AppointmentEndpoints.CreateAppointmentAsync).RequireRateLimiting("uploads");
app.MapGet("/api/appointments/availability", AppointmentEndpoints.GetAvailabilityAsync).RequireRateLimiting("uploads");

app.MapPost("/api/admin/auth/login", AdminAuthEndpoints.Login).AllowAnonymous().RequireRateLimiting("uploads");

app.MapGet("/api/admin/appointments", AppointmentEndpoints.GetAdminAppointmentsAsync).RequireAuthorization("Staff");

app.MapPut("/api/admin/appointments/{id:guid}/status", AppointmentEndpoints.UpdateAdminAppointmentStatusAsync).RequireAuthorization("Staff");

app.MapPost("/api/orders", async (CreateOrderRequest request, OrderService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateAsync(request, cancellationToken);
    return result.Created ? Results.Created($"/api/orders/{result.Order!.Id}", result.Order) : Results.BadRequest(new { error = result.Message });
});

app.MapGet("/api/admin/orders", AdminOrderEndpoints.GetAsync).RequireAuthorization("Staff");

app.MapPut("/api/admin/orders/{id:guid}/status", AdminOrderEndpoints.UpdateStatusAsync).RequireAuthorization("Staff");

app.MapGet("/api/admin/customers", async (BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var orderCustomers = await db.Orders
        .Select(order => new { order.CustomerName, order.Phone, order.CreatedAt })
        .ToListAsync(cancellationToken);
    var appointmentCustomers = await db.Appointments
        .Select(appointment => new { appointment.CustomerName, appointment.Phone, CreatedAt = appointment.StartAt })
        .ToListAsync(cancellationToken);

    var rows = orderCustomers
        .Concat(appointmentCustomers)
        .Where(item => !string.IsNullOrWhiteSpace(item.Phone))
        .GroupBy(item => item.Phone)
        .Select(group => new
        {
            phone = group.Key,
            name = group.OrderByDescending(item => item.CreatedAt).First().CustomerName,
            visits = group.Count(),
            lastActivityAt = group.Max(item => item.CreatedAt)
        })
        .OrderByDescending(item => item.lastActivityAt)
        .ToList();

    return Results.Ok(rows);
}).RequireAuthorization("Staff");

app.MapGet("/api/admin/dashboard", async (BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var today = DateTimeOffset.Now.Date;
    var allOrders = (await db.Orders.Include(order => order.Items).ToListAsync(cancellationToken))
        .OrderByDescending(order => order.CreatedAt)
        .ToList();
    var allAppointments = (await db.Appointments.ToListAsync(cancellationToken))
        .OrderBy(appointment => appointment.StartAt)
        .ToList();
    var orders = allOrders.Take(8).ToList();
    var appointments = allAppointments.Take(8).ToList();
    var products = await db.Products.OrderByDescending(product => product.Stock).Take(6).ToListAsync(cancellationToken);
    var todayOrders = allOrders.Where(order => order.CreatedAt.ToLocalTime().Date == today).ToList();
    var todayAppointments = allAppointments.Where(appointment => appointment.StartAt.ToLocalTime().Date == today).ToList();

    return Results.Ok(new
    {
        revenueToday = todayOrders.Sum(order => order.Total),
        newOrders = todayOrders.Count,
        appointmentsToday = todayAppointments.Count,
        newCustomers = todayAppointments.Select(appointment => appointment.Phone).Distinct().Count(),
        appointments = appointments.Select(item => new { item.StartAt, item.CustomerName, item.Service, item.Status }),
        bestProducts = products.Select(item => new { item.Name, item.Stock }),
        topCustomers = appointments.GroupBy(item => item.CustomerName).Select(group => new { name = group.Key, count = group.Count() }).Take(5)
    });
}).RequireAuthorization("Staff");

app.MapGet("/api/admin/products", AdminProductEndpoints.GetAsync).RequireAuthorization("Staff");

app.MapPost("/api/admin/products", AdminProductEndpoints.CreateAsync).RequireAuthorization("Admin");

app.MapPut("/api/admin/products/{id:guid}", AdminProductEndpoints.UpdateAsync).RequireAuthorization("Admin");

app.MapDelete("/api/admin/products/{id:guid}", AdminProductEndpoints.DeleteAsync).RequireAuthorization("Admin");

app.MapPost("/api/admin/sales", [Authorize(Policy = "Admin")] async (CreateSaleRequest request, BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var product = await db.Products.FindAsync([request.ProductId], cancellationToken);
    if (product is null)
    {
        return Results.NotFound();
    }

    product.SalePrice = request.SalePrice;
    product.SaleApproved = false;
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { product.Id, product.SalePrice, product.SaleApproved, message = "Giá sale mới chỉ là bản chờ duyệt." });
});

app.MapPost("/api/admin/sales/{productId:guid}/approve", [Authorize(Policy = "Admin")] async (Guid productId, BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var product = await db.Products.FindAsync([productId], cancellationToken);
    if (product is null || product.SalePrice is null)
    {
        return Results.NotFound();
    }

    product.SaleApproved = true;
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(product);
});

app.MapPost("/api/admin/ai-drafts", [Authorize(Policy = "Staff")] async (CreateAiDraftRequest request, ClaimsPrincipal user, AiDraftService service, CancellationToken cancellationToken) =>
{
    var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    var userId = Guid.TryParse(idValue, out var parsed) ? parsed : Guid.Empty;
    var draft = await service.CreateDraftAsync(request, userId, cancellationToken);
    return Results.Created($"/api/admin/ai-drafts/{draft.Id}", draft);
}).RequireRateLimiting("uploads");

app.MapPost("/api/admin/ai-content/identify", async (HttpRequest request, AiDraftService service, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    try
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "Vui lòng gửi dữ liệu dạng form kèm ảnh sản phẩm." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files["image"];
        var result = await service.IdentifyProductAsync(file, cancellationToken);

        return result.Success ? Results.Ok(result.Data) : AiContentFailure(result.Message, result.StatusCode, request.HttpContext.TraceIdentifier);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.Json(new { error = "Yêu cầu nhận diện đã bị trình duyệt hủy trước khi backend xử lý xong." }, statusCode: 499);
    }
    catch (GeminiQuotaExceededException exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogWarning("[CONTENT] Returning HTTP 429. RequestId={RequestId}; Message={Message}", request.HttpContext.TraceIdentifier, exception.Message);
        return GeminiQuotaFailure(exception, request.HttpContext.TraceIdentifier);
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogError(exception, "Identify product image failed.");
        return Results.Json(new { error = $"Backend lỗi khi nhận diện ảnh sản phẩm: {exception.Message}" }, statusCode: 500);
    }
}).RequireAuthorization("Staff").RequireRateLimiting("uploads");

app.MapPost("/api/admin/ai-content/official-url", async (HttpContext context, AiDraftService service, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    try
    {
        ConfirmedProductRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<ConfirmedProductRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Dữ liệu sản phẩm gửi lên không hợp lệ." });
        }

        if (request is null)
        {
            return Results.BadRequest(new { error = "Dữ liệu sản phẩm gửi lên không hợp lệ." });
        }

        var result = await service.FindOfficialProductUrlAsync(request, cancellationToken);
        return result.Success ? Results.Ok(result.Data) : AiContentFailure(result.Message, result.StatusCode, context.TraceIdentifier);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.Json(new { error = "Yêu cầu tìm URL đã bị trình duyệt hủy trước khi backend xử lý xong." }, statusCode: 499);
    }
    catch (GeminiQuotaExceededException exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogWarning("[CONTENT] Returning HTTP 429. RequestId={RequestId}; Message={Message}", context.TraceIdentifier, exception.Message);
        return GeminiQuotaFailure(exception, context.TraceIdentifier);
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogError(exception, "Find official product URL failed.");
        return Results.Json(new { error = $"Backend lỗi khi tìm URL chính hãng: {exception.Message}" }, statusCode: 500);
    }
}).RequireAuthorization("Staff").RequireRateLimiting("uploads");

app.MapPost("/api/admin/ai-content/official-url/image", async (HttpRequest request, AiDraftService service, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    try
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "Vui lòng gửi dữ liệu dạng form kèm ảnh sản phẩm." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var payloadJson = form["payload"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return Results.BadRequest(new { error = "Dữ liệu sản phẩm gửi lên không hợp lệ." });
        }

        ConfirmedProductRequest? productRequest;
        try
        {
            productRequest = JsonSerializer.Deserialize<ConfirmedProductRequest>(
                payloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Dữ liệu sản phẩm gửi lên không hợp lệ." });
        }

        if (productRequest is null)
        {
            return Results.BadRequest(new { error = "Dữ liệu sản phẩm gửi lên không hợp lệ." });
        }

        var file = form.Files["image"];
        var result = await service.FindOfficialProductUrlFromImageAsync(productRequest, file, cancellationToken);
        return result.Success ? Results.Ok(result.Data) : AiContentFailure(result.Message, result.StatusCode, request.HttpContext.TraceIdentifier);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.Json(new { error = "Yêu cầu tìm URL từ ảnh đã bị trình duyệt hủy trước khi backend xử lý xong." }, statusCode: 499);
    }
    catch (GeminiQuotaExceededException exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogWarning("[CONTENT] Returning HTTP 429. RequestId={RequestId}; Message={Message}", request.HttpContext.TraceIdentifier, exception.Message);
        return GeminiQuotaFailure(exception, request.HttpContext.TraceIdentifier);
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogError(exception, "Find official product URL from image failed.");
        return Results.Json(new { error = $"Backend lỗi khi tìm URL chính hãng từ ảnh: {exception.Message}" }, statusCode: 500);
    }
}).RequireAuthorization("Staff").RequireRateLimiting("uploads");

app.MapPost("/api/admin/ai-content/verify-url", async (HttpContext context, AiDraftService service, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    try
    {
        VerifyProductUrlRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<VerifyProductUrlRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Dữ liệu xác minh URL gửi lên không hợp lệ." });
        }

        if (request is null)
        {
            return Results.BadRequest(new { error = "Dữ liệu xác minh URL gửi lên không hợp lệ." });
        }

        var result = await service.VerifyOfficialProductUrlAsync(request, cancellationToken);
        return result.Success ? Results.Ok(result.Data) : AiContentFailure(result.Message, result.StatusCode, context.TraceIdentifier);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.Json(new { error = "Yêu cầu xác minh URL đã bị trình duyệt hủy trước khi backend xử lý xong." }, statusCode: 499);
    }
    catch (GeminiQuotaExceededException exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogWarning("[CONTENT] Returning HTTP 429. RequestId={RequestId}; Message={Message}", context.TraceIdentifier, exception.Message);
        return GeminiQuotaFailure(exception, context.TraceIdentifier);
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogError(exception, "Verify official product URL failed.");
        return Results.Json(new { error = $"Backend lỗi khi xác minh URL sản phẩm: {exception.Message}" }, statusCode: 500);
    }
}).RequireAuthorization("Staff").RequireRateLimiting("uploads");

app.MapPost("/api/admin/ai-content/write", async (HttpContext context, AiDraftService service, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
{
    try
    {
        ConfirmedProductRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<ConfirmedProductRequest>(
                context.Request.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "Dữ liệu sản phẩm gửi lên không hợp lệ." });
        }

        if (request is null)
        {
            return Results.BadRequest(new { error = "Dữ liệu sản phẩm gửi lên không hợp lệ." });
        }

        var result = await service.WriteSaleContentAsync(request, cancellationToken);
        return result.Success ? Results.Ok(result.Data) : AiContentFailure(result.Message, result.StatusCode, context.TraceIdentifier);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        return Results.Json(new { error = "Yêu cầu viết bài sale đã bị trình duyệt hủy trước khi backend xử lý xong." }, statusCode: 499);
    }
    catch (GeminiQuotaExceededException exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogWarning("[CONTENT] Returning HTTP 429. RequestId={RequestId}; Message={Message}", context.TraceIdentifier, exception.Message);
        return GeminiQuotaFailure(exception, context.TraceIdentifier);
    }
    catch (Exception exception)
    {
        loggerFactory.CreateLogger("AiContentEndpoints").LogError(exception, "Write sale content failed.");
        return Results.Json(new { error = $"Backend lỗi khi viết bài sale: {exception.Message}" }, statusCode: 500);
    }
}).RequireAuthorization("Staff").RequireRateLimiting("uploads");

app.MapGet("/api/admin/trusted-product-index/stats", async (TrustedProductIndexService service, CancellationToken cancellationToken) =>
{
    var stats = await service.GetStatsAsync(cancellationToken);
    return Results.Ok(new
    {
        domainCount = stats.DomainCount,
        productCount = stats.ProductCount,
        imageCount = stats.ImageCount,
        lastIndexedAt = stats.LastIndexedAt,
        domains = stats.Domains.Select(domain => new
        {
            domain.Domain,
            domain.Brand,
            domain.SourceType,
            domain.Enabled,
            domain.LastIndexedAt,
            domain.LastStatus,
            domain.LastError
        })
    });
}).RequireAuthorization("Staff");

app.MapPost("/api/admin/trusted-product-index/refresh", async (HttpContext context, TrustedProductIndexService service, CancellationToken cancellationToken) =>
{
    string scope = "all";
    try
    {
        var payload = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(
            context.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);
        if (payload is not null && payload.TryGetValue("scope", out var requestedScope) && !string.IsNullOrWhiteSpace(requestedScope))
        {
            scope = requestedScope.Trim();
        }
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Dữ liệu cập nhật kho không hợp lệ." });
    }

    var job = await service.IndexConfiguredSourcesAsync(scope, cancellationToken);
    return Results.Ok(job);
}).RequireAuthorization("Staff").RequireRateLimiting("uploads");

app.MapPost("/api/admin/trusted-product-index/capture", async (HttpRequest request, TrustedProductIndexService service, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Vui lòng gửi form-data gồm metadata và ảnh sản phẩm." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var metadataJson = form["metadata"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(metadataJson))
    {
        return Results.BadRequest(new { error = "Thiếu metadata sản phẩm." });
    }

    CapturedProductSourceRequest? metadata;
    try
    {
        metadata = JsonSerializer.Deserialize<CapturedProductSourceRequest>(metadataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Metadata sản phẩm không hợp lệ." });
    }

    if (metadata is null)
    {
        return Results.BadRequest(new { error = "Metadata sản phẩm không hợp lệ." });
    }

    try
    {
        var source = await service.CaptureProductSourceAsync(metadata, form.Files["image"], cancellationToken);
        return Results.Ok(new
        {
            source.Id,
            source.ProductName,
            source.Brand,
            source.CanonicalUrl,
            source.SourceDomain,
            source.ImageUrl,
            source.CapturedAt
        });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireAuthorization("Staff").RequireRateLimiting("uploads");

app.MapGet("/api/admin/ai-drafts", [Authorize(Policy = "Staff")] async (BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var drafts = await db.AiDrafts.OrderByDescending(draft => draft.CreatedAt).Take(50).ToListAsync(cancellationToken);
    return Results.Ok(drafts);
});

app.MapPut("/api/admin/ai-drafts/{id:guid}/review", [Authorize(Policy = "Admin")] async (Guid id, ReviewAiDraftRequest request, BeautyDbContext db, CancellationToken cancellationToken) =>
{
    if (request.Status is not DraftStatus.Approved and not DraftStatus.Rejected)
    {
        return Results.BadRequest(new { error = "Bản nháp chỉ có thể được duyệt hoặc từ chối." });
    }

    var draft = await db.AiDrafts.FindAsync([id], cancellationToken);
    if (draft is null)
    {
        return Results.NotFound();
    }

    draft.Status = request.Status;
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(draft);
});

app.MapPost("/api/private/customer-images", [Authorize(Policy = "Staff")] async (IFormFile file, IWebHostEnvironment env, CancellationToken cancellationToken) =>
{
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    var extension = Path.GetExtension(file.FileName);
    if (file.Length is <= 0 or > 5_000_000 || !allowed.Contains(extension))
    {
        return Results.BadRequest(new { error = "File upload không hợp lệ." });
    }

    var privateRoot = Path.Combine(env.ContentRootPath, "PrivateUploads", "customer-images");
    Directory.CreateDirectory(privateRoot);
    var fileName = $"{Guid.NewGuid():N}{extension}";
    var path = Path.Combine(privateRoot, fileName);
    await using var stream = File.Create(path);
    await file.CopyToAsync(stream, cancellationToken);
    return Results.Ok(new { privatePath = path });
}).RequireRateLimiting("uploads");

app.Run();

static IResult AiContentFailure(string message, int statusCode, string requestId)
{
    var code = ResolveAiContentErrorCode(message, statusCode);
    return Results.Json(new
    {
        code,
        message,
        retryAfterSeconds = (int?)null,
        requestId
    }, statusCode: statusCode);
}

static IResult GeminiQuotaFailure(GeminiQuotaExceededException exception, string requestId) =>
    Results.Json(new
    {
        code = "GEMINI_QUOTA_EXCEEDED",
        message = exception.Message,
        retryAfterSeconds = exception.RetryAfterSeconds,
        requestId
    }, statusCode: StatusCodes.Status429TooManyRequests);

static string ResolveAiContentErrorCode(string message, int statusCode)
{
    if (statusCode == StatusCodes.Status429TooManyRequests &&
        message.Contains("Gemini API đã hết hạn mức", StringComparison.OrdinalIgnoreCase))
    {
        return "GEMINI_QUOTA_EXCEEDED";
    }

    if (statusCode == StatusCodes.Status504GatewayTimeout)
    {
        return "REQUEST_TIMEOUT";
    }

    if (statusCode == 499)
    {
        return "USER_CANCELLED";
    }

    if (message.Contains("Không tìm được URL", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("chưa tìm được URL", StringComparison.OrdinalIgnoreCase))
    {
        return "TRUSTED_URL_NOT_FOUND";
    }

    return statusCode == StatusCodes.Status429TooManyRequests ? "RATE_LIMITED" : "AI_CONTENT_ERROR";
}

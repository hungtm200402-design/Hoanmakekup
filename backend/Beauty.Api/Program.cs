using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Beauty.Api.Data;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var keysDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(keysDirectory);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory));

builder.Services.AddDbContext<BeautyDbContext>(options =>
{
    if (builder.Configuration.GetValue<bool>("UseInMemoryDatabase"))
    {
        options.UseInMemoryDatabase("BeautyDev");
        return;
    }

    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<AiDraftService>();
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
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.RequireHttpsMetadata = true;
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

app.MapPost("/api/appointments", async (HttpContext context, AppointmentService service, CancellationToken cancellationToken) =>
{
    CreateAppointmentRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<CreateAppointmentRequest>(
            context.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Dữ liệu đặt lịch không hợp lệ." });
    }

    if (request is null)
    {
        return Results.BadRequest(new { error = "Dữ liệu đặt lịch không hợp lệ." });
    }

    var result = await service.CreateAsync(request, cancellationToken);
    return result.Created ? Results.Created($"/api/appointments/{result.Appointment!.Id}", result.Appointment) : Results.Conflict(new { error = result.Message });
}).RequireRateLimiting("uploads");

app.MapGet("/api/admin/appointments", [Authorize(Policy = "Staff")] async (BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var rows = await db.Appointments.OrderBy(appointment => appointment.StartAt).ToListAsync(cancellationToken);
    return Results.Ok(rows);
});

app.MapPost("/api/orders", async (CreateOrderRequest request, OrderService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateAsync(request, cancellationToken);
    return result.Created ? Results.Created($"/api/orders/{result.Order!.Id}", result.Order) : Results.BadRequest(new { error = result.Message });
});

app.MapGet("/api/admin/orders", [Authorize(Policy = "Staff")] async (BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var rows = await db.Orders.Include(order => order.Items).OrderByDescending(order => order.CreatedAt).ToListAsync(cancellationToken);
    return Results.Ok(rows);
});

app.MapGet("/api/admin/dashboard", async (BeautyDbContext db, CancellationToken cancellationToken) =>
{
    var today = DateTimeOffset.UtcNow.Date;
    var orders = await db.Orders.Include(order => order.Items).OrderByDescending(order => order.CreatedAt).Take(8).ToListAsync(cancellationToken);
    var appointments = await db.Appointments.OrderBy(appointment => appointment.StartAt).Take(8).ToListAsync(cancellationToken);
    var products = await db.Products.OrderByDescending(product => product.Stock).Take(6).ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        revenueToday = orders.Where(order => order.CreatedAt.Date == today).Sum(order => order.Total),
        newOrders = orders.Count,
        appointmentsToday = appointments.Count(appointment => appointment.StartAt.Date == today),
        newCustomers = appointments.Select(appointment => appointment.Phone).Distinct().Count(),
        appointments = appointments.Select(item => new { item.StartAt, item.CustomerName, item.Service, item.Status }),
        bestProducts = products.Select(item => new { item.Name, item.Stock }),
        topCustomers = appointments.GroupBy(item => item.CustomerName).Select(group => new { name = group.Key, count = group.Count() }).Take(5)
    });
});

app.MapPost("/api/admin/products", [Authorize(Policy = "Admin")] async (Product product, BeautyDbContext db, CancellationToken cancellationToken) =>
{
    db.Products.Add(product);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/products/{product.Slug}", product);
});

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

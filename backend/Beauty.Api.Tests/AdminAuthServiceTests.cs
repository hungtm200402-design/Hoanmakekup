using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Beauty.Api.Endpoints;
using Beauty.Api.Models;
using Beauty.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Beauty.Api.Tests;

public sealed class AdminAuthServiceTests
{
    private static readonly AdminAuthOptions Options = new(
        "admin@example.com",
        "local-password",
        "0123456789abcdef0123456789abcdef",
        "Hoanmakekup.Admin",
        "Hoanmakekup.Admin",
        480);

    [Fact]
    public void Login_ReturnsAdminJwtForValidCredentials()
    {
        var service = new AdminAuthService(Options, TimeProvider.System);

        var result = service.Login("admin@example.com", "local-password");

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Token);

        var principal = ValidateToken(result.Token, Options);
        Assert.Equal("admin@example.com", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Contains(principal.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == nameof(UserRole.Admin));
    }

    [Fact]
    public void Login_RejectsInvalidCredentials()
    {
        var service = new AdminAuthService(Options, TimeProvider.System);

        var result = service.Login("admin@example.com", "wrong-password");

        Assert.False(result.Succeeded);
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không đúng.", result.Message);
        Assert.Equal("", result.Token);
    }

    [Fact]
    public void Login_FailsWhenAdminAuthIsNotConfigured()
    {
        var service = new AdminAuthService(new AdminAuthOptions("", "", "", "issuer", "audience", 480), TimeProvider.System);

        var result = service.Login("admin@example.com", "local-password");

        Assert.False(result.Succeeded);
        Assert.Equal("Admin auth chưa được cấu hình.", result.Message);
    }

    [Fact]
    public void TokenValidation_RejectsExpiredToken()
    {
        var service = new AdminAuthService(Options with { ExpiryMinutes = 1 }, new FixedTimeProvider(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        var result = service.Login("admin@example.com", "local-password");
        var parameters = AdminAuthService.BuildValidationParameters(Options);
        parameters.ClockSkew = TimeSpan.Zero;

        Assert.Throws<SecurityTokenExpiredException>(() => new JwtSecurityTokenHandler().ValidateToken(result.Token, parameters, out _));
    }

    [Fact]
    public async Task LoginEndpoint_ReturnsBadRequestForMissingCredentials()
    {
        var service = new AdminAuthService(Options, TimeProvider.System);

        var result = AdminAuthEndpoints.Login(new AdminLoginRequest("", ""), service);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains("Vui lòng nhập tài khoản", response.Body);
    }

    [Fact]
    public async Task LoginEndpoint_ReturnsUnauthorizedForInvalidCredentials()
    {
        var service = new AdminAuthService(Options, TimeProvider.System);

        var result = AdminAuthEndpoints.Login(new AdminLoginRequest("admin@example.com", "wrong-password"), service);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
    }

    private static ClaimsPrincipal ValidateToken(string token, AdminAuthOptions options)
    {
        var parameters = AdminAuthService.BuildValidationParameters(options);
        parameters.ClockSkew = TimeSpan.Zero;
        return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .BuildServiceProvider()
        };
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);
        return (httpContext.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

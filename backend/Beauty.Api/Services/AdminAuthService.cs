using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Beauty.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace Beauty.Api.Services;

public sealed class AdminAuthService(AdminAuthOptions options, TimeProvider clock)
{
    public AdminLoginResult Login(string username, string password)
    {
        if (!options.IsConfigured)
        {
            return AdminLoginResult.Failed("Admin auth chưa được cấu hình.");
        }

        if (!FixedTimeEquals(username, options.Username) || !FixedTimeEquals(password, options.Password))
        {
            return AdminLoginResult.Failed("Tên đăng nhập hoặc mật khẩu không đúng.");
        }

        var expiresAt = clock.GetUtcNow().AddMinutes(options.ExpiryMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, options.Username),
            new Claim(ClaimTypes.Name, options.Username),
            new Claim(ClaimTypes.Role, nameof(UserRole.Admin))
        };
        var credentials = new SigningCredentials(BuildSecurityKey(options.SigningKey), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: clock.GetUtcNow().UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return AdminLoginResult.Success(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public static TokenValidationParameters BuildValidationParameters(AdminAuthOptions options) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = BuildSecurityKey(options.SigningKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

    private static SymmetricSecurityKey BuildSecurityKey(string signingKey) =>
        new(Encoding.UTF8.GetBytes(signingKey));

    private static bool FixedTimeEquals(string actual, string expected)
    {
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }
}

public sealed record AdminAuthOptions(
    string Username,
    string Password,
    string SigningKey,
    string Issuer,
    string Audience,
    int ExpiryMinutes)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !string.IsNullOrWhiteSpace(SigningKey) &&
        Encoding.UTF8.GetByteCount(SigningKey) >= 32;

    public static AdminAuthOptions FromConfiguration(IConfiguration configuration) =>
        new(
            configuration["AdminAuth:Username"] ?? configuration["ADMIN_AUTH_USERNAME"] ?? "",
            configuration["AdminAuth:Password"] ?? configuration["ADMIN_AUTH_PASSWORD"] ?? "",
            configuration["AdminAuth:SigningKey"] ?? configuration["ADMIN_AUTH_SIGNING_KEY"] ?? "",
            configuration["AdminAuth:Issuer"] ?? configuration["ADMIN_AUTH_ISSUER"] ?? "Hoanmakekup.Admin",
            configuration["AdminAuth:Audience"] ?? configuration["ADMIN_AUTH_AUDIENCE"] ?? "Hoanmakekup.Admin",
            int.TryParse(configuration["AdminAuth:ExpiryMinutes"] ?? configuration["ADMIN_AUTH_EXPIRY_MINUTES"], out var minutes) && minutes > 0 ? minutes : 480);
}

public sealed record AdminLoginResult(bool Succeeded, string Message, string Token, DateTimeOffset ExpiresAt)
{
    public static AdminLoginResult Success(string token, DateTimeOffset expiresAt) =>
        new(true, "Đăng nhập thành công.", token, expiresAt);

    public static AdminLoginResult Failed(string message) =>
        new(false, message, "", DateTimeOffset.MinValue);
}

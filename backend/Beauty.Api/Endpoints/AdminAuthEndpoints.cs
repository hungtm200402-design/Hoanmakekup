using Beauty.Api.Models;
using Beauty.Api.Services;

namespace Beauty.Api.Endpoints;

public static class AdminAuthEndpoints
{
    public static IResult Login(AdminLoginRequest request, AdminAuthService service)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Vui lòng nhập tài khoản và mật khẩu admin." });
        }

        var result = service.Login(request.Username.Trim(), request.Password);
        if (!result.Succeeded)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new
        {
            token = result.Token,
            expiresAt = result.ExpiresAt,
            role = nameof(UserRole.Admin)
        });
    }
}

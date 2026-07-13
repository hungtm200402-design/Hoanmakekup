using System.Net;
using System.Net.Sockets;

namespace Beauty.Api.Services;

public sealed class PublicUrlValidator(HttpClient httpClient)
{
    private static readonly IPAddress[] MetadataAddresses =
    [
        IPAddress.Parse("169.254.169.254"),
        IPAddress.Parse("100.100.100.200")
    ];

    public async Task<PublicUrlValidationResult> ValidateAsync(string value, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate((value ?? "").Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return PublicUrlValidationResult.Fail("URL phải bắt đầu bằng http:// hoặc https://.");
        }

        var hostResult = await ValidateHostAsync(uri.Host, cancellationToken);
        if (!hostResult.Success)
        {
            return hostResult;
        }

        using var request = new HttpRequestMessage(HttpMethod.Head, uri);
        request.Headers.UserAgent.ParseAdd("HoanMakeupAdmin/1.0");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.Headers.Location is { } location)
        {
            var redirectUri = location.IsAbsoluteUri ? location : new Uri(uri, location);
            var redirectHostResult = await ValidateHostAsync(redirectUri.Host, cancellationToken);
            if (!redirectHostResult.Success)
            {
                return PublicUrlValidationResult.Fail("URL redirect sang địa chỉ nội bộ hoặc không an toàn.");
            }
        }

        return PublicUrlValidationResult.Ok(uri);
    }

    private static async Task<PublicUrlValidationResult> ValidateHostAsync(string host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host) ||
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return PublicUrlValidationResult.Fail("Không được dùng URL localhost hoặc host nội bộ.");
        }

        if (IPAddress.TryParse(host, out var directAddress))
        {
            return IsPublicAddress(directAddress)
                ? PublicUrlValidationResult.Ok(null)
                : PublicUrlValidationResult.Fail("Không được dùng IP nội bộ, loopback hoặc metadata.");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch
        {
            return PublicUrlValidationResult.Fail("Không phân giải được domain URL.");
        }

        return addresses.Length > 0 && addresses.All(IsPublicAddress)
            ? PublicUrlValidationResult.Ok(null)
            : PublicUrlValidationResult.Fail("Domain URL trỏ tới IP nội bộ hoặc không an toàn.");
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || MetadataAddresses.Contains(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] != 10 &&
                bytes[0] != 127 &&
                !(bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) &&
                !(bytes[0] == 192 && bytes[1] == 168) &&
                !(bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal &&
                !address.IsIPv6SiteLocal &&
                !address.IsIPv6Multicast &&
                !address.Equals(IPAddress.IPv6Loopback);
        }

        return false;
    }
}

public sealed record PublicUrlValidationResult(bool Success, Uri? Uri, string Message)
{
    public static PublicUrlValidationResult Ok(Uri? uri) => new(true, uri, "");
    public static PublicUrlValidationResult Fail(string message) => new(false, null, message);
}

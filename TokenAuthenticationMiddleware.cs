using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

public sealed class TokenAuthenticationMiddleware(
    RequestDelegate next,
    byte[] expectedToken)
{
    internal static readonly object RejectedRequestKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(authorization, out var authorizationHeader)
            || !string.Equals(authorizationHeader.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorizationHeader.Parameter))
        {
            await RejectRequestAsync(context);
            return;
        }

        var suppliedToken = Encoding.UTF8.GetBytes(authorizationHeader.Parameter);
        if (suppliedToken.Length != expectedToken.Length
            || !CryptographicOperations.FixedTimeEquals(suppliedToken, expectedToken))
        {
            await RejectRequestAsync(context);
            return;
        }

        await next(context);
    }

    private async Task RejectRequestAsync(HttpContext context)
    {
        context.Items[RejectedRequestKey] = true;
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = "Bearer";
        await next(context);
    }
}
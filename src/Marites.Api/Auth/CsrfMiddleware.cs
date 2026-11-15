using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Marites.Api.Auth;

/// <summary>
/// Refuses a cookie-authenticated write that does not carry the CSRF token.
/// </summary>
/// <remarks>
/// `SameSite=Lax` already stops a cross-site POST in current browsers; this is the control that
/// does not depend on the browser behaving. The token is compared against both the readable
/// cookie and the signed session claim, so a page that can set a cookie still cannot forge the
/// pair. Bearer-authenticated requests are exempt: a header token is not attached automatically,
/// which is the whole reason CSRF exists for cookies and not for them.
/// </remarks>
public sealed class CsrfMiddleware(RequestDelegate next, AuthOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (options.IsGoogle && IsWrite(context.Request.Method) && AuthenticatedByCookie(context))
        {
            var header = context.Request.Headers[AuthOptions.CsrfHeader].ToString();
            var cookie = context.Request.Cookies[AuthOptions.CsrfCookie];
            var session = context.User.FindFirst(MaritesAuthenticationHandler.CsrfClaim)?.Value;

            if (string.IsNullOrEmpty(header)
                || !Matches(header, cookie)
                || !Matches(header, session))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                    title = "The CSRF token is missing or does not match",
                    status = StatusCodes.Status403Forbidden,
                    detail = "Reload the session and retry. Nothing was executed.",
                    code = "ERR-AUTH-002",
                    outcome = "Denied",
                    retryable = false,
                    remedy = "Read the csrfToken from GET /auth/session and send it as X-CSRF-Token.",
                });

                return;
            }
        }

        await next(context);
    }

    private static bool AuthenticatedByCookie(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && context.User.FindFirst(MaritesAuthenticationHandler.AuthMethodClaim)?.Value == "cookie";

    private static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static bool Matches(string header, string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(header),
            Encoding.UTF8.GetBytes(candidate));
    }
}

using System.Security.Claims;
using System.Text.Json.Serialization;
using Marites.Application.Access;
using Marites.Domain.Access;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Marites.Api.Auth;

/// <summary>OP-AUTH-001…003 — Google sign-in, session read, logout.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes, AuthOptions options)
    {
        var auth = routes.MapGroup("/auth").WithTags("Authentication");

        // Sign-in and session are the only anonymous operations in the API: one is how a
        // caller obtains a credential, the other is how it asks whether it has one.
        auth.MapPost("/google", (HttpContext context, GoogleSignInBody body, IdentityService identity, CancellationToken cancellationToken) =>
                SignInAsync(context, body, identity, options, cancellationToken))
            .WithName("signInWithGoogle")
            .AllowAnonymous();

        auth.MapGet("/session", (HttpContext context, IdentityService identity, CancellationToken cancellationToken) =>
                SessionAsync(context, identity, options, cancellationToken))
            .WithName("getSession")
            .AllowAnonymous();

        auth.MapPost("/logout", (HttpContext context, IdentityService identity, CancellationToken cancellationToken) =>
                LogoutAsync(context, identity, options, cancellationToken))
            .WithName("signOut")
            .RequireAuthorization("CanRead");

        return routes;
    }

    private static async Task<IResult> SignInAsync(
        HttpContext context,
        GoogleSignInBody body,
        IdentityService identity,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        // Served only in google mode: an endpoint that cannot work should not appear to exist.
        if (!options.IsGoogle)
        {
            return Results.NotFound();
        }

        var outcome = await identity.SignInAsync(body.IdToken, cancellationToken);

        switch (outcome.Status)
        {
            case SignInStatus.InvalidToken:
                return Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "The Google ID token is not valid",
                    detail: "The token failed signature, issuer, audience or expiry validation. Sign in again.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "ERR-AUTH-001",
                        ["outcome"] = "Denied",
                        ["retryable"] = false,
                    });

            case SignInStatus.Unmapped:
                // The identity is real; the permission is not. The caller's own subject is
                // returned so an operator can grant it — it is not a secret, and it is the one
                // piece of information enrolment needs.
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "This Google account has no Marites role",
                    detail: $"Sign-in succeeded, but the identity '{outcome.Subject}' is not mapped to a role. "
                        + "Ask an administrator to grant it with 'marites identity grant'.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "ERR-AUTH-002",
                        ["outcome"] = "Denied",
                        ["retryable"] = false,
                        ["subject"] = outcome.Subject,
                    });

            default:
                var principal = outcome.Principal!;

                context.Response.Cookies.Append(
                    AuthOptions.SessionCookie,
                    outcome.Token!,
                    CookieOptions(context, options, httpOnly: true, principal.ExpiresAt));

                // Readable by the client on purpose: it must echo this value in X-CSRF-Token.
                context.Response.Cookies.Append(
                    AuthOptions.CsrfCookie,
                    principal.CsrfToken,
                    CookieOptions(context, options, httpOnly: false, principal.ExpiresAt));

                return Results.Ok(View(principal));
        }
    }

    private static async Task<IResult> SessionAsync(
        HttpContext context,
        IdentityService identity,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        if (options.IsNone)
        {
            // No session exists to read; the implicit local Admin is the identity.
            var now = DateTimeOffset.UtcNow;
            return Results.Ok(View(new SessionPrincipal(
                Guid.Empty, "local", AppRoles.Admin, now, now.AddHours(options.SessionHours), string.Empty)));
        }

        var session = await identity.ReadSessionAsync(
            context.Request.Cookies[AuthOptions.SessionCookie], cancellationToken);

        return session is null
            ? Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No valid session",
                detail: "The session is missing, expired or was revoked. Sign in again.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "ERR-AUTH-001",
                    ["outcome"] = "Denied",
                    ["retryable"] = false,
                })
            : Results.Ok(View(session));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IdentityService identity,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        if (options.IsNone)
        {
            return Results.NoContent();
        }

        var session = await identity.ReadSessionAsync(
            context.Request.Cookies[AuthOptions.SessionCookie], cancellationToken);

        if (session is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No session to end",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "ERR-AUTH-001",
                    ["outcome"] = "Denied",
                    ["retryable"] = false,
                });
        }

        // Revoking every session this user holds is the behaviour a lost device needs; the
        // cookie is cleared as well so the browser stops sending it.
        await identity.LogoutAsync(session.UserId, cancellationToken);

        context.Response.Cookies.Delete(AuthOptions.SessionCookie, CookieOptions(context, options, true, null));
        context.Response.Cookies.Delete(AuthOptions.CsrfCookie, CookieOptions(context, options, false, null));

        return Results.NoContent();
    }

    private static CookieOptions CookieOptions(
        HttpContext context, AuthOptions options, bool httpOnly, DateTimeOffset? expires) => new()
    {
        HttpOnly = httpOnly,
        SameSite = SameSiteMode.Lax,
        Secure = options.SecureCookie || context.Request.IsHttps,
        Path = "/",
        Expires = expires,
    };

    private static SessionView View(SessionPrincipal principal) => new(
        new AuthenticatedUserView(
            principal.UserId,
            principal.Email,
            principal.Role,
            Marites.Application.Access.Capabilities.ForRole(principal.Role)),
        principal.CsrfToken,
        principal.ExpiresAt);

    public sealed record GoogleSignInBody(string IdToken);

    public sealed record AuthenticatedUserView(
        Guid Id,
        string Email,
        string Role,
        [property: JsonPropertyName("capabilities")] IReadOnlyList<string> Capabilities);

    public sealed record SessionView(
        AuthenticatedUserView User,
        string CsrfToken,
        DateTimeOffset ExpiresAt);
}

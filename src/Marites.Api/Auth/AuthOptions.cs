namespace Marites.Api.Auth;

/// <summary>
/// The identity mode and what each mode needs (ADR-008 amendment, configuration reference §2).
/// </summary>
public sealed record AuthOptions(
    string Mode,
    string? GoogleClientId,
    string? EntraTenantId,
    string? EntraAudience,
    int SessionHours,
    bool SecureCookie)
{
    public const string None = "none";
    public const string Entra = "entra";
    public const string Google = "google";

    public const string SessionCookie = "marites_session";
    public const string CsrfCookie = "marites_csrf";
    public const string CsrfHeader = "X-CSRF-Token";

    public bool IsGoogle => Mode == Google;
    public bool IsNone => Mode == None;

    public static AuthOptions FromEnvironment(Func<string, string?> read, bool secureByDefault)
    {
        var mode = (read("MARITES_AUTH_MODE") ?? Entra).Trim().ToLowerInvariant();
        var url = read("MARITES_URL");
        var secure = secureByDefault || (url?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ?? false);

        var hours = int.TryParse(read("MARITES_SESSION_HOURS"), out var parsed) ? parsed : 8;

        return new AuthOptions(
            mode,
            read("MARITES_GOOGLE_CLIENT_ID"),
            read("MARITES_ENTRA_TENANT_ID"),
            read("MARITES_ENTRA_AUDIENCE"),
            Math.Clamp(hours, 1, 72),
            secure);
    }

    /// <summary>
    /// Refuses a configuration that cannot work, at startup rather than at the first request.
    /// </summary>
    public void Validate(string listenAddress)
    {
        if (Mode is not (None or Entra or Google))
        {
            throw new InvalidOperationException(
                $"MARITES_AUTH_MODE='{Mode}' is not a mode. Use entra, google or none (ADR-008).");
        }

        if (IsNone && !IsLoopback(listenAddress))
        {
            throw new InvalidOperationException(
                "MARITES_AUTH_MODE=none is accepted only on a loopback address. A reachable install " +
                "without identity is an open broker console; bind to 127.0.0.1 or configure entra/google.");
        }

        if (IsGoogle && string.IsNullOrWhiteSpace(GoogleClientId))
        {
            throw new InvalidOperationException(
                "MARITES_AUTH_MODE=google requires MARITES_GOOGLE_CLIENT_ID — the public client ID from " +
                "Google Cloud Console. The client secret is not used and must not be configured.");
        }

        if (Mode == Entra && (string.IsNullOrWhiteSpace(EntraTenantId) || string.IsNullOrWhiteSpace(EntraAudience)))
        {
            throw new InvalidOperationException(
                "MARITES_AUTH_MODE=entra requires MARITES_ENTRA_TENANT_ID and MARITES_ENTRA_AUDIENCE.");
        }
    }

    /// <summary>
    /// Every configured listener must be loopback. The caller passes what it actually binds —
    /// either <c>host:port</c> or one or more absolute URLs — because a guard that reads the
    /// wrong string is a guard that silently passes.
    /// </summary>
    private static bool IsLoopback(string listenAddress) =>
        listenAddress.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .All(address => IsSingleLoopback(address.Trim()));

    private static bool IsSingleLoopback(string address)
    {
        var host = address;

        if (address.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                return false;
            }

            host = uri.Host;
        }
        else if (address.StartsWith('['))
        {
            var end = address.IndexOf(']');
            host = end > 0 ? address[1..end] : address;
        }
        else if (address.Contains(':', StringComparison.Ordinal))
        {
            host = address.Split(':')[0];
        }

        return host is "127.0.0.1" or "localhost" or "::1" or "[::1]";
    }
}

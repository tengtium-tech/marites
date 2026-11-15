namespace Marites.Domain.Access;

/// <summary>
/// A person, mapped to one of the three roles. The mapping is explicit: the verified
/// <c>(issuer, subject)</c> pair is the identity, and a provider identity with no row here has
/// no access (ADR-008, VOCABULARY §18).
/// </summary>
public sealed class AppUser
{
    private AppUser()
    {
        // Materialisation only.
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>
    /// The identity provider's identifier — <c>accounts.google.com</c> for Google, the tenant
    /// issuer for Entra. Null for the break-glass admin, which exists only locally.
    /// </summary>
    public string? Issuer { get; private set; }

    /// <summary>
    /// The provider's stable identifier for the person — Google's <c>sub</c>, the Entra object
    /// id. Null for the break-glass admin. This is the identity; <see cref="Email"/> is display
    /// data and never decides access.
    /// </summary>
    public string? Subject { get; private set; }

    public string Email { get; private set; } = string.Empty;

    /// <summary><c>Viewer</c>, <c>Support</c> or <c>Admin</c> (VOCABULARY §2).</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Disabled by default, enabled by environment variable, and every use is audited.</summary>
    public bool IsBreakGlass { get; private set; }

    /// <summary>
    /// Set by logout. Every session issued before this instant is invalid, so signing out on one
    /// device signs the person out everywhere.
    /// </summary>
    public DateTimeOffset? SessionsRevokedAt { get; private set; }

    public static AppUser CreateMapped(
        Guid tenantId,
        string issuer,
        string subject,
        string email,
        string role) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Issuer = issuer,
        Subject = subject,
        Email = email,
        Role = role,
        IsBreakGlass = false,
        SessionsRevokedAt = null,
    };

    public void ChangeRole(string role) => Role = role;

    public void ChangeEmail(string email) => Email = email;

    public void RevokeSessions(DateTimeOffset now) => SessionsRevokedAt = now;
}

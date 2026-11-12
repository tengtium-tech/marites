namespace Marites.Domain.Access;

/// <summary>
/// A person, mapped to one of the three roles. Sessions and tokens belong to Entra ID; this
/// row holds only what Marites must know about the person (ADR-008, VOCABULARY §2).
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
    /// The Entra object id. Nullable because the break-glass admin is a local account with no
    /// directory identity — the one user that must exist when Entra is unreachable.
    /// </summary>
    public string? EntraObjectId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    /// <summary><c>Viewer</c>, <c>Support</c> or <c>Admin</c> (VOCABULARY §2).</summary>
    public string Role { get; private set; } = string.Empty;

    /// <summary>Disabled by default, enabled by environment variable, and every use is audited.</summary>
    public bool IsBreakGlass { get; private set; }
}

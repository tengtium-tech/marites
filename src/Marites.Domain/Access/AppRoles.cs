namespace Marites.Domain.Access;

/// <summary>The three roles of DOC-UX-001 §2. Both identity providers map into these values.</summary>
public static class AppRoles
{
    public const string Viewer = "Viewer";
    public const string Support = "Support";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Viewer, Support, Admin];

    public static bool IsKnown(string role) =>
        All.Contains(role, StringComparer.Ordinal);
}

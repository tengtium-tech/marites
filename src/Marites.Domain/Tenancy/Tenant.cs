namespace Marites.Domain.Tenancy;

/// <summary>
/// The isolation boundary. One row per team inside one organisation (ADR-014), not per
/// customer — this product is self-hosted software, not a hosted service.
/// </summary>
public sealed class Tenant
{
    private Tenant()
    {
        // Materialisation only.
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Unique, lowercase, hyphenated. Used in URLs and in the shadow-queue prefix.</summary>
    public string Slug { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Tenant Create(string name, string slug, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A tenant needs a name.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new ArgumentException("A tenant needs a slug.", nameof(slug));
        }

        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            CreatedAt = now.ToUniversalTime(),
        };
    }
}

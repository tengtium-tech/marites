using System.Globalization;
using Marites.Domain.Access;
using Marites.Domain.Audit;
using Marites.Domain.Brokers;
using Marites.Domain.Idempotency;
using Marites.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Marites.Infrastructure.Bookkeeping;

/// <summary>
/// Marites' own store: configuration, roles, audit, operation evidence. Embedded SQLite,
/// created at install, always present (ADR-022).
/// </summary>
/// <remarks>
/// Column names follow the data dictionary, and the type mapping is the one DEC-020 fixes:
/// <c>Guid</c> as 36-character lowercase TEXT, timestamps as ISO-8601 TEXT with seven
/// fractional digits, booleans as INTEGER. The conversions below are explicit rather than
/// left to the provider's defaults, because the provider writes <c>DateTimeOffset</c> with a
/// space separator and no <c>Z</c> — which sorts correctly in .NET but is not what the
/// dictionary promises, and not what a range query written by hand would expect.
/// </remarks>
public sealed class BookkeepingDbContext : DbContext
{
    public BookkeepingDbContext(DbContextOptions<BookkeepingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<BrokerProfile> BrokerProfiles => Set<BrokerProfile>();

    public DbSet<OperationAudit> OperationAudits => Set<OperationAudit>();

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    /// <summary>36-character lowercase hyphenated, so a value read in a SQLite browser is legible.</summary>
    public static readonly ValueConverter<Guid, string> GuidToText = new(
        value => value.ToString("D", CultureInfo.InvariantCulture),
        text => Guid.Parse(text));

    /// <summary>ISO-8601 UTC with seven fractional digits: sorts lexicographically, which every range query relies on.</summary>
    public static readonly ValueConverter<DateTimeOffset, string> TimestampToText = new(
        value => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture),
        text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    /// <summary>SQLite has no boolean; 0 and 1 are the whole vocabulary.</summary>
    public static readonly ValueConverter<bool, long> BooleanToInteger = new(
        value => value ? 1L : 0L,
        stored => stored != 0L);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(tenant =>
        {
            tenant.ToTable("tenant");
            tenant.HasKey(t => t.Id);

            tenant.Property(t => t.Id).HasColumnName("id").HasConversion(GuidToText);
            tenant.Property(t => t.Name).HasColumnName("name").IsRequired();
            tenant.Property(t => t.Slug).HasColumnName("slug").IsRequired();
            tenant.Property(t => t.CreatedAt).HasColumnName("created_at").HasConversion(TimestampToText);

            tenant.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<AppUser>(user =>
        {
            user.ToTable("app_user");
            user.HasKey(u => u.Id);

            user.Property(u => u.Id).HasColumnName("id").HasConversion(GuidToText);
            user.Property(u => u.TenantId).HasColumnName("tenant_id").HasConversion(GuidToText);
            user.Property(u => u.EntraObjectId).HasColumnName("entra_object_id");
            user.Property(u => u.Email).HasColumnName("email").IsRequired();
            user.Property(u => u.Role).HasColumnName("role").IsRequired();
            user.Property(u => u.IsBreakGlass).HasColumnName("is_break_glass").HasConversion(BooleanToInteger);

            user.HasIndex(u => u.TenantId);
        });

        modelBuilder.Entity<BrokerProfile>(profile =>
        {
            profile.ToTable("broker_profile");
            profile.HasKey(p => p.Id);

            profile.Property(p => p.Id).HasColumnName("id").HasConversion(GuidToText);
            profile.Property(p => p.TenantId).HasColumnName("tenant_id").HasConversion(GuidToText);
            profile.Property(p => p.Name).HasColumnName("name").IsRequired();
            profile.Property(p => p.ProviderKind).HasColumnName("provider_kind").IsRequired();
            profile.Property(p => p.Environment).HasColumnName("environment").IsRequired();
            profile.Property(p => p.EndpointEnc).HasColumnName("endpoint_enc").IsRequired();
            profile.Property(p => p.AdminUri).HasColumnName("admin_uri");
            profile.Property(p => p.CredentialKind).HasColumnName("credential_kind").IsRequired();
            profile.Property(p => p.CredentialsEnc).HasColumnName("credentials_enc");
            profile.Property(p => p.CredentialsUpdatedAt).HasColumnName("credentials_updated_at").HasConversion(TimestampToText);
            profile.Property(p => p.NamespaceOrVhost).HasColumnName("namespace_or_vhost");
            profile.Property(p => p.TlsOpts).HasColumnName("tls_opts").IsRequired();
            profile.Property(p => p.CapabilitiesJson).HasColumnName("capabilities_json").IsRequired();
            profile.Property(p => p.IsDefault).HasColumnName("is_default").HasConversion(BooleanToInteger);
            profile.Property(p => p.PreflightState).HasColumnName("preflight_state").IsRequired();
            profile.Property(p => p.PreflightAt).HasColumnName("preflight_at").HasConversion(TimestampToText);
            profile.Property(p => p.Revision).HasColumnName("revision").IsRequired();

            profile.HasIndex(p => new { p.TenantId, p.Name }).IsUnique();
        });

        modelBuilder.Entity<OperationAudit>(audit =>
        {
            audit.ToTable("operation_audit");
            audit.HasKey(a => a.Id);

            audit.Property(a => a.Id).HasColumnName("id").HasConversion(GuidToText);
            audit.Property(a => a.TenantId).HasColumnName("tenant_id").HasConversion(GuidToText);
            audit.Property(a => a.ActorId).HasColumnName("actor_id").HasConversion(GuidToText);
            audit.Property(a => a.ActorKind).HasColumnName("actor_kind").IsRequired();
            audit.Property(a => a.OnBehalfOf).HasColumnName("on_behalf_of").HasConversion(GuidToText);
            audit.Property(a => a.ProposalId).HasColumnName("proposal_id").HasConversion(GuidToText);
            audit.Property(a => a.Action).HasColumnName("action").IsRequired();
            audit.Property(a => a.Target).HasColumnName("target").IsRequired();
            audit.Property(a => a.ParamsJson).HasColumnName("params_json").IsRequired();
            audit.Property(a => a.Result).HasColumnName("result").IsRequired();
            audit.Property(a => a.MessageCount).HasColumnName("message_count");
            audit.Property(a => a.PayloadHash).HasColumnName("payload_hash");
            audit.Property(a => a.PayloadEdited).HasColumnName("payload_edited").HasConversion(BooleanToInteger);
            audit.Property(a => a.Via).HasColumnName("via");
            audit.Property(a => a.CreatedAt).HasColumnName("created_at").HasConversion(TimestampToText);
            audit.Property(a => a.Ip).HasColumnName("ip");

            audit.HasIndex(a => new { a.TenantId, a.CreatedAt });
        });

        modelBuilder.Entity<IdempotencyKey>(record =>
        {
            record.ToTable("idempotency_key");
            record.HasKey(r => r.Id);

            record.Property(r => r.Id).HasColumnName("id").HasConversion(GuidToText);
            record.Property(r => r.TenantId).HasColumnName("tenant_id").HasConversion(GuidToText);
            record.Property(r => r.Key).HasColumnName("key").IsRequired();
            record.Property(r => r.Operation).HasColumnName("operation").IsRequired();
            record.Property(r => r.RequestHash).HasColumnName("request_hash").IsRequired();
            record.Property(r => r.ResponseStatus).HasColumnName("response_status").IsRequired();
            record.Property(r => r.ResponseJson).HasColumnName("response_json").IsRequired();
            record.Property(r => r.CreatedAt).HasColumnName("created_at").HasConversion(TimestampToText);

            // One key means one outcome per command. A repeat is answered from this row, and a
            // second row for the same key would let two different outcomes both claim to be it.
            record.HasIndex(r => new { r.TenantId, r.Key, r.Operation }).IsUnique();
            record.HasIndex(r => r.CreatedAt);
        });
    }
}

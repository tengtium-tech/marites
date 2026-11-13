using Marites.Application.Access;
using Marites.Domain.Access;
using Marites.Infrastructure.Bookkeeping;
using Microsoft.EntityFrameworkCore;

namespace Marites.Infrastructure.Access;

/// <summary>
/// The explicit role mapping, stored in <c>app_user</c>.
/// </summary>
/// <remarks>
/// There is deliberately no "create on first sign-in". A row exists because an operator created
/// it, which is the difference between a product that admits a workspace and one that admits the
/// people it was told to.
/// </remarks>
public sealed class EfUserDirectory(BookkeepingDbContext context) : IUserDirectory
{
    public Task<AppUser?> FindBySubjectAsync(
        string issuer, string subject, CancellationToken cancellationToken) =>
        context.AppUsers.FirstOrDefaultAsync(
            user => user.Issuer == issuer && user.Subject == subject, cancellationToken);

    public Task<AppUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        context.AppUsers.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken cancellationToken) =>
        await context.AppUsers.OrderBy(user => user.Email).ToListAsync(cancellationToken);

    public async Task<AppUser> GrantAsync(
        string issuer, string subject, string email, string role, CancellationToken cancellationToken)
    {
        var existing = await FindBySubjectAsync(issuer, subject, cancellationToken);

        if (existing is not null)
        {
            existing.ChangeRole(role);
            existing.ChangeEmail(email);
            await context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var tenantId = await context.Tenants.Select(tenant => tenant.Id).FirstAsync(cancellationToken);
        var user = AppUser.CreateMapped(tenantId, issuer, subject, email, role);

        context.AppUsers.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<bool> RevokeAsync(string issuer, string subject, CancellationToken cancellationToken)
    {
        var user = await FindBySubjectAsync(issuer, subject, cancellationToken);

        if (user is null)
        {
            return false;
        }

        context.AppUsers.Remove(user);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RevokeSessionsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await FindByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return;
        }

        user.RevokeSessions(now);
        await context.SaveChangesAsync(cancellationToken);
    }
}

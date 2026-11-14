using Marites.Application.Access;
using Marites.Domain.Access;
using Marites.Infrastructure.Access;
using Marites.Infrastructure.Bookkeeping;
using Microsoft.EntityFrameworkCore;

namespace Marites.Host;

/// <summary>
/// <c>marites identity</c> — the explicit role mapping, managed by an operator.
/// </summary>
/// <remarks>
/// Deliberately a CLI rather than an endpoint: granting a role is the moment an install decides
/// who may see production traffic, and it should be a deliberate action on the host rather than
/// a request a compromised session could make. It writes the same <c>app_user</c> row the API
/// reads, so there is one source of truth.
/// </remarks>
public static class IdentityCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            return Usage("A subcommand is required: grant, list or revoke.");
        }

        var path = Environment.GetEnvironmentVariable("MARITES_BOOKKEEPING_PATH") ?? "./data/marites.db";
        var options = new DbContextOptionsBuilder<BookkeepingDbContext>()
            .UseSqlite($"Data Source={Path.GetFullPath(path)}")
            .Options;

        await using var context = new BookkeepingDbContext(options);
        var users = new EfUserDirectory(context);

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "grant" => await GrantAsync(users, args[1..]),
                "list" => await ListAsync(users),
                "revoke" => await RevokeAsync(users, args[1..]),
                _ => Usage($"Unknown subcommand '{args[0]}'. Use grant, list or revoke."),
            };
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static async Task<int> GrantAsync(IUserDirectory users, string[] args)
    {
        var values = Parse(args);

        var issuer = NormalizeIssuer(Require(values, "issuer"));
        var subject = Require(values, "subject");
        var email = Require(values, "email");
        var role = Require(values, "role");

        if (!AppRoles.IsKnown(role))
        {
            return Usage($"'{role}' is not a role. Use Viewer, Support or Admin.");
        }

        var user = await users.GrantAsync(issuer, subject, email, role, CancellationToken.None);

        Console.WriteLine($"Granted {role} to {email} ({issuer} / {subject}); user id {user.Id}.");
        return 0;
    }

    private static async Task<int> ListAsync(IUserDirectory users)
    {
        var all = await users.ListAsync(CancellationToken.None);

        if (all.Count == 0)
        {
            Console.WriteLine("No identity mappings. A provider sign-in with no mapping is refused.");
            return 0;
        }

        foreach (var user in all)
        {
            var revoked = user.SessionsRevokedAt is { } at ? $" sessions revoked {at:u}" : string.Empty;
            Console.WriteLine($"{user.Role,-8} {user.Email,-40} {user.Issuer} / {user.Subject}{revoked}");
        }

        return 0;
    }

    private static async Task<int> RevokeAsync(IUserDirectory users, string[] args)
    {
        var values = Parse(args);
        var issuer = NormalizeIssuer(Require(values, "issuer"));
        var subject = Require(values, "subject");

        var removed = await users.RevokeAsync(issuer, subject, CancellationToken.None);

        if (!removed)
        {
            Console.Error.WriteLine($"No mapping for {issuer} / {subject}.");
            return 1;
        }

        Console.WriteLine($"Revoked the mapping for {issuer} / {subject}.");
        return 0;
    }

    private static string NormalizeIssuer(string issuer) =>
        issuer == GoogleIdentityProvider.IssuerShort ? GoogleIdentityProvider.Issuer : issuer;

    private static Dictionary<string, string> Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var name = args[i][2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : string.Empty;

            values[name] = value;
        }

        return values;
    }

    private static string Require(Dictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"--{name} is required.");

    private static int Usage(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine(
            "Usage:\n" +
            "  marites identity grant --issuer <issuer> --subject <sub> --email <address> --role <Viewer|Support|Admin>\n" +
            "  marites identity list\n" +
            "  marites identity revoke --issuer <issuer> --subject <sub>");
        return 2;
    }
}

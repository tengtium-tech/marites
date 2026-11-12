using Marites.Api;
using Marites.Host;
using Marites.Infrastructure.Bookkeeping;
using Marites.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

if (!HostModeParser.TryParse(args, out var mode))
{
    Console.Error.WriteLine("Usage: Marites.Host <api|worker|all|migrate>");
    return 2;
}

switch (mode)
{
    case HostMode.Api:
        await ApiHost.Build().RunAsync();
        break;
    case HostMode.Worker:
        await WorkerHost.Build().RunAsync();
        break;
    case HostMode.All:
        await ApiHost.Build(configureAdditionalServices: services => services.AddMaritesWorker()).RunAsync();
        break;
    case HostMode.Migrate:
        return await MigrateAsync();
    default:
        return 2;
}

return 0;

/// <summary>
/// The separate migration step DEC-014 requires. It exits rather than serving, so a deploy can
/// stop before the application starts if the schema cannot be brought forward.
/// </summary>
static async Task<int> MigrateAsync()
{
    var path = Environment.GetEnvironmentVariable("MARITES_BOOKKEEPING_PATH") ?? "./data/marites.db";
    var fullPath = Path.GetFullPath(path);

    // SQLite creates the file but not the directory above it, and the default path assumes one.
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var options = new DbContextOptionsBuilder<BookkeepingDbContext>()
        .UseSqlite($"Data Source={fullPath}")
        .Options;

    await using var context = new BookkeepingDbContext(options);
    var migrator = new BookkeepingMigrator(context);
    var seeded = await migrator.MigrateAndSeedAsync("Default", "default", DateTimeOffset.UtcNow);

    Console.WriteLine(
        seeded
            ? $"Bookkeeping store ready at {fullPath}; seeded the default tenant."
            : $"Bookkeeping store ready at {fullPath}; a tenant already exists.");

    return 0;
}

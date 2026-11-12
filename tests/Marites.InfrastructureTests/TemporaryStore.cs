using Marites.Infrastructure.Bookkeeping;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Marites.InfrastructureTests;

/// <summary>A bookkeeping store in a temporary file, deleted when the test finishes.</summary>
internal sealed class TemporaryStore : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"marites-bookkeeping-{Guid.NewGuid():N}.db");

    public string Path_ => _path;

    public BookkeepingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BookkeepingDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        return new BookkeepingDbContext(options);
    }

    public void Dispose()
    {
        // The connection pool holds the file open; without this the delete fails on Windows.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}

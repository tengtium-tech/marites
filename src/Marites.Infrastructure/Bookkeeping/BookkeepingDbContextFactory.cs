using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Marites.Infrastructure.Bookkeeping;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without starting a host. Design-time only: the
/// running service resolves its own options from <c>MARITES_BOOKKEEPING_PATH</c>, and this
/// connection string is never used outside migration tooling.
/// </summary>
public sealed class BookkeepingDbContextFactory : IDesignTimeDbContextFactory<BookkeepingDbContext>
{
    public BookkeepingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BookkeepingDbContext>()
            .UseSqlite("Data Source=./data/marites.db")
            .Options;

        return new BookkeepingDbContext(options);
    }
}

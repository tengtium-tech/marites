using System.Text.RegularExpressions;

namespace Marites.ApiTests;

/// <summary>
/// The error catalog, read at run time.
/// </summary>
/// <remarks>
/// Tests that assert a status and a code should take both from the catalog rather than from a
/// copy of it. Three codes were wrong in the implementation for a day and no test noticed,
/// because every assertion had been written against what the code did rather than what the
/// contract said.
/// </remarks>
public static partial class ErrorCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, int>> Statuses = new(Load);

    /// <summary>The documented HTTP status for a code. Throws if the code is not in the catalog.</summary>
    public static int StatusFor(string code) =>
        Statuses.Value.TryGetValue(code, out var status)
            ? status
            : throw new InvalidOperationException($"{code} is not in the error catalog.");

    private static IReadOnlyDictionary<string, int> Load()
    {
        var path = Locate();
        var statuses = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in File.ReadAllLines(path))
        {
            var match = Row().Match(line);

            if (match.Success)
            {
                statuses[match.Groups["code"].Value] = int.Parse(match.Groups["status"].Value);
            }
        }

        return statuses;
    }

    [GeneratedRegex(@"^\|\s*`(?<code>ERR-[A-Z]+-\d{3})`\s*\|\s*(?<status>\d{3})\s*\|")]
    private static partial Regex Row();

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "07-data-and-contracts", "error-catalog.md");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("error-catalog.md was not found above the test output directory.");
    }
}

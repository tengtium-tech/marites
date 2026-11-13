using System.Text.RegularExpressions;

namespace Marites.ApiTests;

/// <summary>
/// Every operation the served contract declares is actually routed.
/// </summary>
/// <remarks>
/// This exists because two documented operations — `listQueues` and `getQueue` — sat in
/// `openapi.yaml` for days with nothing behind them, and no test noticed: the contract
/// conformance suite validates the responses of operations that exist, not whether every
/// documented operation exists. A contract that promises more than the build serves is the
/// failure `/capabilities` is supposed to prevent, one layer down.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed partial class RouteCoverageTests
{
    private readonly BrokerApiFixture _fixture;

    public RouteCoverageTests(BrokerApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Every_served_operation_has_a_registered_route()
    {
        var documented = DocumentedOperations();
        var registered = _fixture.Routes
            .Select(Normalise)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = documented
            .Where(operation => !registered.Contains(operation))
            .OrderBy(operation => operation, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "The contract declares operations with no route registered:" + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Select(m => "  " + m)) +
            Environment.NewLine + Environment.NewLine +
            "Registered:" + Environment.NewLine +
            string.Join(Environment.NewLine, registered.OrderBy(r => r, StringComparer.Ordinal).Select(r => "  " + r)));
    }

    /// <summary>Reads the served contract and returns "METHOD /path" for every operation.</summary>
    private static List<string> DocumentedOperations()
    {
        var yaml = File.ReadAllText(LocateContract());
        var operations = new List<string>();

        string? path = null;

        foreach (var raw in yaml.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            // A top-level path key is indented exactly two spaces.
            var pathMatch = PathLine().Match(line);
            if (pathMatch.Success)
            {
                path = pathMatch.Groups["path"].Value;
                continue;
            }

            // A method key under the current path is indented exactly four spaces.
            var methodMatch = MethodLine().Match(line);
            if (methodMatch.Success && path is not null)
            {
                var method = methodMatch.Groups["method"].Value.ToUpperInvariant();

                // `parameters` and `servers` sit at the same depth but are not operations.
                if (method is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS")
                {
                    operations.Add($"{method} /api/v1{path.TrimEnd('/')}");
                }
            }
        }

        return operations;
    }

    /// <summary>
    /// Drops route constraints and trailing slashes so both sides compare like for like.
    /// A group mapped at "/brokers" with a child at "/" registers "/brokers/", while the contract
    /// writes "/brokers" — the same route, spelled differently.
    /// </summary>
    private static string Normalise(string route)
    {
        var normalised = Regex.Replace(route, @"\{([a-zA-Z]+):[a-zA-Z]+\}", "{$1}");
        var parts = normalised.Split(' ', 2);

        return $"{parts[0]} {parts[1].TrimEnd('/')}";
    }

    [GeneratedRegex(@"^  (?<path>/[A-Za-z0-9{}/_\-]*):\s*$")]
    private static partial Regex PathLine();

    [GeneratedRegex(@"^    (?<method>[a-z]+):\s*$")]
    private static partial Regex MethodLine();

    private static string LocateContract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "07-data-and-contracts", "openapi", "openapi.yaml");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("openapi.yaml was not found above the test output directory.");
    }
}

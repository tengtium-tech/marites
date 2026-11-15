namespace Marites.Host;

public enum HostMode
{
    Api,
    Worker,
    All,

    /// <summary>Applies the bookkeeping schema and seeds the implicit tenant, then exits.</summary>
    Migrate,

    /// <summary>Manages the explicit identity mapping — grant, list, revoke.</summary>
    Identity
}

public static class HostModeParser
{
    public static bool TryParse(string[] args, out HostMode mode)
    {
        mode = default;

        if (args.Length == 0)
        {
            return false;
        }

        // `identity` takes a subcommand and its options; every other mode is a bare word.
        if (string.Equals(args[0], "identity", StringComparison.OrdinalIgnoreCase))
        {
            mode = HostMode.Identity;
            return args.Length >= 2;
        }

        if (args.Length != 1)
        {
            return false;
        }

        mode = args[0].ToLowerInvariant() switch
        {
            "api" => HostMode.Api,
            "worker" => HostMode.Worker,
            "all" => HostMode.All,
            "migrate" => HostMode.Migrate,
            _ => (HostMode)(-1)
        };

        return Enum.IsDefined(mode);
    }
}

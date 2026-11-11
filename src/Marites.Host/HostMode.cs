namespace Marites.Host;

public enum HostMode
{
    Api,
    Worker,
    All
}

public static class HostModeParser
{
    public static bool TryParse(string[] args, out HostMode mode)
    {
        mode = default;
        if (args.Length != 1)
        {
            return false;
        }

        mode = args[0].ToLowerInvariant() switch
        {
            "api" => HostMode.Api,
            "worker" => HostMode.Worker,
            "all" => HostMode.All,
            _ => (HostMode)(-1)
        };

        return Enum.IsDefined(mode);
    }
}

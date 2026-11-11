using Marites.Host;

namespace Marites.ArchitectureTests;

public sealed class HostModeParserTests
{
    [Theory]
    [InlineData("api", HostMode.Api)]
    [InlineData("worker", HostMode.Worker)]
    [InlineData("all", HostMode.All)]
    public void ParsesSupportedModes(string argument, HostMode expected)
    {
        Assert.True(HostModeParser.TryParse([argument], out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData()]
    [InlineData("api", "worker")]
    [InlineData("unknown")]
    public void RejectsUnsupportedModes(params string[] arguments)
    {
        Assert.False(HostModeParser.TryParse(arguments, out _));
    }
}

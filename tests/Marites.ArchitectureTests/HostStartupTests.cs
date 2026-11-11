using Marites.Api;
using Marites.Worker;

namespace Marites.ArchitectureTests;

public sealed class HostStartupTests
{
    [Fact]
    public async Task ApiHostStartsAndStops()
    {
        await using var app = ApiHost.Build(["--urls", "http://127.0.0.1:0"]);

        await app.StartAsync();
        await app.StopAsync();
    }

    [Fact]
    public async Task WorkerHostStartsAndStops()
    {
        using var host = WorkerHost.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task CombinedHostStartsAndStops()
    {
        await using var app = ApiHost.Build(
            ["--urls", "http://127.0.0.1:0"],
            services => services.AddMaritesWorker());

        await app.StartAsync();
        await app.StopAsync();
    }
}

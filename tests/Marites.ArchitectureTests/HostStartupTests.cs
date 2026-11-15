using Marites.Api;
using Marites.Worker;

namespace Marites.ArchitectureTests;

public sealed class HostStartupTests
{
    [Fact]
    public Task ApiHostStartsAndStops() => WithoutIdentityAsync(async () =>
    {
        await using var app = ApiHost.Build(["--urls", "http://127.0.0.1:0"]);

        await app.StartAsync();
        await app.StopAsync();
    });

    [Fact]
    public async Task WorkerHostStartsAndStops()
    {
        using var host = WorkerHost.Build();

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public Task CombinedHostStartsAndStops() => WithoutIdentityAsync(async () =>
    {
        await using var app = ApiHost.Build(
            ["--urls", "http://127.0.0.1:0"],
            services => services.AddMaritesWorker());

        await app.StartAsync();
        await app.StopAsync();
    });

    /// <summary>
    /// The API refuses to start in the default `entra` mode without a tenant, which is the
    /// point of the startup validation. These tests are about the host lifecycle, so they run
    /// in `none` mode and restore whatever the environment had.
    /// </summary>
    private static async Task WithoutIdentityAsync(Func<Task> body)
    {
        var previous = Environment.GetEnvironmentVariable("MARITES_AUTH_MODE");
        Environment.SetEnvironmentVariable("MARITES_AUTH_MODE", "none");

        try
        {
            await body();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MARITES_AUTH_MODE", previous);
        }
    }
}

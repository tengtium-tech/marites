using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Marites.Worker;

public static class WorkerHost
{
    public static IHost Build(string[]? args = null)
    {
        var builder = Host.CreateApplicationBuilder(args ?? []);
        builder.Services.AddMaritesWorker();
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });

        return builder.Build();
    }
}

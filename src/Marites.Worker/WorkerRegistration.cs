using Microsoft.Extensions.DependencyInjection;

namespace Marites.Worker;

public static class WorkerRegistration
{
    public static IServiceCollection AddMaritesWorker(this IServiceCollection services)
    {
        services.AddHostedService<CaptureWorker>();
        return services;
    }
}

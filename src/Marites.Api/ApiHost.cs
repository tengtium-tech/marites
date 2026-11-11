using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Marites.Api;

public static class ApiHost
{
    public static WebApplication Build(
        string[]? args = null,
        Action<IServiceCollection>? configureAdditionalServices = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []);
        builder.Host.ConfigureHostOptions(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });

        configureAdditionalServices?.Invoke(builder.Services);

        return builder.Build();
    }
}

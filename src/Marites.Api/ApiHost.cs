using Marites.Api.Capabilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Marites.Api;

public static class ApiHost
{
    /// <summary>
    /// Base path of every served operation. The contract's server URL is <c>/api/v1</c>,
    /// so a route declared without this prefix is not reachable at the documented address.
    /// </summary>
    public const string ApiBasePath = "/api/v1";

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

        var app = builder.Build();

        var api = app.MapGroup(ApiBasePath);
        api.MapCapabilitiesEndpoints();

        return app;
    }
}

using Marites.Api.Brokers;
using Marites.Api.Capabilities;
using Marites.Application.Brokers;
using Marites.Application.Security;
using Marites.Infrastructure.Bookkeeping;
using Marites.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
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

        var bookkeepingPath = Environment.GetEnvironmentVariable("MARITES_BOOKKEEPING_PATH")
            ?? "./data/marites.db";

        builder.Services.AddDbContext<BookkeepingDbContext>(options =>
            options.UseSqlite($"Data Source={Path.GetFullPath(bookkeepingPath)}"));

        // The KEK is required from first start (configuration reference §1). Resolving it here
        // means a service without one refuses to boot rather than failing at the first write.
        builder.Services.AddSingleton(KekOptions.FromEnvironment(Environment.GetEnvironmentVariable));
        builder.Services.AddSingleton<ICredentialProtector, KekCredentialProtector>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IBrokerProfileRepository, BrokerProfileRepository>();
        builder.Services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        builder.Services.AddScoped<BrokerProfileService>();

        configureAdditionalServices?.Invoke(builder.Services);

        var app = builder.Build();

        var api = app.MapGroup(ApiBasePath);
        api.MapCapabilitiesEndpoints();
        api.MapBrokerEndpoints();

        return app;
    }
}

using Marites.Api.Brokers;
using Marites.Api.Capabilities;
using Marites.Application.Brokers;
using Marites.Application.Preflight;
using Marites.Application.Security;
using Marites.Infrastructure.Bookkeeping;
using Marites.Infrastructure.RabbitMq;
using Marites.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
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

        // The contract declares optional properties as `type: string`, and JSON Schema treats
        // null as a different type — so a null-serialised optional field is a contract
        // violation, not a harmless blank. Omit absent values rather than sending null.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

        builder.Services.AddDbContext<BookkeepingDbContext>(options =>
            options.UseSqlite($"Data Source={Path.GetFullPath(bookkeepingPath)}"));

        // Registered lazily: building a host must not require a key, or every test that starts
        // one would need production configuration. ValidateConfiguration makes the service
        // refuse to start without it, which is where the requirement actually belongs.
        builder.Services.AddSingleton(_ => KekOptions.FromEnvironment(Environment.GetEnvironmentVariable));
        builder.Services.AddSingleton<ICredentialProtector, KekCredentialProtector>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IBrokerProfileRepository, BrokerProfileRepository>();
        builder.Services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        builder.Services.AddScoped<BrokerProfileService>();
        builder.Services.AddScoped<IBrokerProbe, RabbitMqProbe>();
        builder.Services.AddScoped<PreflightService>();

        configureAdditionalServices?.Invoke(builder.Services);

        var app = builder.Build();

        var api = app.MapGroup(ApiBasePath);
        api.MapCapabilitiesEndpoints();
        api.MapBrokerEndpoints();

        return app;
    }

    /// <summary>
    /// Resolves the configuration the service cannot run without, so a missing KEK stops the
    /// process before it binds a port rather than surfacing at the first credential write
    /// (configuration reference §1). Called by the entrypoint, not by <see cref="Build"/>:
    /// a host that is built to be inspected should not demand production secrets.
    /// </summary>
    public static void ValidateConfiguration(IServiceProvider services)
    {
        _ = services.GetRequiredService<KekOptions>();
    }
}

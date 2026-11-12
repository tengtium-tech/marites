using Marites.Api.Brokers;
using Marites.Api.Capabilities;
using Marites.Application.Brokers;
using Marites.Application.Preflight;
using Marites.Application.Security;
using Marites.Infrastructure.Bookkeeping;
using Marites.Infrastructure.RabbitMq;
using Marites.Infrastructure.Security;
using System.Net;
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

        // Health and metrics listen on their own address and are never published to the user
        // network (configuration reference §2). Binding them to a second endpoint is the only
        // way to say that in code rather than in a comment.
        var adminAddress = Environment.GetEnvironmentVariable("MARITES_ADMIN_ADDR") ?? "127.0.0.1:9090";
        var adminEndPoint = ParseEndPoint(adminAddress);

        builder.WebHost.ConfigureKestrel(options =>
        {
            foreach (var url in (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://127.0.0.1:8080")
                         .Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    options.Listen(ParseHost(uri.Host), uri.Port);
                }
            }

            options.Listen(adminEndPoint);
        });

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

        MapOperationsEndpoints(app, adminEndPoint.Port);

        return app;
    }

    private static void MapOperationsEndpoints(WebApplication app, int adminPort)
    {
        // Liveness: the process is running. It says nothing about the store, because a
        // restart loop is the wrong response to a store that is merely unavailable.
        app.MapGet("/health", (HttpContext context) =>
            IsAdmin(context, adminPort)
                ? Results.Ok(new { status = "live" })
                : Results.NotFound());

        // Readiness: can this instance actually serve? The bookkeeping store is required, so
        // an unreachable one means not ready — while the message store is optional and its
        // absence is a degraded mode, not a failure (DEC-016).
        app.MapGet("/ready", async (HttpContext context, BookkeepingDbContext store, CancellationToken cancellationToken) =>
        {
            if (!IsAdmin(context, adminPort))
            {
                return Results.NotFound();
            }

            var bookkeeping = await store.Database.CanConnectAsync(cancellationToken);

            return bookkeeping
                ? Results.Ok(new { status = "ready", bookkeeping = "ok" })
                : Results.Json(
                    new { status = "not-ready", bookkeeping = "unreachable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });
    }

    /// <summary>Health lives on the admin port only; asking for it on the user port is a 404.</summary>
    private static bool IsAdmin(HttpContext context, int adminPort) =>
        context.Connection.LocalPort == adminPort;

    private static IPEndPoint ParseEndPoint(string address)
    {
        var separator = address.LastIndexOf(':');
        var host = address[..separator];
        var port = int.Parse(address[(separator + 1)..]);

        return new IPEndPoint(ParseHost(host), port);
    }

    private static IPAddress ParseHost(string host) =>
        host is "" or "0.0.0.0" or "*" or "+"
            ? IPAddress.Any
            : host is "localhost" or "127.0.0.1"
                ? IPAddress.Loopback
                : IPAddress.Parse(host);

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

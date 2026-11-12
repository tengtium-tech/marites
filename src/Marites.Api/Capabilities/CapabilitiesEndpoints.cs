using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Marites.Api.Capabilities;

/// <summary>
/// <c>GET /api/v1/capabilities</c> — OP-CAP-001.
/// </summary>
/// <remarks>
/// The client reads this before rendering, so a control the backend cannot honour is
/// never shown. That only works if the answer is true: a feature is listed here when its
/// operations are served, not when they are planned. Declaring one early produces a
/// button that fails at the moment it is pressed, which is the failure this endpoint
/// exists to prevent (DOC-ARCH-001 §6.2).
/// </remarks>
public static class CapabilitiesEndpoints
{
    /// <summary>Version of the contract this backend implements.</summary>
    public const string ApiVersion = "0.2";

    /// <summary>
    /// Features served today. A feature is added in the same change that serves its
    /// operations: brokers arrived with OP-BRK-001 to 005, preflight and queues are still to come.
    /// </summary>
    private static readonly string[] SupportedFeatures = ["brokers"];

    /// <summary>
    /// Provider kinds with a registered adapter. Empty until the RabbitMQ adapter lands
    /// in M1-1 — the registry does not exist yet, and a hard-coded list here would be a
    /// second source of truth for VOCABULARY §12.
    /// </summary>
    private static readonly string[] SupportedProviderKinds = [];

    public static IEndpointRouteBuilder MapCapabilitiesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/capabilities", GetBackendCapabilities)
            .WithName("getBackendCapabilities")
            .WithTags("Capabilities")
            .Produces<BackendCapabilitiesResponse>(StatusCodes.Status200OK);

        return routes;
    }

    private static Ok<BackendCapabilitiesResponse> GetBackendCapabilities() =>
        TypedResults.Ok(new BackendCapabilitiesResponse(
            ApiVersion,
            SupportedFeatures,
            SupportedProviderKinds));
}

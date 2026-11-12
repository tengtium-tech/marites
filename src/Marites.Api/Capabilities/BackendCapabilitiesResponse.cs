namespace Marites.Api.Capabilities;

/// <summary>
/// The <c>BackendCapabilities</c> schema of the served contract
/// (<c>docs/07-data-and-contracts/openapi/openapi.yaml</c>).
/// </summary>
/// <param name="ApiVersion">Version of the contract this backend implements.</param>
/// <param name="Features">
/// Feature flags this implementation serves. Absent means unsupported, so an empty
/// list is a truthful answer and a guessed one is not.
/// </param>
/// <param name="ProviderKinds">Broker platforms this build can talk to.</param>
public sealed record BackendCapabilitiesResponse(
    string ApiVersion,
    IReadOnlyList<string> Features,
    IReadOnlyList<string> ProviderKinds);

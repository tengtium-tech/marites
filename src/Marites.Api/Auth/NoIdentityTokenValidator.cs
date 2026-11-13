using Marites.Application.Access;

namespace Marites.Api.Auth;

/// <summary>
/// The validator used in <c>none</c> mode, where there is no token to validate. It refuses
/// everything rather than accepting anything: a path that should not exist must not become a
/// path that trusts an unverified claim.
/// </summary>
public sealed class NoIdentityTokenValidator : IIdentityTokenValidator
{
    public Task<VerifiedIdentity?> ValidateAsync(string token, CancellationToken cancellationToken) =>
        Task.FromResult<VerifiedIdentity?>(null);
}

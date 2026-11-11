using Marites.Infrastructure;

namespace Marites.Domain;

internal sealed class InvalidDomainReference
{
    public InfrastructureAssemblyMarker? ForbiddenDependency { get; init; }
}

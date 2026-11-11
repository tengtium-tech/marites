using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Marites.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        Assembly.Load("Marites.Contracts"),
        Assembly.Load("Marites.Domain"),
        Assembly.Load("Marites.Application"),
        Assembly.Load("Marites.Infrastructure"),
        Assembly.Load("Marites.Api"),
        Assembly.Load("Marites.Worker")
    ];

    private static Architecture LoadArchitecture(params Assembly[] additionalAssemblies) =>
        new ArchLoader()
            .LoadAssemblies(ProductionAssemblies.Concat(additionalAssemblies).ToArray())
            .Build();

    [Fact]
    public void ApprovedProjectGraphRespectsInwardDependencies()
    {
        var architecture = LoadArchitecture();

        Assert.True(Types().That().ResideInNamespace("Marites.Domain")
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Marites.Application"))
            .HasNoViolations(architecture), "Domain must not depend on Application.");
        Assert.True(Types().That().ResideInNamespace("Marites.Domain")
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Marites.Infrastructure"))
            .HasNoViolations(architecture), "Domain must not depend on Infrastructure.");
        Assert.True(Types().That().ResideInNamespace("Marites.Domain")
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Marites.Api"))
            .HasNoViolations(architecture), "Domain must not depend on API.");
        Assert.True(Types().That().ResideInNamespace("Marites.Application")
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Marites.Infrastructure"))
            .HasNoViolations(architecture), "Application must not depend on Infrastructure.");
        Assert.True(Types().That().ResideInNamespace("Marites.Application")
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Marites.Api"))
            .HasNoViolations(architecture), "Application must not depend on API.");
        Assert.True(Types().That().ResideInNamespace("Marites.Infrastructure")
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Marites.Api"))
            .HasNoViolations(architecture), "Infrastructure must not depend on API.");
    }

    [Fact]
    public void RuleDetectsAnIntentionallyInvalidDomainToInfrastructureReference()
    {
        var architecture = LoadArchitecture(Assembly.Load("Marites.ArchitectureViolationFixture"));
        var rule = Types().That().ResideInNamespace("Marites.Domain")
            .Should().NotDependOnAny(Types().That().ResideInNamespace("Marites.Infrastructure"));

        Assert.False(rule.HasNoViolations(architecture));
    }
}

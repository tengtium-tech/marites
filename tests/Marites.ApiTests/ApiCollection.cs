namespace Marites.ApiTests;

/// <summary>
/// One host for every API test. The fixture sets process-wide environment variables, so two
/// fixtures would race for the same database and one would answer with the other's data —
/// the hazard is real enough to have happened while writing these tests.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<BrokerApiFixture>
{
    public const string Name = "api";
}

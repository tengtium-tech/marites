using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Serialization;

namespace Marites.ApiTests;

/// <summary>
/// TC-001 — every served 2xx body validates against the schema the contract declares.
/// </summary>
/// <remarks>
/// This exists because a test that asserts fields chosen by the author can pass while the
/// contract says something else. The schema here is read from <c>openapi.yaml</c> at run time,
/// so the assertion follows the contract rather than a copy of it.
///
/// The components are lifted into <c>$defs</c> and their internal references rewritten, which
/// is what makes them validatable standalone: <c>#/components/schemas/X</c> is not a resolvable
/// pointer outside an OpenAPI document.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class ContractConformanceTests
{
    private static readonly Lazy<JsonSchema> SchemaDocument = new(LoadSchemas);

    private readonly BrokerApiFixture _fixture;

    public ContractConformanceTests(BrokerApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Capabilities_response_validates_against_the_contract()
    {
        var response = await _fixture.Client.GetAsync("/api/v1/capabilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertValidAsync("BackendCapabilities", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Broker_list_and_detail_validate_against_the_contract()
    {
        var created = await _fixture.Client.PostAsync(
            "/api/v1/brokers",
            JsonBody("""{"name":"contract-check","providerKind":"rabbitmq","environment":"dev","credentialKind":"basic","endpoint":"amqp://user:s3cret@h/v"}""", "contract-1"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadAsStringAsync();

        await AssertValidAsync("BrokerProfile", body);

        using var document = JsonDocument.Parse(body);
        var id = document.RootElement.GetProperty("id").GetString()!;

        var detail = await _fixture.Client.GetAsync($"/api/v1/brokers/{id}");
        await AssertValidAsync("BrokerProfile", await detail.Content.ReadAsStringAsync());

        var list = await _fixture.Client.GetAsync("/api/v1/brokers");
        var listBody = await list.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var array = JsonNode.Parse(listBody)!.AsArray();
        Assert.NotEmpty(array);

        foreach (var item in array)
        {
            AssertValid("BrokerProfile", item!);
        }
    }

    private static void AssertValid(string component, JsonNode instance)
    {
        var schema = SchemaDocument.Value;

        var document = JsonNode.Parse(
            $$"""{"$schema":"https://json-schema.org/draft/2020-12/schema","$defs":{{Definitions()}},"$ref":"#/$defs/{{component}}"}""")!;

        var results = JsonSchema.FromText(document.ToJsonString()).Evaluate(instance);

        if (!results.IsValid)
        {
            var failures = string.Join(
                Environment.NewLine,
                Flatten(results).Select(f => $"  {f}"));

            Assert.Fail($"{component} does not validate:{Environment.NewLine}{failures}");
        }

        _ = schema;
    }

    private static IEnumerable<string> Flatten(EvaluationResults results)
    {
        if (results.Errors is not null)
        {
            foreach (var error in results.Errors)
            {
                yield return $"{results.InstanceLocation} → {error.Key}: {error.Value}";
            }
        }

        foreach (var detail in results.Details)
        {
            foreach (var nested in Flatten(detail))
            {
                yield return nested;
            }
        }
    }

    private static async Task AssertValidAsync(string component, string body) =>
        AssertValid(component, JsonNode.Parse(body)!);

    private static string Definitions() => CachedDefinitions.Value;

    private static readonly Lazy<string> CachedDefinitions = new(() =>
    {
        var components = JsonNode.Parse(OpenApiJson())!["components"]!["schemas"]!
            .ToJsonString()
            .Replace("#/components/schemas/", "#/$defs/", StringComparison.Ordinal);

        return components;
    });

    private static JsonSchema LoadSchemas() => JsonSchema.FromText(
        $$"""{"$schema":"https://json-schema.org/draft/2020-12/schema","$defs":{{Definitions()}}}""");

    private static string OpenApiJson()
    {
        var yaml = File.ReadAllText(ContractPath());

        // Without type inference every scalar arrives as a string, and a schema whose
        // "minimum": 1 is the string "1" fails to parse rather than to validate.
        var parsed = new DeserializerBuilder()
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build()
            .Deserialize<object>(yaml);

        return new SerializerBuilder().JsonCompatible().Build().Serialize(parsed);
    }

    private static string ContractPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docs", "07-data-and-contracts", "openapi", "openapi.yaml");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("openapi.yaml was not found above the test output directory.");
    }

    private static StringContent JsonBody(string json, string key)
    {
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        content.Headers.Add("Idempotency-Key", key);
        return content;
    }
}

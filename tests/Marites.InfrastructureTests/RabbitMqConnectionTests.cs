using System.Net.Sockets;
using Marites.Infrastructure.RabbitMq;
using RabbitMQ.Client;

namespace Marites.InfrastructureTests;

/// <summary>
/// The AMQP authentication path, which nothing before this exercised.
/// </summary>
/// <remarks>
/// Preflight proves the management API accepts a credential. It says nothing about whether the
/// AMQP endpoint does — a user can hold management access without vhost access to consume — so
/// this is the first test that answers the question a consumer actually depends on.
///
/// These run only when a broker is reachable; <c>testing/compose.yaml</c> provides one. Skipped
/// rather than failed otherwise, because a missing broker is an environment fact, not a defect.
/// </remarks>
public sealed class RabbitMqConnectionTests
{
    private const string Endpoint = "amqp://localhost:5672/";
    private const string Credentials = """{"amqp":"marites:marites"}""";

    [Fact]
    public async Task The_stored_credential_authenticates_over_amqp()
    {
        if (!await BrokerReachableAsync())
        {
            return; // No broker: nothing to prove, and nothing to blame.
        }

        var factory = RabbitMqConnectionFactory.Create(Endpoint, Credentials);

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        Assert.True(connection.IsOpen);
        Assert.Equal("marites", factory.UserName);
        Assert.Equal("/", factory.VirtualHost);
        _ = channel;
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        if (!await BrokerReachableAsync())
        {
            return;
        }

        var factory = RabbitMqConnectionFactory.Create(Endpoint, """{"amqp":"marites:wrong"}""");

        await Assert.ThrowsAnyAsync<Exception>(() => factory.CreateConnectionAsync());
    }

    [Fact]
    public void The_endpoint_is_parsed_into_host_port_and_vhost()
    {
        var factory = RabbitMqConnectionFactory.Create("amqp://rabbit.internal:5673/orders", Credentials);

        Assert.Equal("rabbit.internal", factory.HostName);
        Assert.Equal(5673, factory.Port);
        Assert.Equal("orders", factory.VirtualHost);
    }

    [Fact]
    public void A_profile_with_no_amqp_credential_is_refused_before_connecting()
    {
        var error = Assert.Throws<ArgumentException>(
            () => RabbitMqConnectionFactory.Create(Endpoint, "{}"));

        Assert.Contains("no AMQP credential", error.Message);
    }

    private static async Task<bool> BrokerReachableAsync()
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("localhost", 5672);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

using Marites.Infrastructure.RabbitMq;

namespace Marites.InfrastructureTests;

/// <summary>
/// Where a redriven message goes. No broker involved — this is pure header arithmetic, which
/// is exactly why it must not live untested inside the AMQP loop.
/// </summary>
public sealed class ReplayTargetTests
{
    [Fact]
    public void An_explicit_routing_key_wins_over_everything()
    {
        var headers = DeathRecord("orders", "order.created");

        var (exchange, routingKey) = ReplayTarget.Resolve(
            "override", "forced", headers, "source.queue");

        Assert.Equal("override", exchange);
        Assert.Equal("forced", routingKey);
    }

    [Fact]
    public void An_omitted_exchange_means_the_default_exchange_with_an_explicit_routing_key()
    {
        var (exchange, routingKey) = ReplayTarget.Resolve(
            null, "target.queue", null, "source.queue");

        // Empty string is the AMQP way of naming the default exchange — routing straight to
        // the queue named by the routing key. Not a missing value.
        Assert.Equal(string.Empty, exchange);
        Assert.Equal("target.queue", routingKey);
    }

    [Fact]
    public void Without_an_override_the_death_record_names_the_origin()
    {
        // RabbitMQ prepends each death, so the most recent one is first. The message died at
        // "orders" most recently, after an earlier death at "payments".
        var headers = DeathRecord("orders", "order.created", second: ("payments", "payment.made"));

        var (exchange, routingKey) = ReplayTarget.Resolve(
            null, null, headers, "source.queue");

        Assert.Equal("orders", exchange);
        Assert.Equal("order.created", routingKey);
    }

    [Fact]
    public void A_message_without_a_death_record_goes_back_to_its_queue()
    {
        var (exchange, routingKey) = ReplayTarget.Resolve(null, null, null, "dlq.fallback");

        Assert.Equal(string.Empty, exchange);
        Assert.Equal("dlq.fallback", routingKey);
    }

    [Fact]
    public void A_death_record_without_a_routing_key_does_not_guess()
    {
        var headers = new Dictionary<string, object?>
        {
            ["x-death"] = new List<object?> { new Dictionary<string, object?> { ["exchange"] = "orders" } },
        };

        var (exchange, routingKey) = ReplayTarget.Resolve(null, null, headers, "dlq.fallback");

        // An exchange with no routing key is half an address; publishing there would scatter
        // the message wherever the exchange's bindings happen to lead. The queue is honest.
        Assert.Equal(string.Empty, exchange);
        Assert.Equal("dlq.fallback", routingKey);
    }

    [Fact]
    public void An_x_death_of_an_unexpected_shape_is_ignored_not_thrown()
    {
        var headers = new Dictionary<string, object?> { ["x-death"] = "not-a-list" };

        var (exchange, routingKey) = ReplayTarget.Resolve(null, null, headers, "dlq.fallback");

        Assert.Equal(string.Empty, exchange);
        Assert.Equal("dlq.fallback", routingKey);
    }

    private static IDictionary<string, object?> DeathRecord(
        string firstExchange,
        string firstRoutingKey,
        (string Exchange, string RoutingKey)? second = null)
    {
        var deaths = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["exchange"] = firstExchange,
                ["routing-keys"] = new List<object?> { firstRoutingKey },
            },
        };

        if (second is { } entry)
        {
            deaths.Add(new Dictionary<string, object?>
            {
                ["exchange"] = entry.Exchange,
                ["routing-keys"] = new List<object?> { entry.RoutingKey },
            });
        }

        return new Dictionary<string, object?> { ["x-death"] = deaths };
    }
}

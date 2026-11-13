using Marites.Infrastructure.RabbitMq;

namespace Marites.InfrastructureTests;

/// <summary>
/// The reconnect delay, exercised without waiting for a real drop.
/// </summary>
public sealed class BackoffTests
{
    [Fact]
    public void The_delay_never_exceeds_the_ceiling()
    {
        for (var drop = 0; drop < 20; drop++)
        {
            for (var i = 0; i < 50; i++)
            {
                Assert.True(RabbitMqTapSupervisor.NextBackoff(drop) <= TimeSpan.FromSeconds(30));
            }
        }
    }

    [Fact]
    public void Successive_delays_differ_so_reconnects_spread_out()
    {
        // The defect this fixes: a deterministic delay means every tap reconnects in the same
        // instant after a broker restart. Identical values here would mean jitter is absent.
        var values = Enumerable.Range(0, 20)
            .Select(_ => RabbitMqTapSupervisor.NextBackoff(dropCount: 5, new Random()).TotalMilliseconds)
            .Distinct()
            .Count();

        Assert.True(values > 5, $"Expected spread across delays, got {values} distinct values.");
    }

    [Fact]
    public void The_window_widens_with_repeated_failures()
    {
        // Drop 0 draws from [0,2), drop 8 from [0,256) capped at 30. Sampling the maximum of a
        // small set shows the ceiling actually grows rather than staying fixed.
        var early = Enumerable.Range(0, 200)
            .Max(_ => RabbitMqTapSupervisor.NextBackoff(dropCount: 0).TotalSeconds);
        var late = Enumerable.Range(0, 200)
            .Max(_ => RabbitMqTapSupervisor.NextBackoff(dropCount: 8).TotalSeconds);

        Assert.True(late > early, $"Expected the ceiling to grow: early max {early:F1}s, late max {late:F1}s.");
        Assert.True(late <= 30.0);
    }
}

using Marites.Infrastructure.RabbitMq;

namespace Marites.InfrastructureTests;

/// <summary>
/// The clock-skew judgement, exercised without a broker whose clock is actually wrong — which is
/// not something a test can arrange.
/// </summary>
public sealed class ClockSkewTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Tolerance = RabbitMqProbe.ClockTolerance;

    [Fact]
    public void A_broker_on_time_passes()
    {
        var check = RabbitMqProbe.EvaluateClockSkew(Now, Now, Tolerance);

        Assert.Equal("pass", check.State);
    }

    [Fact]
    public void A_broker_ahead_passes_within_tolerance()
    {
        var check = RabbitMqProbe.EvaluateClockSkew(Now.AddSeconds(30), Now, Tolerance);

        Assert.Equal("pass", check.State);
    }

    [Fact]
    public void A_broker_behind_beyond_tolerance_fails_with_a_remedy()
    {
        var check = RabbitMqProbe.EvaluateClockSkew(Now.AddSeconds(-90), Now, Tolerance);

        Assert.Equal("fail", check.State);
        Assert.NotNull(check.Remedy);
        Assert.Contains("time source", check.Remedy!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Skew_is_absolute_so_direction_does_not_matter()
    {
        var ahead = RabbitMqProbe.EvaluateClockSkew(Now.AddSeconds(120), Now, Tolerance);
        var behind = RabbitMqProbe.EvaluateClockSkew(Now.AddSeconds(-120), Now, Tolerance);

        Assert.Equal("fail", ahead.State);
        Assert.Equal("fail", behind.State);
    }

    [Fact]
    public void An_unknown_clock_is_skipped_rather_than_passed()
    {
        // The distinction that matters: not knowing is not the same as being fine.
        var check = RabbitMqProbe.EvaluateClockSkew(null, Now, Tolerance);

        Assert.Equal("skipped", check.State);
        Assert.Contains("unknown", check.Detail!, StringComparison.OrdinalIgnoreCase);
    }
}

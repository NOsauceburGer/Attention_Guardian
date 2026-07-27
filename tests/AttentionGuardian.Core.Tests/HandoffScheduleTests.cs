using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class HandoffScheduleTests
{
    private static readonly DateTimeOffset StartTime =
        new(2026, 7, 25, 18, 0, 0, TimeSpan.FromHours(8));

    private static readonly FixedEvent NextEvent =
        new(StartTime, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(10));

    [Fact]
    public void GetSafeUntil_ReturnsNextEventHandoffTime()
    {
        Assert.Equal(NextEvent.HandoffTime, HandoffSchedule.GetSafeUntil(NextEvent));
    }

    [Fact]
    public void ShouldHandoff_BeforeHandoffTime_ReturnsFalse()
    {
        Assert.False(HandoffSchedule.ShouldHandoff(NextEvent.HandoffTime.AddTicks(-1), NextEvent));
    }

    [Fact]
    public void ShouldHandoff_AtHandoffTime_ReturnsTrue()
    {
        Assert.True(HandoffSchedule.ShouldHandoff(NextEvent.HandoffTime, NextEvent));
    }

    [Fact]
    public void ShouldHandoff_AfterHandoffTime_ReturnsTrue()
    {
        Assert.True(HandoffSchedule.ShouldHandoff(NextEvent.HandoffTime.AddTicks(1), NextEvent));
    }
}

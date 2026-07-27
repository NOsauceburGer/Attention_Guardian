using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class FixedEventSelectorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetNext_FromUnorderedEvents_ReturnsEarliestFutureEvent()
    {
        var later = CreateEvent(Now.AddHours(3));
        var next = CreateEvent(Now.AddHours(1));

        var result = FixedEventSelector.GetNext([later, next], Now);

        Assert.Same(next, result);
    }

    [Fact]
    public void GetNext_EventStartingNow_IsStillSelectedForHandoff()
    {
        var startingNow = CreateEvent(Now);

        var result = FixedEventSelector.GetNext([startingNow], Now);

        Assert.Same(startingNow, result);
    }

    [Fact]
    public void GetNext_IgnoresEventsThatAlreadyStarted()
    {
        var past = CreateEvent(Now.AddTicks(-1));
        var future = CreateEvent(Now.AddHours(1));

        var result = FixedEventSelector.GetNext([past, future], Now);

        Assert.Same(future, result);
    }

    [Fact]
    public void GetNext_WithNoUpcomingEvents_ReturnsNull()
    {
        var result = FixedEventSelector.GetNext([CreateEvent(Now.AddHours(-1))], Now);

        Assert.Null(result);
    }

    private static FixedEvent CreateEvent(DateTimeOffset startTime) =>
        new(startTime, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
}

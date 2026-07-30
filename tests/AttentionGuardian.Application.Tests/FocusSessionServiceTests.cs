using AttentionGuardian.Application;

namespace AttentionGuardian.Application.Tests;

public sealed class FocusSessionServiceTests
{
    private static readonly DateTimeOffset EventStart =
        new(2026, 7, 25, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_BeforeHandoff_CreatesFocusingSessionInMemory()
    {
        var clock = new ManualTimeProvider(EventStart.AddHours(-2));
        var service = new FocusSessionService(clock);

        var session = service.Start(CreateRequest());

        Assert.Same(session, service.Current);
        Assert.Equal(FocusSessionStatus.Focusing, session.Status);
        Assert.Equal(EventStart.AddHours(-1), session.SafeUntil);
    }

    [Fact]
    public void Start_AtHandoff_CreatesHandoffSession()
    {
        var service = new FocusSessionService(new ManualTimeProvider(EventStart.AddHours(-1)));

        var session = service.Start(CreateRequest());

        Assert.Equal(FocusSessionStatus.Handoff, session.Status);
    }

    [Fact]
    public void Refresh_WhenClockReachesHandoff_ChangesStatus()
    {
        var clock = new ManualTimeProvider(EventStart.AddHours(-2));
        var service = new FocusSessionService(clock);
        service.Start(CreateRequest());

        clock.UtcNow = EventStart.AddHours(-1);
        var session = service.Refresh();

        Assert.NotNull(session);
        Assert.Equal(FocusSessionStatus.Handoff, session.Status);
    }

    [Fact]
    public void Start_WithoutCurrentTask_IsRejected()
    {
        var service = new FocusSessionService(new ManualTimeProvider(EventStart.AddHours(-2)));
        var request = CreateRequest() with { CurrentTask = "   " };

        Assert.Throws<ArgumentException>(() => service.Start(request));
        Assert.Null(service.Current);
    }

    [Fact]
    public void Clear_RemovesInMemorySession()
    {
        var service = new FocusSessionService(new ManualTimeProvider(EventStart.AddHours(-2)));
        service.Start(CreateRequest());

        service.Clear();

        Assert.Null(service.Current);
    }

    [Fact]
    public void Start_WithMultipleUnorderedEvents_UsesEarliestUpcomingEvent()
    {
        var clock = new ManualTimeProvider(EventStart.AddHours(-2));
        var service = new FocusSessionService(clock);
        var later = CreateFixedEvent(EventStart.AddHours(2));
        var earliest = CreateFixedEvent(EventStart);

        var session = service.Start("完成当前任务", [later, earliest]);

        Assert.Same(earliest, session.NextEvent);
        Assert.Equal(earliest.HandoffTime, session.SafeUntil);
    }

    [Fact]
    public void Start_WithPastAndFutureEvents_IgnoresPastEvent()
    {
        var clock = new ManualTimeProvider(EventStart.AddHours(-2));
        var service = new FocusSessionService(clock);
        var past = CreateFixedEvent(clock.UtcNow.AddMinutes(-1));
        var future = CreateFixedEvent(EventStart);

        var session = service.Start("完成当前任务", [past, future]);

        Assert.Same(future, session.NextEvent);
    }

    [Fact]
    public void Start_WithNoUpcomingEvent_IsRejectedWithoutChangingState()
    {
        var clock = new ManualTimeProvider(EventStart);
        var service = new FocusSessionService(clock);

        var exception = Assert.Throws<ArgumentException>(() =>
            service.Start("完成当前任务", [CreateFixedEvent(EventStart.AddTicks(-1))]));

        Assert.Equal("fixedEvents", exception.ParamName);
        Assert.Null(service.Current);
    }

    private static StartFocusSessionRequest CreateRequest() =>
        new(
            "撰写项目说明",
            EventStart,
            TimeSpan.FromMinutes(20),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(10));

    private static AttentionGuardian.Core.FixedEvent CreateFixedEvent(DateTimeOffset startTime) =>
        new(
            startTime,
            TimeSpan.FromMinutes(20),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(10));
}

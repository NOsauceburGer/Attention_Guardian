using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed class FocusSessionService(TimeProvider timeProvider)
{
    public FocusSession? Current { get; private set; }

    public FocusSession Start(StartFocusSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nextEvent = new FixedEvent(
            request.EventStartTime,
            request.PreparationDuration,
            request.TravelDuration,
            request.SafetyBuffer);

        return Start(request.CurrentTask, [nextEvent]);
    }

    public FocusSession Start(
        string currentTask,
        IEnumerable<FixedEvent> fixedEvents)
    {
        ArgumentNullException.ThrowIfNull(currentTask);
        ArgumentNullException.ThrowIfNull(fixedEvents);

        var task = currentTask.Trim();
        if (task.Length == 0)
        {
            throw new ArgumentException("Current task is required.", nameof(currentTask));
        }

        var currentTime = timeProvider.GetUtcNow();
        var nextEvent = FixedEventSelector.GetNext(fixedEvents, currentTime);
        if (nextEvent is null)
        {
            throw new ArgumentException(
                "At least one upcoming fixed event is required.",
                nameof(fixedEvents));
        }

        Current = CreateSession(task, nextEvent, currentTime);
        return Current;
    }

    public FocusSession? Refresh()
    {
        if (Current is null)
        {
            return null;
        }

        Current = CreateSession(Current.CurrentTask, Current.NextEvent, timeProvider.GetUtcNow());
        return Current;
    }

    public FocusSession Restore(string currentTask, FixedEvent nextEvent)
    {
        ArgumentNullException.ThrowIfNull(currentTask);
        ArgumentNullException.ThrowIfNull(nextEvent);

        var task = currentTask.Trim();
        if (task.Length == 0)
        {
            throw new ArgumentException("Current task is required.", nameof(currentTask));
        }

        Current = CreateSession(task, nextEvent, timeProvider.GetUtcNow());
        return Current;
    }

    public void Clear() => Current = null;

    private static FocusSession CreateSession(
        string task,
        FixedEvent nextEvent,
        DateTimeOffset currentTime)
    {
        var status = HandoffSchedule.ShouldHandoff(currentTime, nextEvent)
            ? FocusSessionStatus.Handoff
            : FocusSessionStatus.Focusing;

        return new FocusSession(
            task,
            nextEvent,
            HandoffSchedule.GetSafeUntil(nextEvent),
            status);
    }
}

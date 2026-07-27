namespace AttentionGuardian.Core;

public static class HandoffSchedule
{
    public static DateTimeOffset GetSafeUntil(FixedEvent nextFixedEvent)
    {
        ArgumentNullException.ThrowIfNull(nextFixedEvent);
        return nextFixedEvent.HandoffTime;
    }

    public static bool ShouldHandoff(DateTimeOffset currentTime, FixedEvent nextFixedEvent)
    {
        ArgumentNullException.ThrowIfNull(nextFixedEvent);
        return currentTime >= nextFixedEvent.HandoffTime;
    }
}

namespace AttentionGuardian.Core;

public static class FixedEventSelector
{
    public static FixedEvent? GetNext(
        IEnumerable<FixedEvent> fixedEvents,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(fixedEvents);

        FixedEvent? nextEvent = null;

        foreach (var fixedEvent in fixedEvents)
        {
            if (fixedEvent is null)
            {
                throw new ArgumentException(
                    "The fixed event collection cannot contain null values.",
                    nameof(fixedEvents));
            }

            if (fixedEvent.StartTime < currentTime)
            {
                continue;
            }

            if (nextEvent is null || fixedEvent.StartTime < nextEvent.StartTime)
            {
                nextEvent = fixedEvent;
            }
        }

        return nextEvent;
    }
}

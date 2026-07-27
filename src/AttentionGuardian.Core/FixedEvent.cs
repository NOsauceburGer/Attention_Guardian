namespace AttentionGuardian.Core;

public sealed record FixedEvent
{
    public FixedEvent(
        DateTimeOffset startTime,
        TimeSpan preparationDuration,
        TimeSpan travelDuration,
        TimeSpan safetyBuffer)
    {
        StartTime = startTime;
        PreparationDuration = EnsureNonNegative(preparationDuration, nameof(preparationDuration));
        TravelDuration = EnsureNonNegative(travelDuration, nameof(travelDuration));
        SafetyBuffer = EnsureNonNegative(safetyBuffer, nameof(safetyBuffer));
        HandoffTime = CalculateHandoffTime();
    }

    public DateTimeOffset StartTime { get; }

    public TimeSpan PreparationDuration { get; }

    public TimeSpan TravelDuration { get; }

    public TimeSpan SafetyBuffer { get; }

    public DateTimeOffset HandoffTime { get; }

    private DateTimeOffset CalculateHandoffTime()
    {
        try
        {
            return StartTime - PreparationDuration - TravelDuration - SafetyBuffer;
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StartTime),
                StartTime,
                "Lead durations place the handoff time outside the supported date range.");
        }
    }

    private static TimeSpan EnsureNonNegative(TimeSpan value, string parameterName)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Duration cannot be negative.");
        }

        return value;
    }
}

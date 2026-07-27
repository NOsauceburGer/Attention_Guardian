namespace AttentionGuardian.Application;

public sealed record StartFocusSessionRequest(
    string CurrentTask,
    DateTimeOffset EventStartTime,
    TimeSpan PreparationDuration,
    TimeSpan TravelDuration,
    TimeSpan SafetyBuffer);

namespace AttentionGuardian.Application;

public sealed record AddScheduledTodoRequest(
    Guid Id,
    string Title,
    TimeSpan Duration,
    DateTimeOffset? StartTime = null,
    bool IsMandatory = false);

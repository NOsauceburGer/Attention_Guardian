namespace AttentionGuardian.Application;

public sealed record PlanUnscheduledTodoRequest(
    Guid UnscheduledTodoId,
    TimeSpan Duration,
    DateTimeOffset? StartTime = null);

namespace AttentionGuardian.Application;

public sealed record EditScheduledTodoRequest(
    Guid Id,
    string Title,
    TimeSpan Duration,
    bool IsMandatory);

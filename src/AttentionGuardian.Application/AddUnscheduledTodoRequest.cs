namespace AttentionGuardian.Application;

public sealed record AddUnscheduledTodoRequest(
    Guid Id,
    string Title,
    DateOnly? ScheduledDate = null,
    int? DaysFromToday = null,
    bool IsMandatory = false);

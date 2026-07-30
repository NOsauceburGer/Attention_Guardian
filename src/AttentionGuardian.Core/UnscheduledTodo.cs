namespace AttentionGuardian.Core;

public sealed record UnscheduledTodo : TodoItem
{
    public UnscheduledTodo(
        Guid id,
        string title,
        DateOnly scheduledDate,
        bool isMandatory = false)
        : base(id, title, isMandatory)
    {
        ScheduledDate = scheduledDate;
    }

    public DateOnly ScheduledDate { get; }
}

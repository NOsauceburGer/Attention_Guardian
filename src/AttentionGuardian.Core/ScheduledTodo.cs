namespace AttentionGuardian.Core;

public sealed record ScheduledTodo : TodoItem
{
    public ScheduledTodo(
        Guid id,
        string title,
        TimeRange timeRange,
        bool isMandatory = false,
        long currentSelectionPriority = 0)
        : base(id, title, isMandatory)
    {
        if (currentSelectionPriority < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentSelectionPriority),
                currentSelectionPriority,
                "Current selection priority cannot be negative.");
        }

        TimeRange = timeRange;
        CurrentSelectionPriority = currentSelectionPriority;
    }

    public TimeRange TimeRange { get; }

    public long CurrentSelectionPriority { get; }

    public DateOnly ScheduleDate => DateOnly.FromDateTime(TimeRange.Start.Date);

    public ScheduledTodo MoveTo(DateTimeOffset newStart) =>
        new(
            Id,
            Title,
            TimeRange.MoveTo(newStart),
            IsMandatory,
            CurrentSelectionPriority);

    public ScheduledTodo Edit(
        string title,
        TimeSpan duration,
        bool isMandatory)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "Duration must be greater than zero.");
        }

        return new(
            Id,
            title,
            new TimeRange(TimeRange.Start, TimeRange.Start + duration),
            isMandatory,
            CurrentSelectionPriority);
    }
}

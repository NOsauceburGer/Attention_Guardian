namespace AttentionGuardian.Core;

public enum HandoffReminderIneligibility
{
    None,
    NoCurrentTodo,
    CurrentTodoTooShort,
    NoAdjacentNextTodo,
    NextTodoIsBreak,
    OutsideReminderWindow
}

public sealed record HandoffReminderEvaluation(
    ScheduledTodo? CurrentTodo,
    ScheduledTodo? NextTodo,
    DateTimeOffset? ReminderAt,
    bool ShouldNotifyNow,
    HandoffReminderIneligibility Ineligibility);

public static class HandoffReminderPolicy
{
    public static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(5);

    public static HandoffReminderEvaluation Evaluate(
        IEnumerable<ScheduledTodo> schedule,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var ordered = schedule
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToArray();
        if (ordered.Any(todo => todo is null))
        {
            throw new ArgumentException(
                "The schedule cannot contain null values.",
                nameof(schedule));
        }

        var current = ScheduledTodoSelector.GetCurrent(ordered, currentTime);
        if (current is null)
        {
            return Ineligible(HandoffReminderIneligibility.NoCurrentTodo);
        }

        if (current.TimeRange.Duration < LeadTime)
        {
            return Ineligible(
                HandoffReminderIneligibility.CurrentTodoTooShort,
                current);
        }

        var next = ordered
            .Where(todo =>
                todo.Id != current.Id
                && todo.TimeRange.Start == current.TimeRange.End)
            .FirstOrDefault();
        if (next is null)
        {
            return Ineligible(
                HandoffReminderIneligibility.NoAdjacentNextTodo,
                current);
        }

        if (next.Title == ScheduleManagement.BreakTitle)
        {
            return Ineligible(
                HandoffReminderIneligibility.NextTodoIsBreak,
                current,
                next);
        }

        var reminderAt = current.TimeRange.End - LeadTime;
        var shouldNotify =
            currentTime >= reminderAt
            && currentTime < current.TimeRange.End;
        return new(
            current,
            next,
            reminderAt,
            shouldNotify,
            shouldNotify
                ? HandoffReminderIneligibility.None
                : HandoffReminderIneligibility.OutsideReminderWindow);
    }

    private static HandoffReminderEvaluation Ineligible(
        HandoffReminderIneligibility reason,
        ScheduledTodo? current = null,
        ScheduledTodo? next = null) =>
        new(current, next, null, false, reason);
}

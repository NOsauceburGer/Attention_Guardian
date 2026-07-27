using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed record PendingHandoffReminder(
    Guid CurrentTodoId,
    string CurrentTodoTitle,
    Guid NextTodoId,
    string NextTodoTitle,
    DateTimeOffset ReminderAt);

public sealed class HandoffReminderService(
    IScheduledTodoRepository scheduledRepository,
    TimeProvider timeProvider)
{
    private readonly HashSet<Guid> notifiedCurrentTodoIds = [];

    public async Task<PendingHandoffReminder?> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduledRepository.LoadAllAsync(cancellationToken);
        var evaluation = HandoffReminderPolicy.Evaluate(
            schedule,
            timeProvider.GetLocalNow());
        if (!evaluation.ShouldNotifyNow
            || evaluation.CurrentTodo is null
            || evaluation.NextTodo is null
            || evaluation.ReminderAt is null
            || !notifiedCurrentTodoIds.Add(evaluation.CurrentTodo.Id))
        {
            return null;
        }

        return new(
            evaluation.CurrentTodo.Id,
            evaluation.CurrentTodo.Title,
            evaluation.NextTodo.Id,
            evaluation.NextTodo.Title,
            evaluation.ReminderAt.Value);
    }
}

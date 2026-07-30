using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed class ScheduleManagementService(
    IScheduledTodoRepository scheduledRepository,
    IUnscheduledTodoRepository unscheduledRepository,
    TimeProvider timeProvider)
{
    public async Task<ManagementState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduledRepository.LoadAllAsync(cancellationToken);
        var now = timeProvider.GetLocalNow();
        await scheduledRepository.MarkCompletedBeforeAsync(now, cancellationToken);
        var activeSchedule = schedule.Where(todo => todo.TimeRange.End > now).ToArray();

        return new(
            activeSchedule,
            ScheduleManagement.FindMandatoryGroups(activeSchedule));
    }

    public async Task<ScheduleReorderResult> ReorderAsync(
        Guid todoId,
        int requestedIndex,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduledRepository.LoadAllAsync(cancellationToken);
        var result = ScheduleManagement.Reorder(schedule, todoId, requestedIndex);
        await scheduledRepository.ReplaceAllAsync(result.ScheduledTodos, cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<ScheduledTodo>> EditAsync(
        EditScheduledTodoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var schedule = await scheduledRepository.LoadAllAsync(cancellationToken);
        var result = ScheduleManagement.Edit(
            schedule,
            request.Id,
            request.Title,
            request.Duration,
            request.IsMandatory);
        await scheduledRepository.ReplaceAllAsync(result, cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<ScheduledTodo>> DeleteAsync(
        Guid todoId,
        bool isConfirmed,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduledRepository.LoadAllAsync(cancellationToken);
        if (!isConfirmed)
        {
            return schedule;
        }

        var result = ScheduleManagement.Delete(schedule, todoId);
        await scheduledRepository.ReplaceAllAsync(result, cancellationToken);
        return result;
    }

    public async Task<ScheduleTrialResult> AddBreakAsync(
        Guid id,
        DateTimeOffset start,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var schedule = await scheduledRepository.LoadAllAsync(cancellationToken);
        var result = ScheduleManagement.InsertBreak(
            schedule,
            id,
            start,
            duration,
            ScheduledTodoPriority.Next(schedule));
        await scheduledRepository.ReplaceAllAsync(result.ScheduledTodos, cancellationToken);
        return result;
    }

    public Task<IReadOnlyList<UnscheduledTodo>> LoadFutureTodosAsync(
        CancellationToken cancellationToken = default) =>
        unscheduledRepository.LoadAllActiveAsync(cancellationToken);

    public async Task<UnscheduledTodo> UpdateFutureTodoAsync(
        Guid id,
        string title,
        DateOnly scheduledDate,
        CancellationToken cancellationToken = default)
    {
        var existing = await unscheduledRepository.LoadActiveByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException(
                "The selected future todo is no longer active.");
        var updated = new UnscheduledTodo(
            existing.Id,
            title,
            scheduledDate,
            existing.IsMandatory);
        await unscheduledRepository.UpdateActiveAsync(updated, cancellationToken);
        return updated;
    }

    public Task DeleteFutureTodoAsync(
        Guid id,
        bool isConfirmed,
        CancellationToken cancellationToken = default)
    {
        if (!isConfirmed)
        {
            return Task.CompletedTask;
        }

        return unscheduledRepository.MarkDeletedAsync(id, cancellationToken);
    }
}

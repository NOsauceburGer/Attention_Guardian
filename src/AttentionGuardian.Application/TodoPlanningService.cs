using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed class TodoPlanningService(
    IScheduledTodoRepository scheduledRepository,
    IUnscheduledTodoRepository unscheduledRepository,
    TimeProvider timeProvider)
{
    public async Task<ScheduleTrialResult> AddScheduledTodoAsync(
        AddScheduledTodoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startTime = request.StartTime ?? timeProvider.GetLocalNow();
        var timeRange = CreateTimeRange(startTime, request.Duration);
        var existing = await scheduledRepository.LoadAllAsync(cancellationToken);
        var proposedTodo = new ScheduledTodo(
            request.Id,
            request.Title,
            timeRange,
            request.IsMandatory,
            ScheduledTodoPriority.Next(existing));
        var trial = ScheduleTrial.Insert(existing, proposedTodo);

        if (trial.IsSuccess)
        {
            await scheduledRepository.ReplaceAllAsync(
                trial.ScheduledTodos,
                cancellationToken);
        }

        return trial;
    }

    public async Task<UnscheduledTodo> AddUnscheduledTodoAsync(
        AddUnscheduledTodoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scheduledDate = ResolveScheduledDate(request);
        var todo = new UnscheduledTodo(
            request.Id,
            request.Title,
            scheduledDate,
            request.IsMandatory);

        await unscheduledRepository.SaveAsync(todo, cancellationToken);
        return todo;
    }

    public async Task<OpeningTodoState> LoadOpeningStateAsync(
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetLocalNow();
        await scheduledRepository.MarkCompletedBeforeAsync(now, cancellationToken);
        var scheduledTodos = await scheduledRepository.LoadAllAsync(cancellationToken);
        var currentTodo = ScheduledTodoSelector.GetCurrent(scheduledTodos, now);
        var today = DateOnly.FromDateTime(now.Date);
        var dueUnscheduledTodos = await unscheduledRepository.LoadDueOnOrBeforeAsync(
            today,
            cancellationToken);

        return new OpeningTodoState(
            currentTodo,
            dueUnscheduledTodos,
            ScheduleConflictDetector.Find(
                scheduledTodos.Where(todo => todo.TimeRange.End > now)));
    }

    public async Task<ScheduleTrialResult> PlanUnscheduledTodoAsync(
        PlanUnscheduledTodoRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingSchedule = await scheduledRepository.LoadAllAsync(cancellationToken);
        var alreadyScheduled = existingSchedule.FirstOrDefault(
            todo => todo.Id == request.UnscheduledTodoId);
        if (alreadyScheduled is not null)
        {
            await unscheduledRepository.MarkPlannedAsync(
                request.UnscheduledTodoId,
                cancellationToken);
            return ScheduleTrialResult.Unchanged(existingSchedule);
        }

        var source = await unscheduledRepository.LoadActiveByIdAsync(
            request.UnscheduledTodoId,
            cancellationToken);
        if (source is null)
        {
            throw new InvalidOperationException(
                "The selected future todo is no longer active.");
        }

        var startTime = request.StartTime ?? timeProvider.GetLocalNow();
        var proposedTodo = new ScheduledTodo(
            source.Id,
            source.Title,
            CreateTimeRange(startTime, request.Duration),
            source.IsMandatory,
            ScheduledTodoPriority.Next(existingSchedule));
        var trial = ScheduleTrial.Insert(existingSchedule, proposedTodo);
        if (!trial.IsSuccess)
        {
            return trial;
        }

        await scheduledRepository.ReplaceAllAsync(trial.ScheduledTodos, cancellationToken);
        await unscheduledRepository.MarkPlannedAsync(source.Id, cancellationToken);
        return trial;
    }

    public Task DeleteUnscheduledTodoAsync(
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

    private DateOnly ResolveScheduledDate(AddUnscheduledTodoRequest request)
    {
        var hasExactDate = request.ScheduledDate.HasValue;
        var hasRelativeDate = request.DaysFromToday.HasValue;

        if (hasExactDate == hasRelativeDate)
        {
            throw new ArgumentException(
                "Choose either an exact date or a number of days from today.",
                nameof(request));
        }

        if (hasExactDate)
        {
            return request.ScheduledDate!.Value;
        }

        if (request.DaysFromToday < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.DaysFromToday,
                "Days from today must be at least one.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);
        return today.AddDays(request.DaysFromToday!.Value);
    }

    private static TimeRange CreateTimeRange(
        DateTimeOffset startTime,
        TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "Duration must be greater than zero.");
        }

        try
        {
            return new TimeRange(startTime, startTime + duration);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "The duration places the end time outside the supported date range.");
        }
    }
}

namespace AttentionGuardian.Core;

public enum ScheduleConflictKind
{
    MandatoryEventCollision
}

public sealed record ScheduleConflict(
    ScheduleConflictKind Kind,
    ScheduledTodo ProposedTodo,
    ScheduledTodo MandatoryTodo);

public sealed record ScheduleTrialResult
{
    private ScheduleTrialResult(
        IReadOnlyList<ScheduledTodo> scheduledTodos,
        bool hasRolloverToNextDay,
        IReadOnlyList<ScheduleConflict> conflicts)
    {
        ScheduledTodos = scheduledTodos;
        HasRolloverToNextDay = hasRolloverToNextDay;
        Conflicts = conflicts;
    }

    public bool IsSuccess => ScheduledTodos.Count > 0;

    public bool HasUnresolvedConflicts => Conflicts.Count > 0;

    public IReadOnlyList<ScheduledTodo> ScheduledTodos { get; }

    public bool HasRolloverToNextDay { get; }

    public IReadOnlyList<ScheduleConflict> Conflicts { get; }

    public ScheduleConflict? Conflict => Conflicts.FirstOrDefault();

    public static ScheduleTrialResult Unchanged(
        IReadOnlyList<ScheduledTodo> scheduledTodos)
    {
        ArgumentNullException.ThrowIfNull(scheduledTodos);
        return new(scheduledTodos, false, []);
    }

    internal static ScheduleTrialResult Success(
        IReadOnlyList<ScheduledTodo> scheduledTodos,
        bool hasRolloverToNextDay,
        IReadOnlyList<ScheduleConflict>? conflicts = null) =>
        new(scheduledTodos, hasRolloverToNextDay, conflicts ?? []);

    internal static ScheduleTrialResult Failure(ScheduleConflict conflict) =>
        new(Array.Empty<ScheduledTodo>(), false, [conflict]);
}

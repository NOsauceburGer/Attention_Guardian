using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed record OpeningTodoState(
    ScheduledTodo? CurrentTodo,
    IReadOnlyList<UnscheduledTodo> DueUnscheduledTodos,
    IReadOnlyList<ScheduleConflict> MandatoryConflicts);

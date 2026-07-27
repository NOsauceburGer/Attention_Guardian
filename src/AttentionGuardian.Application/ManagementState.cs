using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed record ManagementState(
    IReadOnlyList<ScheduledTodo> ScheduledTodos,
    IReadOnlyList<MandatoryTodoGroup> MandatoryGroups);

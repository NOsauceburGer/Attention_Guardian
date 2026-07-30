namespace AttentionGuardian.Core;

public static class ScheduledTodoSelector
{
    public static ScheduledTodo? GetCurrent(
        IEnumerable<ScheduledTodo> scheduledTodos,
        DateTimeOffset currentTime)
    {
        ArgumentNullException.ThrowIfNull(scheduledTodos);

        var todos = scheduledTodos.ToArray();
        if (todos.Any(todo => todo is null))
        {
            throw new ArgumentException(
                "The scheduled todo collection cannot contain null values.",
                nameof(scheduledTodos));
        }

        return todos
            .Where(todo => todo.TimeRange.Contains(currentTime))
            .OrderByDescending(todo => todo.CurrentSelectionPriority)
            .ThenBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .FirstOrDefault();
    }
}

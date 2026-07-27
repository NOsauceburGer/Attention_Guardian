namespace AttentionGuardian.Core;

public static class ScheduledTodoPriority
{
    public static long Next(IEnumerable<ScheduledTodo> scheduledTodos)
    {
        ArgumentNullException.ThrowIfNull(scheduledTodos);

        var todos = scheduledTodos.ToArray();
        if (todos.Any(todo => todo is null))
        {
            throw new ArgumentException(
                "The scheduled todo collection cannot contain null values.",
                nameof(scheduledTodos));
        }

        var currentMaximum = todos.Length == 0
            ? 0
            : todos.Max(todo => todo.CurrentSelectionPriority);

        try
        {
            return checked(currentMaximum + 1);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException(
                "No additional current-selection priority can be allocated.");
        }
    }
}

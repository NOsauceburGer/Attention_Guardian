namespace AttentionGuardian.Core;

public static class ScheduleConflictDetector
{
    public static IReadOnlyList<ScheduleConflict> Find(
        IEnumerable<ScheduledTodo> scheduledTodos)
    {
        ArgumentNullException.ThrowIfNull(scheduledTodos);

        var todos = scheduledTodos.ToArray();
        if (todos.Any(todo => todo is null))
        {
            throw new ArgumentException(
                "The scheduled todo collection cannot contain null values.",
                nameof(scheduledTodos));
        }

        if (todos.Select(todo => todo.Id).Distinct().Count() != todos.Length)
        {
            throw new ArgumentException(
                "The scheduled todo collection cannot contain duplicate identifiers.",
                nameof(scheduledTodos));
        }

        var mandatoryTodos = todos
            .Where(todo => todo.IsMandatory)
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToArray();
        var conflicts = new List<ScheduleConflict>();

        for (var firstIndex = 0; firstIndex < mandatoryTodos.Length; firstIndex++)
        {
            var first = mandatoryTodos[firstIndex];
            for (var secondIndex = firstIndex + 1;
                 secondIndex < mandatoryTodos.Length;
                 secondIndex++)
            {
                var second = mandatoryTodos[secondIndex];
                if (second.TimeRange.Start >= first.TimeRange.End)
                {
                    break;
                }

                if (!first.TimeRange.Overlaps(second.TimeRange))
                {
                    continue;
                }

                var preferred = first.CurrentSelectionPriority
                    >= second.CurrentSelectionPriority
                    ? first
                    : second;
                var other = preferred.Id == first.Id ? second : first;
                conflicts.Add(
                    new ScheduleConflict(
                        ScheduleConflictKind.MandatoryEventCollision,
                        preferred,
                        other));
            }
        }

        return conflicts;
    }
}

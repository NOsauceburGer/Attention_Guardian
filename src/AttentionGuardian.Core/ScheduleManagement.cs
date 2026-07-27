namespace AttentionGuardian.Core;

public sealed record MandatoryTodoGroup(
    DateTimeOffset OriginalStart,
    IReadOnlyList<ScheduledTodo> Todos);

public sealed record ScheduleReorderResult(
    IReadOnlyList<ScheduledTodo> ScheduledTodos,
    int ActualIndex,
    bool UsedFallbackPosition,
    bool HasRolloverToNextDay);

public static class ScheduleManagement
{
    public const string BreakTitle = "休息";

    public static IReadOnlyList<MandatoryTodoGroup> FindMandatoryGroups(
        IEnumerable<ScheduledTodo> schedule)
    {
        var ordered = ValidateAndOrder(schedule)
            .Where(todo => todo.IsMandatory)
            .ToArray();
        var groups = new List<MandatoryTodoGroup>();
        var current = new List<ScheduledTodo>();
        DateTimeOffset groupEnd = default;

        foreach (var todo in ordered)
        {
            if (current.Count == 0 || todo.TimeRange.Start <= groupEnd)
            {
                current.Add(todo);
                if (todo.TimeRange.End > groupEnd)
                {
                    groupEnd = todo.TimeRange.End;
                }

                continue;
            }

            AddGroupWhenContinuous(groups, current);
            current = [todo];
            groupEnd = todo.TimeRange.End;
        }

        AddGroupWhenContinuous(groups, current);
        return groups;
    }

    public static ScheduleReorderResult Reorder(
        IEnumerable<ScheduledTodo> schedule,
        Guid todoId,
        int requestedIndex)
    {
        var ordered = ValidateAndOrder(schedule);
        if (requestedIndex < 0 || requestedIndex >= ordered.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedIndex));
        }

        var moving = ordered.SingleOrDefault(todo => todo.Id == todoId)
            ?? throw new ArgumentException("The selected todo does not exist.", nameof(todoId));

        if (moving.IsMandatory)
        {
            return ReorderMandatoryWithinGroup(ordered, moving, requestedIndex);
        }

        var desired = ordered.Where(todo => todo.Id != todoId).ToList();
        desired.Insert(requestedIndex, moving);
        var rebuilt = RebuildFromAnchor(desired, ordered[0].TimeRange.Start);
        var actualIndex = IndexOf(rebuilt, todoId);
        return new(
            rebuilt,
            actualIndex,
            actualIndex != requestedIndex,
            HasRollover(ordered, rebuilt));
    }

    public static IReadOnlyList<ScheduledTodo> Delete(
        IEnumerable<ScheduledTodo> schedule,
        Guid todoId)
    {
        var ordered = ValidateAndOrder(schedule);
        var deletedIndex = ordered.FindIndex(todo => todo.Id == todoId);
        if (deletedIndex < 0)
        {
            throw new ArgumentException("The selected todo does not exist.", nameof(todoId));
        }

        var deleted = ordered[deletedIndex];
        var before = ordered.Take(deletedIndex).ToList();
        var after = ordered.Skip(deletedIndex + 1).ToList();
        var rebuiltAfter = RebuildFromAnchor(after, deleted.TimeRange.Start);
        return before
            .Concat(rebuiltAfter)
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToArray();
    }

    public static IReadOnlyList<ScheduledTodo> Edit(
        IEnumerable<ScheduledTodo> schedule,
        Guid todoId,
        string title,
        TimeSpan duration,
        bool isMandatory)
    {
        var ordered = ValidateAndOrder(schedule);
        var index = ordered.FindIndex(todo => todo.Id == todoId);
        if (index < 0)
        {
            throw new ArgumentException("The selected todo does not exist.", nameof(todoId));
        }

        if (ordered[index].Title == BreakTitle && title != BreakTitle)
        {
            throw new InvalidOperationException("Break events cannot be renamed.");
        }

        var edited = ordered[index].Edit(title, duration, isMandatory);
        var desired = ordered.ToList();
        desired[index] = edited;
        return RebuildFromAnchor(desired, ordered[0].TimeRange.Start);
    }

    public static ScheduleTrialResult InsertBreak(
        IEnumerable<ScheduledTodo> schedule,
        Guid id,
        DateTimeOffset start,
        TimeSpan duration,
        long currentSelectionPriority = 0)
    {
        var breakTodo = new ScheduledTodo(
            id,
            BreakTitle,
            new TimeRange(start, start + duration),
            isMandatory: false,
            currentSelectionPriority);
        return ScheduleTrial.Insert(schedule, breakTodo);
    }

    private static ScheduleReorderResult ReorderMandatoryWithinGroup(
        List<ScheduledTodo> ordered,
        ScheduledTodo moving,
        int requestedIndex)
    {
        var group = FindMandatoryGroups(ordered)
            .SingleOrDefault(candidate => candidate.Todos.Any(todo => todo.Id == moving.Id));
        if (group is null)
        {
            throw new InvalidOperationException(
                "A mandatory todo can only move inside a continuous mandatory group.");
        }

        var groupIndices = group.Todos
            .Select(todo => ordered.FindIndex(candidate => candidate.Id == todo.Id))
            .Order()
            .ToArray();
        if (!groupIndices.Contains(requestedIndex))
        {
            throw new InvalidOperationException(
                "A mandatory todo cannot move outside its continuous group.");
        }

        var reorderedGroup = group.Todos.Where(todo => todo.Id != moving.Id).ToList();
        var targetInGroup = Array.IndexOf(groupIndices, requestedIndex);
        reorderedGroup.Insert(targetInGroup, moving);

        var cursor = group.OriginalStart;
        var replacements = new Dictionary<Guid, ScheduledTodo>();
        foreach (var todo in reorderedGroup)
        {
            var shifted = todo.MoveTo(cursor);
            replacements[todo.Id] = shifted;
            cursor = shifted.TimeRange.End;
        }

        var desired = ordered
            .Select(todo => replacements.GetValueOrDefault(todo.Id, todo))
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToList();
        var rebuilt = RebuildFromAnchor(desired, ordered[0].TimeRange.Start);
        return new(
            rebuilt,
            IndexOf(rebuilt, moving.Id),
            false,
            HasRollover(ordered, rebuilt));
    }

    private static IReadOnlyList<ScheduledTodo> RebuildFromAnchor(
        IReadOnlyList<ScheduledTodo> desiredOrder,
        DateTimeOffset anchor)
    {
        var mandatory = desiredOrder
            .Where(todo => todo.IsMandatory)
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToArray();
        var rebuilt = new List<ScheduledTodo>(desiredOrder.Count);
        var cursor = anchor;

        foreach (var todo in desiredOrder)
        {
            if (todo.IsMandatory)
            {
                rebuilt.Add(todo);
                if (todo.TimeRange.End > cursor)
                {
                    cursor = todo.TimeRange.End;
                }

                continue;
            }

            var shifted = todo.MoveTo(cursor);
            foreach (var blocker in mandatory)
            {
                if (shifted.TimeRange.Overlaps(blocker.TimeRange))
                {
                    shifted = shifted.MoveTo(blocker.TimeRange.End);
                }
            }

            rebuilt.Add(shifted);
            cursor = shifted.TimeRange.End;
        }

        return rebuilt
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToArray();
    }

    private static List<ScheduledTodo> ValidateAndOrder(
        IEnumerable<ScheduledTodo> schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        var items = schedule.ToArray();
        if (items.Any(todo => todo is null))
        {
            throw new ArgumentException("The schedule cannot contain null values.", nameof(schedule));
        }

        if (items.Select(todo => todo.Id).Distinct().Count() != items.Length)
        {
            throw new ArgumentException(
                "The schedule cannot contain duplicate identifiers.",
                nameof(schedule));
        }

        return items
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToList();
    }

    private static void AddGroupWhenContinuous(
        ICollection<MandatoryTodoGroup> groups,
        IReadOnlyList<ScheduledTodo> current)
    {
        if (current.Count >= 2)
        {
            groups.Add(new(current[0].TimeRange.Start, current.ToArray()));
        }
    }

    private static int IndexOf(IReadOnlyList<ScheduledTodo> todos, Guid id)
    {
        for (var index = 0; index < todos.Count; index++)
        {
            if (todos[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool HasRollover(
        IReadOnlyList<ScheduledTodo> original,
        IReadOnlyList<ScheduledTodo> rebuilt)
    {
        var originalById = original.ToDictionary(todo => todo.Id);
        return rebuilt.Any(
            todo => todo.ScheduleDate > originalById[todo.Id].ScheduleDate);
    }
}

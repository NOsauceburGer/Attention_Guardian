namespace AttentionGuardian.Core;

public static class ScheduleTrial
{
    public static ScheduleTrialResult Insert(
        IEnumerable<ScheduledTodo> existingSchedule,
        ScheduledTodo proposedTodo)
    {
        ArgumentNullException.ThrowIfNull(existingSchedule);
        ArgumentNullException.ThrowIfNull(proposedTodo);

        var existing = existingSchedule.ToArray();
        ValidateExistingSchedule(existing, proposedTodo);

        var ordered = existing
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToArray();

        var mandatoryTodos = ordered
            .Where(todo => todo.IsMandatory)
            .ToArray();

        var conflicts = proposedTodo.IsMandatory
            ? mandatoryTodos
                .Where(todo => todo.TimeRange.Overlaps(proposedTodo.TimeRange))
                .Select(todo => new ScheduleConflict(
                    ScheduleConflictKind.MandatoryEventCollision,
                    proposedTodo,
                    todo))
                .ToArray()
            : [];

        var originalProposedTodo = proposedTodo;
        if (!proposedTodo.IsMandatory)
        {
            proposedTodo = MovePastMandatoryTodos(proposedTodo, mandatoryTodos);
        }

        var result = new List<ScheduledTodo>(ordered.Length + 1);
        var affected = new List<ScheduledTodo>();

        foreach (var todo in ordered)
        {
            if (todo.TimeRange.End <= proposedTodo.TimeRange.Start)
            {
                result.Add(todo);
            }
            else
            {
                affected.Add(todo);
            }
        }

        result.Add(proposedTodo);
        var occupiedUntil = proposedTodo.TimeRange.End;
        var chainIsActive = true;
        var hasRollover = proposedTodo.ScheduleDate > originalProposedTodo.ScheduleDate;

        foreach (var todo in affected)
        {
            if (!chainIsActive)
            {
                result.Add(todo);
                continue;
            }

            if (todo.IsMandatory)
            {
                if (todo.TimeRange.Start < occupiedUntil)
                {
                    occupiedUntil = todo.TimeRange.End > occupiedUntil
                        ? todo.TimeRange.End
                        : occupiedUntil;
                    result.Add(todo);
                    continue;
                }

                chainIsActive = false;
                result.Add(todo);
                continue;
            }

            if (todo.TimeRange.Start >= occupiedUntil)
            {
                chainIsActive = false;
                result.Add(todo);
                continue;
            }

            var shifted = MovePastMandatoryTodos(todo.MoveTo(occupiedUntil), mandatoryTodos);
            hasRollover |= shifted.ScheduleDate > todo.ScheduleDate;
            result.Add(shifted);
            occupiedUntil = shifted.TimeRange.End;
        }

        var finalSchedule = result
            .OrderBy(todo => todo.TimeRange.Start)
            .ThenBy(todo => todo.TimeRange.End)
            .ThenBy(todo => todo.Id)
            .ToArray();

        return ScheduleTrialResult.Success(finalSchedule, hasRollover, conflicts);
    }

    private static ScheduledTodo MovePastMandatoryTodos(
        ScheduledTodo todo,
        IReadOnlyList<ScheduledTodo> mandatoryTodos)
    {
        var moved = todo;

        foreach (var mandatoryTodo in mandatoryTodos)
        {
            if (moved.TimeRange.Overlaps(mandatoryTodo.TimeRange))
            {
                moved = moved.MoveTo(mandatoryTodo.TimeRange.End);
            }
        }

        return moved;
    }

    private static void ValidateExistingSchedule(
        IReadOnlyList<ScheduledTodo> existing,
        ScheduledTodo proposedTodo)
    {
        if (existing.Any(todo => todo is null))
        {
            throw new ArgumentException(
                "The existing schedule cannot contain null values.",
                nameof(existing));
        }

        if (existing.Any(todo => todo.Id == proposedTodo.Id))
        {
            throw new ArgumentException(
                "The proposed todo identifier must not already exist in the schedule.",
                nameof(proposedTodo));
        }

        if (existing.Select(todo => todo.Id).Distinct().Count() != existing.Count)
        {
            throw new ArgumentException(
                "The existing schedule cannot contain duplicate identifiers.",
                nameof(existing));
        }
    }
}

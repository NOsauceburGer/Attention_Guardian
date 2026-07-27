using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class ScheduleConflictDetectorTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void Find_ReturnsEveryOverlappingMandatoryPair()
    {
        var first = Todo(Start, Start.AddHours(2), priority: 1);
        var second = Todo(Start.AddMinutes(30), Start.AddHours(2), priority: 2);
        var third = Todo(Start.AddHours(1), Start.AddHours(3), priority: 3);

        var conflicts = ScheduleConflictDetector.Find([third, first, second]);

        Assert.Equal(3, conflicts.Count);
        Assert.All(
            conflicts,
            conflict => Assert.Equal(
                ScheduleConflictKind.MandatoryEventCollision,
                conflict.Kind));
    }

    [Fact]
    public void Find_DoesNotTreatTouchingMandatoryTodosAsConflict()
    {
        var first = Todo(Start, Start.AddHours(1), priority: 1);
        var second = Todo(Start.AddHours(1), Start.AddHours(2), priority: 2);

        var conflicts = ScheduleConflictDetector.Find([first, second]);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void Find_PutsHigherPriorityTodoInPreferredPosition()
    {
        var existing = Todo(Start, Start.AddHours(1), priority: 4);
        var newlyEntered = Todo(
            Start.AddMinutes(15),
            Start.AddHours(1),
            priority: 5);

        var conflict = Assert.Single(
            ScheduleConflictDetector.Find([existing, newlyEntered]));

        Assert.Same(newlyEntered, conflict.ProposedTodo);
        Assert.Same(existing, conflict.MandatoryTodo);
    }

    private static ScheduledTodo Todo(
        DateTimeOffset start,
        DateTimeOffset end,
        long priority) =>
        new(
            Guid.NewGuid(),
            "mandatory",
            new TimeRange(start, end),
            isMandatory: true,
            currentSelectionPriority: priority);
}

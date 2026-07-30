using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class ScheduledTodoPriorityTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void Next_ReturnsOneAboveCurrentMaximum()
    {
        var result = ScheduledTodoPriority.Next(
            [Todo(3), Todo(8), Todo(5)]);

        Assert.Equal(9, result);
    }

    [Fact]
    public void Next_WhenMaximumIsExhausted_ThrowsClearError()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ScheduledTodoPriority.Next([Todo(long.MaxValue)]));

        Assert.Contains("priority", exception.Message);
    }

    [Fact]
    public void MoveAndEdit_PreserveCurrentSelectionPriority()
    {
        var original = Todo(7);

        var moved = original.MoveTo(Start.AddHours(2));
        var edited = moved.Edit("edited", TimeSpan.FromMinutes(30), true);

        Assert.Equal(7, moved.CurrentSelectionPriority);
        Assert.Equal(7, edited.CurrentSelectionPriority);
    }

    [Fact]
    public void Constructor_RejectsNegativeCurrentSelectionPriority()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Todo(-1));
    }

    private static ScheduledTodo Todo(long priority) =>
        new(
            Guid.NewGuid(),
            "todo",
            new TimeRange(Start, Start.AddHours(1)),
            currentSelectionPriority: priority);
}

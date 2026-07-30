using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class ScheduledTodoSelectorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 0, 15, 0, TimeSpan.FromHours(8));

    [Fact]
    public void GetCurrent_MatchesTodoThatCrossedMidnight()
    {
        var crossingMidnight = Todo(
            new DateTimeOffset(2026, 7, 25, 23, 30, 0, TimeSpan.FromHours(8)),
            new DateTimeOffset(2026, 7, 26, 0, 30, 0, TimeSpan.FromHours(8)));

        var result = ScheduledTodoSelector.GetCurrent([crossingMidnight], Now);

        Assert.Same(crossingMidnight, result);
    }

    [Fact]
    public void GetCurrent_AtEndBoundary_ReturnsNull()
    {
        var endingNow = Todo(Now.AddHours(-1), Now);

        var result = ScheduledTodoSelector.GetCurrent([endingNow], Now);

        Assert.Null(result);
    }

    [Fact]
    public void GetCurrent_FromUnorderedSchedule_ReturnsMatchingTodo()
    {
        var later = Todo(Now.AddHours(1), Now.AddHours(2));
        var current = Todo(Now.AddMinutes(-15), Now.AddMinutes(15));

        var result = ScheduledTodoSelector.GetCurrent([later, current], Now);

        Assert.Same(current, result);
    }

    [Fact]
    public void GetCurrent_FromOverlappingTodos_ReturnsMostRecentlyEnteredTodo()
    {
        var existing = Todo(
            Now.AddMinutes(-30),
            Now.AddMinutes(30),
            currentSelectionPriority: 4);
        var newlyEntered = Todo(
            Now.AddMinutes(-15),
            Now.AddMinutes(45),
            currentSelectionPriority: 5);

        var result = ScheduledTodoSelector.GetCurrent(
            [existing, newlyEntered],
            Now);

        Assert.Same(newlyEntered, result);
    }

    [Fact]
    public void GetCurrent_WithEqualLegacyPriority_UsesDeterministicTimeOrder()
    {
        var earlier = Todo(Now.AddMinutes(-30), Now.AddMinutes(30));
        var later = Todo(Now.AddMinutes(-15), Now.AddMinutes(45));

        var result = ScheduledTodoSelector.GetCurrent([later, earlier], Now);

        Assert.Same(earlier, result);
    }

    private static ScheduledTodo Todo(
        DateTimeOffset start,
        DateTimeOffset end,
        long currentSelectionPriority) =>
        new(
            Guid.NewGuid(),
            "todo",
            new TimeRange(start, end),
            currentSelectionPriority: currentSelectionPriority);

    private static ScheduledTodo Todo(DateTimeOffset start, DateTimeOffset end) =>
        new(Guid.NewGuid(), "测试待办", new TimeRange(start, end));
}

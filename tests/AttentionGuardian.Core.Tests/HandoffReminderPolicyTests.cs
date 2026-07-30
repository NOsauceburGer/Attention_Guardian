using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class HandoffReminderPolicyTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.FromHours(8));

    [Fact]
    public void Evaluate_AtFiveMinuteBoundary_ReturnsCurrentAndAdjacentNext()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(30));
        var next = Todo("下一项", current.TimeRange.End, TimeSpan.FromMinutes(20));

        var result = HandoffReminderPolicy.Evaluate(
            [next, current],
            current.TimeRange.End - TimeSpan.FromMinutes(5));

        Assert.True(result.ShouldNotifyNow);
        Assert.Same(current, result.CurrentTodo);
        Assert.Same(next, result.NextTodo);
        Assert.Equal(current.TimeRange.End - TimeSpan.FromMinutes(5), result.ReminderAt);
        Assert.Equal(HandoffReminderIneligibility.None, result.Ineligibility);
    }

    [Fact]
    public void Evaluate_BeforeFiveMinuteBoundary_DoesNotNotify()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(30));
        var next = Todo("下一项", current.TimeRange.End, TimeSpan.FromMinutes(20));

        var result = HandoffReminderPolicy.Evaluate(
            [current, next],
            current.TimeRange.End - TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));

        Assert.False(result.ShouldNotifyNow);
        Assert.Equal(
            HandoffReminderIneligibility.OutsideReminderWindow,
            result.Ineligibility);
    }

    [Fact]
    public void Evaluate_CurrentTodoExactlyFiveMinutes_NotifiesFromItsStart()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(5));
        var next = Todo("下一项", current.TimeRange.End, TimeSpan.FromMinutes(20));

        var result = HandoffReminderPolicy.Evaluate([current, next], Start);

        Assert.True(result.ShouldNotifyNow);
    }

    [Fact]
    public void Evaluate_CurrentTodoShorterThanFiveMinutes_DoesNotNotify()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(4));
        var next = Todo("下一项", current.TimeRange.End, TimeSpan.FromMinutes(20));

        var result = HandoffReminderPolicy.Evaluate([current, next], Start);

        Assert.False(result.ShouldNotifyNow);
        Assert.Equal(
            HandoffReminderIneligibility.CurrentTodoTooShort,
            result.Ineligibility);
    }

    [Fact]
    public void Evaluate_WithGapBeforeNextTodo_DoesNotNotify()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(30));
        var next = Todo(
            "下一项",
            current.TimeRange.End + TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(20));

        var result = HandoffReminderPolicy.Evaluate(
            [current, next],
            current.TimeRange.End - TimeSpan.FromMinutes(5));

        Assert.False(result.ShouldNotifyNow);
        Assert.Equal(
            HandoffReminderIneligibility.NoAdjacentNextTodo,
            result.Ineligibility);
    }

    [Fact]
    public void Evaluate_BeforeBreak_DoesNotNotify()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(30));
        var next = Todo(
            ScheduleManagement.BreakTitle,
            current.TimeRange.End,
            TimeSpan.FromMinutes(20));

        var result = HandoffReminderPolicy.Evaluate(
            [current, next],
            current.TimeRange.End - TimeSpan.FromMinutes(5));

        Assert.False(result.ShouldNotifyNow);
        Assert.Equal(
            HandoffReminderIneligibility.NextTodoIsBreak,
            result.Ineligibility);
    }

    [Fact]
    public void Evaluate_AfterBreakBeforeAdjacentTask_NotifiesNormally()
    {
        var current = Todo(
            ScheduleManagement.BreakTitle,
            Start,
            TimeSpan.FromMinutes(20));
        var next = Todo("下一项", current.TimeRange.End, TimeSpan.FromMinutes(20));

        var result = HandoffReminderPolicy.Evaluate(
            [current, next],
            current.TimeRange.End - TimeSpan.FromMinutes(5));

        Assert.True(result.ShouldNotifyNow);
        Assert.Same(next, result.NextTodo);
    }

    [Fact]
    public void Evaluate_AtCurrentEnd_DoesNotReuseFinishedTodo()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(30));
        var next = Todo("下一项", current.TimeRange.End, TimeSpan.FromMinutes(20));

        var result = HandoffReminderPolicy.Evaluate(
            [current, next],
            current.TimeRange.End);

        Assert.False(result.ShouldNotifyNow);
        Assert.Same(next, result.CurrentTodo);
    }

    private static ScheduledTodo Todo(
        string title,
        DateTimeOffset start,
        TimeSpan duration) =>
        new(Guid.NewGuid(), title, new TimeRange(start, start + duration));
}

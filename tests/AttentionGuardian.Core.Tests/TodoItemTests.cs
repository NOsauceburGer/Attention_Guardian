using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class TodoItemTests
{
    [Fact]
    public void ScheduledTodo_StoresTimedInputAndMandatoryFlag()
    {
        var range = Range(9, 0, 10, 0);
        var todo = new ScheduledTodo(Guid.NewGuid(), "  深度工作  ", range, true);

        Assert.Equal("深度工作", todo.Title);
        Assert.Equal(range, todo.TimeRange);
        Assert.Equal(new DateOnly(2026, 7, 26), todo.ScheduleDate);
        Assert.True(todo.IsMandatory);
    }

    [Fact]
    public void UnscheduledTodo_StoresDateWithoutInventingTimeRange()
    {
        var date = new DateOnly(2026, 7, 27);
        var todo = new UnscheduledTodo(Guid.NewGuid(), "购买材料", date);

        Assert.Equal(date, todo.ScheduledDate);
        Assert.False(todo.IsMandatory);
    }

    [Fact]
    public void TodoItem_RejectsEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() =>
            new UnscheduledTodo(Guid.Empty, "购买材料", new DateOnly(2026, 7, 27)));
    }

    [Fact]
    public void TodoItem_RejectsBlankTitle()
    {
        Assert.Throws<ArgumentException>(() =>
            new UnscheduledTodo(Guid.NewGuid(), "  ", new DateOnly(2026, 7, 27)));
    }

    private static TimeRange Range(int startHour, int startMinute, int endHour, int endMinute) =>
        new(
            At(startHour, startMinute),
            At(endHour, endMinute));

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 7, 26, hour, minute, 0, TimeSpan.FromHours(8));
}

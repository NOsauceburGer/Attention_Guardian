using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class ScheduleTrialTests
{
    [Fact]
    public void Insert_IntoGap_LeavesExistingScheduleUnchanged()
    {
        var before = Todo("之前", 8, 0, 9, 0);
        var after = Todo("之后", 11, 0, 12, 0);
        var proposed = Todo("新增", 9, 30, 10, 0);

        var result = ScheduleTrial.Insert([after, before], proposed);

        Assert.True(result.IsSuccess);
        Assert.Equal([before, proposed, after], result.ScheduledTodos);
        Assert.False(result.HasRolloverToNextDay);
    }

    [Fact]
    public void Insert_OverlappingNormalTodo_ShiftsItWithoutChangingDuration()
    {
        var existing = Todo("普通", 9, 30, 10, 30);
        var proposed = Todo("新增", 9, 0, 10, 0);

        var result = ScheduleTrial.Insert([existing], proposed);

        var shifted = Assert.Single(result.ScheduledTodos, todo => todo.Id == existing.Id);
        Assert.Equal(At(10, 0), shifted.TimeRange.Start);
        Assert.Equal(At(11, 0), shifted.TimeRange.End);
        Assert.Equal(existing.TimeRange.Duration, shifted.TimeRange.Duration);
        Assert.Equal(At(9, 30), existing.TimeRange.Start);
    }

    [Fact]
    public void Insert_CascadesAcrossConsecutiveNormalTodos()
    {
        var first = Todo("第一项", 9, 30, 10, 0);
        var second = Todo("第二项", 10, 0, 10, 30);
        var proposed = Todo("新增", 9, 0, 10, 0);

        var result = ScheduleTrial.Insert([second, first], proposed);

        Assert.Equal(At(10, 0), Find(result, first).TimeRange.Start);
        Assert.Equal(At(10, 30), Find(result, second).TimeRange.Start);
    }

    [Fact]
    public void Insert_ChainStopsAtAvailableGap()
    {
        var shifted = Todo("受影响", 9, 30, 10, 0);
        var later = Todo("不受影响", 11, 0, 12, 0);
        var proposed = Todo("新增", 9, 0, 10, 0);

        var result = ScheduleTrial.Insert([later, shifted], proposed);

        Assert.Equal(At(10, 0), Find(result, shifted).TimeRange.Start);
        Assert.Same(later, Find(result, later));
    }

    [Fact]
    public void Insert_ChainTouchingMandatoryBoundary_Succeeds()
    {
        var normal = Todo("普通", 9, 30, 10, 0);
        var mandatory = Todo("强制", 10, 30, 11, 0, mandatory: true);
        var proposed = Todo("新增", 9, 0, 10, 0);

        var result = ScheduleTrial.Insert([mandatory, normal], proposed);

        Assert.True(result.IsSuccess);
        Assert.Equal(At(10, 0), Find(result, normal).TimeRange.Start);
        Assert.Same(mandatory, Find(result, mandatory));
    }

    [Fact]
    public void Insert_WhenCascadeWouldHitMandatoryTodo_MovesNormalTodoPastIt()
    {
        var normal = Todo("普通", 9, 30, 10, 30);
        var mandatory = Todo("强制", 10, 30, 11, 0, mandatory: true);
        var proposed = Todo("新增", 9, 0, 10, 0);

        var result = ScheduleTrial.Insert([normal, mandatory], proposed);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Conflict);
        Assert.Equal(At(11, 0), Find(result, normal).TimeRange.Start);
        Assert.Equal(At(12, 0), Find(result, normal).TimeRange.End);
        Assert.Same(mandatory, Find(result, mandatory));
        Assert.Equal(At(9, 30), normal.TimeRange.Start);
    }

    [Fact]
    public void Insert_NormalProposedTodoOverlappingMandatoryTodo_MovesPastIt()
    {
        var mandatory = Todo("强制", 9, 30, 10, 30, mandatory: true);
        var proposed = Todo("新增", 9, 0, 10, 0);

        var result = ScheduleTrial.Insert([mandatory], proposed);

        Assert.True(result.IsSuccess);
        Assert.Equal(At(10, 30), Find(result, proposed).TimeRange.Start);
        Assert.Equal(At(11, 30), Find(result, proposed).TimeRange.End);
        Assert.Null(result.Conflict);
    }

    [Fact]
    public void Insert_MandatoryTodoAgainstMandatoryTodo_PreservesOverlapForManagement()
    {
        var existing = Todo("已有强制", 9, 30, 10, 30, mandatory: true);
        var proposed = Todo("新增强制", 9, 0, 10, 0, mandatory: true);

        var result = ScheduleTrial.Insert([existing], proposed);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasUnresolvedConflicts);
        Assert.Equal(2, result.ScheduledTodos.Count);
        Assert.Same(proposed, result.Conflict?.ProposedTodo);
        Assert.Same(existing, result.Conflict?.MandatoryTodo);
    }

    [Fact]
    public void Insert_NormalTodo_SkipsConsecutiveMandatoryTodosAndContinuesCascade()
    {
        var firstMandatory = Todo("第一强制", 10, 0, 10, 30, mandatory: true);
        var secondMandatory = Todo("第二强制", 11, 0, 11, 30, mandatory: true);
        var normal = Todo("普通", 9, 30, 10, 30);
        var later = Todo("后续普通", 11, 30, 12, 0);
        var proposed = Todo("新增", 9, 0, 10, 0);

        var result = ScheduleTrial.Insert(
            [later, secondMandatory, normal, firstMandatory],
            proposed);

        Assert.True(result.IsSuccess);
        Assert.Equal(At(11, 30), Find(result, normal).TimeRange.Start);
        Assert.Equal(At(12, 30), Find(result, normal).TimeRange.End);
        Assert.Equal(At(12, 30), Find(result, later).TimeRange.Start);
        Assert.Equal(At(13, 0), Find(result, later).TimeRange.End);
    }

    [Fact]
    public void Insert_SkippingMandatoryTodoPastMidnight_SetsRolloverFlag()
    {
        var mandatory = TodoAcrossDates(
            "午夜强制",
            At(23, 30),
            AtNextDay(0, 15),
            mandatory: true);
        var proposed = Todo("新增普通", 23, 0, 23, 45);

        var result = ScheduleTrial.Insert([mandatory], proposed);

        Assert.True(result.IsSuccess);
        Assert.Equal(AtNextDay(0, 15), Find(result, proposed).TimeRange.Start);
        Assert.Equal(AtNextDay(1, 0), Find(result, proposed).TimeRange.End);
        Assert.True(result.HasRolloverToNextDay);
    }

    [Fact]
    public void Insert_ShiftingPastMidnight_UpdatesDateAndSetsRolloverFlag()
    {
        var existing = TodoAcrossDates(
            "夜间任务",
            At(23, 30),
            AtNextDay(0, 30));
        var proposed = TodoAcrossDates(
            "新增",
            At(23, 0),
            AtNextDay(0, 15));

        var result = ScheduleTrial.Insert([existing], proposed);

        var shifted = Find(result, existing);
        Assert.Equal(AtNextDay(0, 15), shifted.TimeRange.Start);
        Assert.Equal(AtNextDay(1, 15), shifted.TimeRange.End);
        Assert.Equal(new DateOnly(2026, 7, 26), shifted.ScheduleDate);
        Assert.True(result.HasRolloverToNextDay);
    }

    private static ScheduledTodo Find(ScheduleTrialResult result, ScheduledTodo original) =>
        Assert.Single(result.ScheduledTodos, todo => todo.Id == original.Id);

    private static ScheduledTodo Todo(
        string title,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        bool mandatory = false) =>
        TodoAcrossDates(title, At(startHour, startMinute), At(endHour, endMinute), mandatory);

    private static ScheduledTodo TodoAcrossDates(
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        bool mandatory = false) =>
        new(Guid.NewGuid(), title, new TimeRange(start, end), mandatory);

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 7, 25, hour, minute, 0, TimeSpan.FromHours(8));

    private static DateTimeOffset AtNextDay(int hour, int minute) =>
        new(2026, 7, 26, hour, minute, 0, TimeSpan.FromHours(8));
}

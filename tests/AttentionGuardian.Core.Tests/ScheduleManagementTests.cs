using AttentionGuardian.Core;

namespace AttentionGuardian.Core.Tests;

public sealed class ScheduleManagementTests
{
    [Fact]
    public void FindMandatoryGroups_UsesOverlapAndTouchingButStopsAtGap()
    {
        var first = Todo("一", 9, 0, 10, 0, mandatory: true);
        var touching = Todo("二", 10, 0, 11, 0, mandatory: true);
        var afterGap = Todo("三", 11, 30, 12, 0, mandatory: true);

        var groups = ScheduleManagement.FindMandatoryGroups([afterGap, touching, first]);

        var group = Assert.Single(groups);
        Assert.Equal([first, touching], group.Todos);
    }

    [Fact]
    public void Reorder_NormalTodos_RebuildsFromEarliestStartAndKeepsDurations()
    {
        var first = Todo("一", 9, 0, 9, 30);
        var second = Todo("二", 9, 30, 10, 30);
        var third = Todo("三", 10, 30, 10, 45);

        var result = ScheduleManagement.Reorder([first, second, third], third.Id, 0);

        Assert.Equal([third.Id, first.Id, second.Id], result.ScheduledTodos.Select(todo => todo.Id));
        Assert.Equal(At(9, 0), result.ScheduledTodos[0].TimeRange.Start);
        Assert.Equal(TimeSpan.FromMinutes(15), result.ScheduledTodos[0].TimeRange.Duration);
        Assert.Equal(At(9, 15), result.ScheduledTodos[1].TimeRange.Start);
        Assert.False(result.UsedFallbackPosition);
    }

    [Fact]
    public void Reorder_NormalTodoBlockedByMandatory_FallsBackAfterBlocker()
    {
        var first = Todo("普通一", 9, 0, 9, 30);
        var mandatory = Todo("不可移动", 9, 30, 10, 30, mandatory: true);
        var moving = Todo("普通二", 10, 30, 11, 30);

        var result = ScheduleManagement.Reorder([first, mandatory, moving], moving.Id, 1);

        Assert.True(result.UsedFallbackPosition);
        Assert.Equal(2, result.ActualIndex);
        Assert.Equal(At(10, 30), result.ScheduledTodos[2].TimeRange.Start);
    }

    [Fact]
    public void Reorder_MandatoryOutsideContinuousGroup_IsRejected()
    {
        var first = Todo("强制一", 9, 0, 10, 0, mandatory: true);
        var second = Todo("强制二", 10, 0, 11, 0, mandatory: true);
        var normal = Todo("普通", 11, 0, 12, 0);

        Assert.Throws<InvalidOperationException>(
            () => ScheduleManagement.Reorder([first, second, normal], first.Id, 2));
    }

    [Fact]
    public void Reorder_MandatoryInsideContinuousGroup_RecalculatesFromGroupStart()
    {
        var first = Todo("强制一", 9, 0, 10, 0, mandatory: true);
        var second = Todo("强制二", 9, 30, 10, 30, mandatory: true);

        var result = ScheduleManagement.Reorder([first, second], second.Id, 0);

        Assert.Equal(second.Id, result.ScheduledTodos[0].Id);
        Assert.Equal(At(9, 0), result.ScheduledTodos[0].TimeRange.Start);
        Assert.Equal(At(10, 0), result.ScheduledTodos[1].TimeRange.Start);
        Assert.All(result.ScheduledTodos, todo => Assert.True(todo.IsMandatory));
    }

    [Fact]
    public void Delete_MakesNextTodoUseDeletedStart()
    {
        var first = Todo("一", 9, 0, 9, 30);
        var deleted = Todo("删除", 9, 30, 10, 0);
        var next = Todo("下一项", 10, 0, 11, 0);

        var result = ScheduleManagement.Delete([first, deleted, next], deleted.Id);

        Assert.Equal([first.Id, next.Id], result.Select(todo => todo.Id));
        Assert.Equal(At(9, 30), result[1].TimeRange.Start);
        Assert.Equal(next.TimeRange.Duration, result[1].TimeRange.Duration);
    }

    [Fact]
    public void Edit_DurationRebuildsFollowingTodosAndAllowsMandatoryOverlap()
    {
        var edited = Todo("编辑", 9, 0, 9, 30);
        var mandatory = Todo("已有强制", 9, 30, 10, 0, mandatory: true);

        var result = ScheduleManagement.Edit(
            [edited, mandatory],
            edited.Id,
            "改名",
            TimeSpan.FromHours(1),
            isMandatory: true);

        Assert.Equal("改名", result[0].Title);
        Assert.True(result[0].IsMandatory);
        Assert.True(result[0].TimeRange.Overlaps(result[1].TimeRange));
    }

    [Fact]
    public void Edit_BreakCannotBeRenamed()
    {
        var breakTodo = Todo(ScheduleManagement.BreakTitle, 9, 0, 9, 20);

        Assert.Throws<InvalidOperationException>(
            () => ScheduleManagement.Edit(
                [breakTodo],
                breakTodo.Id,
                "偷改名字",
                TimeSpan.FromMinutes(20),
                isMandatory: false));
    }

    [Fact]
    public void Edit_BreakCanBecomeMandatoryWithoutChangingReservedTitle()
    {
        var breakTodo = Todo(ScheduleManagement.BreakTitle, 9, 0, 9, 20);

        var result = ScheduleManagement.Edit(
            [breakTodo],
            breakTodo.Id,
            ScheduleManagement.BreakTitle,
            TimeSpan.FromMinutes(20),
            isMandatory: true);

        var edited = Assert.Single(result);
        Assert.Equal(ScheduleManagement.BreakTitle, edited.Title);
        Assert.True(edited.IsMandatory);
    }

    [Fact]
    public void EditStart_MoveExistingAfterEdited_PreservesFirstTodoDuration()
    {
        var existing = Todo("九点任务", 9, 0, 11, 0);
        var edited = Todo("修改任务", 12, 0, 13, 0);
        var after = Todo("后续任务", 13, 0, 13, 30);

        var result = ScheduleManagement.EditStart(
            [existing, edited, after],
            edited.Id,
            At(10, 0),
            StartTimeConflictResolution.MoveExistingAfterEdited);

        Assert.Equal(StartTimeEditRejection.None, result.Rejection);
        Assert.Equal([edited.Id, existing.Id, after.Id], result.ScheduledTodos.Select(todo => todo.Id));
        Assert.Equal(At(10, 0), result.ScheduledTodos[0].TimeRange.Start);
        Assert.Equal(At(11, 0), result.ScheduledTodos[1].TimeRange.Start);
        Assert.Equal(TimeSpan.FromHours(2), result.ScheduledTodos[1].TimeRange.Duration);
        Assert.Equal(At(13, 0), result.ScheduledTodos[2].TimeRange.Start);
    }

    [Fact]
    public void EditStart_TruncateExistingAtNewStart_OnlyShortensFirstTodo()
    {
        var existing = Todo("九点任务", 9, 0, 11, 0);
        var edited = Todo("修改任务", 12, 0, 13, 0);
        var after = Todo("后续任务", 13, 0, 13, 30);

        var result = ScheduleManagement.EditStart(
            [existing, edited, after],
            edited.Id,
            At(10, 0),
            StartTimeConflictResolution.TruncateExistingAtNewStart);

        Assert.Equal(At(10, 0), result.ScheduledTodos[0].TimeRange.End);
        Assert.Equal(edited.Id, result.ScheduledTodos[1].Id);
        Assert.Equal(At(10, 0), result.ScheduledTodos[1].TimeRange.Start);
        Assert.Equal(after.Id, result.ScheduledTodos[2].Id);
        Assert.Equal(At(11, 0), result.ScheduledTodos[2].TimeRange.Start);
        Assert.Equal(TimeSpan.FromMinutes(30), result.ScheduledTodos[2].TimeRange.Duration);
    }

    [Fact]
    public void EditStart_WhenMandatoryTodoOccupiesNewStart_IsRejectedWithoutChanges()
    {
        var mandatory = Todo("不可移动", 9, 0, 11, 0, mandatory: true);
        var edited = Todo("修改任务", 12, 0, 13, 0);

        var result = ScheduleManagement.EditStart(
            [mandatory, edited],
            edited.Id,
            At(10, 0),
            conflictResolution: null);

        Assert.Equal(
            StartTimeEditRejection.MandatoryTodoOccupiesNewStart,
            result.Rejection);
        Assert.Equal([mandatory, edited], result.ScheduledTodos);
        Assert.Equal(mandatory.Id, result.ConflictingTodoId);
    }

    [Fact]
    public void InsertBreak_CreatesNormalFixedTitleTodo()
    {
        var result = ScheduleManagement.InsertBreak(
            [],
            Guid.NewGuid(),
            At(9, 0),
            TimeSpan.FromMinutes(20));

        var breakTodo = Assert.Single(result.ScheduledTodos);
        Assert.Equal("休息", breakTodo.Title);
        Assert.False(breakTodo.IsMandatory);
        Assert.Equal(TimeSpan.FromMinutes(20), breakTodo.TimeRange.Duration);
    }

    private static ScheduledTodo Todo(
        string title,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        bool mandatory = false) =>
        new(
            Guid.NewGuid(),
            title,
            new TimeRange(At(startHour, startMinute), At(endHour, endMinute)),
            mandatory);

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 7, 26, hour, minute, 0, TimeSpan.FromHours(8));
}

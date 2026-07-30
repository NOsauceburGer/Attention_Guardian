using AttentionGuardian.Application;
using AttentionGuardian.Core;
using AttentionGuardian.Desktop.ViewModels;

namespace AttentionGuardian.Desktop.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task RefreshCurrentAsync_SwitchesToAdjacentTaskWhileFocusRemainsStarted()
    {
        var first = Todo("第一项", 9);
        var second = Todo("第二项", 9, 30);
        var scheduled = new ScheduledRepository([first, second]);
        var timeProvider = new MutableTimeProvider(first.TimeRange.Start.AddMinutes(5));
        var future = new FutureRepository();
        var viewModel = new MainViewModel(
            new TodoPlanningService(scheduled, future, timeProvider),
            new ScheduleManagementService(scheduled, future, timeProvider),
            scheduled,
            timeProvider);

        await viewModel.InitializeAsync();
        viewModel.StartFocusCommand.Execute(null);
        timeProvider.Now = second.TimeRange.Start;
        await viewModel.RefreshCurrentAsync();

        Assert.True(viewModel.IsFocusStarted);
        Assert.Equal(second.Id, viewModel.CurrentTodo?.Id);
        Assert.Equal("第二项", viewModel.CurrentTodoTitle);
    }

    [Fact]
    public async Task PlanDueTodo_PrefillsScheduledFormAndSavePlansOnlySelectedTodo()
    {
        var selected = new UnscheduledTodo(
            Guid.NewGuid(),
            "整理发布说明",
            new DateOnly(2026, 7, 26));
        var untouched = new UnscheduledTodo(
            Guid.NewGuid(),
            "联系设计师",
            new DateOnly(2026, 7, 26));
        var scheduled = new ScheduledRepository([]);
        var future = new FutureRepository([selected, untouched]);
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.FromHours(8)));
        var viewModel = new MainViewModel(
            new TodoPlanningService(scheduled, future, timeProvider),
            new ScheduleManagementService(scheduled, future, timeProvider),
            scheduled,
            timeProvider);

        await viewModel.InitializeAsync();
        viewModel.PlanDueTodoCommand.Execute(selected);

        Assert.Equal(DesktopPage.AddScheduled, viewModel.CurrentPage);
        Assert.Equal(selected.Title, viewModel.ScheduledTitle);

        viewModel.DurationMinutes = 30;
        await viewModel.SaveScheduledCommand.ExecuteAsync(null);

        var planned = Assert.Single(scheduled.Saved);
        Assert.Equal(selected.Id, planned.Id);
        Assert.Equal(selected.Title, planned.Title);
        Assert.Equal([selected.Id], future.PlannedIds);
        Assert.Contains(untouched, future.Active);
    }

    [Fact]
    public async Task PlanDueTodo_BackWithoutSaveKeepsFutureTodoActive()
    {
        var selected = new UnscheduledTodo(
            Guid.NewGuid(),
            "仍需规划",
            new DateOnly(2026, 7, 26));
        var scheduled = new ScheduledRepository([]);
        var future = new FutureRepository([selected]);
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.FromHours(8)));
        var viewModel = new MainViewModel(
            new TodoPlanningService(scheduled, future, timeProvider),
            new ScheduleManagementService(scheduled, future, timeProvider),
            scheduled,
            timeProvider);

        viewModel.PlanDueTodoCommand.Execute(selected);
        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.Empty(scheduled.Saved);
        Assert.Empty(future.PlannedIds);
        Assert.Contains(selected, future.Active);
    }

    [Fact]
    public async Task SaveScheduled_CombinesIndependentHourAndMinuteSelections()
    {
        var scheduled = new ScheduledRepository([]);
        var viewModel = CreateViewModel(scheduled);
        viewModel.ScheduledTitle = "独立时间选择";
        viewModel.ScheduledHour = 14;
        viewModel.ScheduledMinute = 35;
        viewModel.DurationMinutes = 30;

        await viewModel.SaveScheduledCommand.ExecuteAsync(null);

        var saved = Assert.Single(scheduled.Saved);
        Assert.Equal(14, saved.TimeRange.Start.Hour);
        Assert.Equal(35, saved.TimeRange.Start.Minute);
        Assert.Equal(new DateOnly(2026, 7, 26), DateOnly.FromDateTime(saved.TimeRange.Start.Date));
    }

    [Fact]
    public async Task SaveFuture_CombinesIndependentYearMonthAndDaySelections()
    {
        var scheduled = new ScheduledRepository([]);
        var future = new FutureRepository();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.FromHours(8)));
        var viewModel = new MainViewModel(
            new TodoPlanningService(scheduled, future, timeProvider),
            new ScheduleManagementService(scheduled, future, timeProvider),
            scheduled,
            timeProvider)
        {
            FutureTitle = "未来安排",
            FutureYear = 2027,
            FutureMonth = 2,
            FutureDay = 18
        };

        await viewModel.SaveFutureCommand.ExecuteAsync(null);

        var saved = Assert.Single(future.Saved);
        Assert.Equal("未来安排", saved.Title);
        Assert.Equal(new DateOnly(2027, 2, 18), saved.ScheduledDate);
    }

    [Fact]
    public async Task SaveFuture_OneDayShortcut_SavesTomorrow()
    {
        var scheduled = new ScheduledRepository([]);
        var future = new FutureRepository();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.FromHours(8)));
        var viewModel = new MainViewModel(
            new TodoPlanningService(scheduled, future, timeProvider),
            new ScheduleManagementService(scheduled, future, timeProvider),
            scheduled,
            timeProvider)
        {
            FutureTitle = "明天处理"
        };

        viewModel.SelectFutureOneDayCommand.Execute(null);
        await viewModel.SaveFutureCommand.ExecuteAsync(null);

        var saved = Assert.Single(future.Saved);
        Assert.Equal(new DateOnly(2026, 7, 27), saved.ScheduledDate);
        Assert.True(viewModel.IsFutureOneDaySelected);
        Assert.False(viewModel.IsFutureTwoDaysSelected);
    }

    [Fact]
    public async Task SaveFuture_TwoDayShortcut_SavesDayAfterTomorrow()
    {
        var scheduled = new ScheduledRepository([]);
        var future = new FutureRepository();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.FromHours(8)));
        var viewModel = new MainViewModel(
            new TodoPlanningService(scheduled, future, timeProvider),
            new ScheduleManagementService(scheduled, future, timeProvider),
            scheduled,
            timeProvider)
        {
            FutureTitle = "后天处理"
        };

        viewModel.SelectFutureTwoDaysCommand.Execute(null);
        await viewModel.SaveFutureCommand.ExecuteAsync(null);

        var saved = Assert.Single(future.Saved);
        Assert.Equal(new DateOnly(2026, 7, 28), saved.ScheduledDate);
        Assert.False(viewModel.IsFutureOneDaySelected);
        Assert.True(viewModel.IsFutureTwoDaysSelected);
    }

    [Fact]
    public async Task SaveFuture_ManualDateAfterShortcut_ClearsRelativeSelection()
    {
        var scheduled = new ScheduledRepository([]);
        var future = new FutureRepository();
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.FromHours(8)));
        var viewModel = new MainViewModel(
            new TodoPlanningService(scheduled, future, timeProvider),
            new ScheduleManagementService(scheduled, future, timeProvider),
            scheduled,
            timeProvider)
        {
            FutureTitle = "手动日期"
        };
        viewModel.SelectFutureTwoDaysCommand.Execute(null);

        viewModel.FutureDay = 30;
        await viewModel.SaveFutureCommand.ExecuteAsync(null);

        var saved = Assert.Single(future.Saved);
        Assert.Equal(new DateOnly(2026, 7, 30), saved.ScheduledDate);
        Assert.Null(viewModel.FutureDaysFromToday);
        Assert.False(viewModel.IsFutureOneDaySelected);
        Assert.False(viewModel.IsFutureTwoDaysSelected);
    }

    [Fact]
    public async Task ReorderScheduled_SavesRequestedPriorityOrder()
    {
        var first = Todo("第一项", 9);
        var second = Todo("第二项", 10);
        var moving = Todo("移动项", 11);
        var scheduled = new ScheduledRepository([first, second, moving]);
        var viewModel = CreateViewModel(scheduled);
        await viewModel.OpenManageCommand.ExecuteAsync(null);

        var succeeded = await viewModel.ReorderScheduledAsync(moving.Id, 0);

        Assert.True(succeeded);
        Assert.Equal(
            [moving.Id, first.Id, second.Id],
            scheduled.Saved.Select(todo => todo.Id));
        Assert.Equal(1, scheduled.ReplaceCount);
    }

    [Fact]
    public async Task Initialize_LoadsMandatoryConflictsIntoVisibleReminder()
    {
        var first = Todo("强制一", 11, mandatory: true);
        var second = Todo("强制二", 11, 15, mandatory: true);
        var scheduled = new ScheduledRepository([first, second]);
        var viewModel = CreateViewModel(
            scheduled,
            new DateTimeOffset(2026, 7, 26, 11, 20, 0, TimeSpan.FromHours(8)));

        await viewModel.InitializeAsync();

        Assert.True(viewModel.HasConflicts);
        var conflict = Assert.Single(viewModel.Conflicts);
        Assert.Contains(
            conflict.ProposedTodo.Id,
            new[] { first.Id, second.Id });
        Assert.Contains(
            conflict.MandatoryTodo.Id,
            new[] { first.Id, second.Id });
    }

    [Fact]
    public async Task OpenManage_HidesCompletedTodosAndMarksThemCompleted()
    {
        var completed = Todo("已完成", 9);
        var current = Todo("进行中", 12);
        var scheduled = new ScheduledRepository([completed, current]);
        var viewModel = CreateViewModel(
            scheduled,
            new DateTimeOffset(2026, 7, 26, 12, 15, 0, TimeSpan.FromHours(8)));

        await viewModel.OpenManageCommand.ExecuteAsync(null);

        var bubble = Assert.Single(viewModel.ScheduledTodos);
        Assert.Equal(current.Id, bubble.Id);
        Assert.Equal([current.Id], scheduled.Saved.Select(todo => todo.Id));
        Assert.Equal([completed.Id], scheduled.Completed.Select(todo => todo.Id));
    }

    [Fact]
    public async Task ReorderScheduled_CanMoveFirstTodoToQueueEnd()
    {
        var moving = Todo("移动项", 9);
        var second = Todo("第二项", 10);
        var third = Todo("第三项", 11);
        var scheduled = new ScheduledRepository([moving, second, third]);
        var viewModel = CreateViewModel(scheduled);
        await viewModel.OpenManageCommand.ExecuteAsync(null);

        var succeeded = await viewModel.ReorderScheduledAsync(
            moving.Id,
            viewModel.ScheduledTodos.Count - 1);

        Assert.True(succeeded);
        Assert.Equal(
            [second.Id, third.Id, moving.Id],
            scheduled.Saved.Select(todo => todo.Id));
    }

    [Fact]
    public async Task MoveScheduledByOffset_WhenMandatoryIsolated_ShowsRuleError()
    {
        var mandatory = Todo("不可移动", 9, mandatory: true);
        var normal = Todo("普通", 11);
        var scheduled = new ScheduledRepository([mandatory, normal]);
        var viewModel = CreateViewModel(scheduled);
        await viewModel.OpenManageCommand.ExecuteAsync(null);
        var bubble = viewModel.ScheduledTodos[0];

        var succeeded = await viewModel.MoveScheduledByOffsetAsync(bubble, 1);

        Assert.False(succeeded);
        Assert.Equal("这个事件没办法放在这里", viewModel.ErrorMessage);
        Assert.Equal(0, scheduled.ReplaceCount);
    }

    [Fact]
    public async Task OpenManage_GroupsTouchingMandatoryTodosIntoOneRow()
    {
        var first = Todo("强制一", 9, mandatory: true);
        var second = Todo("强制二", 9, 30, mandatory: true);
        var normal = Todo("普通", 11);
        var scheduled = new ScheduledRepository([normal, second, first]);
        var viewModel = CreateViewModel(scheduled);

        await viewModel.OpenManageCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasMandatoryGroups);
        Assert.Equal(2, viewModel.ScheduledRows.Count);
        var group = Assert.Single(
            viewModel.ScheduledRows,
            row => row.IsMandatoryGroup);
        Assert.Equal([first.Id, second.Id], group.Items.Select(item => item.Id));
        Assert.Single(
            viewModel.ScheduledRows,
            row => !row.IsMandatoryGroup && row.Items.Single().Id == normal.Id);
    }

    [Fact]
    public async Task OpenManage_MandatoryTodosSeparatedByGapStayInSeparateRows()
    {
        var first = Todo("强制一", 9, mandatory: true);
        var afterGap = Todo("强制二", 10, 30, mandatory: true);
        var scheduled = new ScheduledRepository([first, afterGap]);
        var viewModel = CreateViewModel(scheduled);

        await viewModel.OpenManageCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasMandatoryGroups);
        Assert.Equal(2, viewModel.ScheduledRows.Count);
        Assert.All(
            viewModel.ScheduledRows,
            row =>
            {
                Assert.False(row.IsMandatoryGroup);
                Assert.Single(row.Items);
            });
    }

    [Fact]
    public async Task AddRestAtIndex_CanRepeatAndKeepsFixedNormalIdentity()
    {
        var scheduled = new ScheduledRepository([]);
        var viewModel = CreateViewModel(scheduled);
        await viewModel.OpenManageCommand.ExecuteAsync(null);
        viewModel.RestDurationHours = 0;
        viewModel.RestDurationMinutes = 25;

        Assert.True(await viewModel.AddRestAtIndexAsync(0));
        Assert.True(await viewModel.AddRestAtIndexAsync(1));

        Assert.Equal(2, scheduled.Saved.Count);
        Assert.Equal(2, scheduled.Saved.Select(todo => todo.Id).Distinct().Count());
        Assert.All(
            scheduled.Saved,
            todo =>
            {
                Assert.Equal(ScheduleManagement.BreakTitle, todo.Title);
                Assert.Equal(TimeSpan.FromMinutes(25), todo.TimeRange.Duration);
                Assert.False(todo.IsMandatory);
            });
    }

    [Fact]
    public async Task AddRestAtMandatoryPosition_FallsBackAndShowsMessage()
    {
        var mandatory = Todo("不可移动", 9, mandatory: true);
        var scheduled = new ScheduledRepository([mandatory]);
        var viewModel = CreateViewModel(scheduled);
        await viewModel.OpenManageCommand.ExecuteAsync(null);

        var succeeded = await viewModel.AddRestAtIndexAsync(0);

        Assert.True(succeeded);
        Assert.Equal(mandatory.Id, scheduled.Saved[0].Id);
        Assert.Equal(ScheduleManagement.BreakTitle, scheduled.Saved[1].Title);
        Assert.Equal("这个事件没办法放在这里", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task AddRestAtIndex_RejectsZeroDurationWithoutSaving()
    {
        var scheduled = new ScheduledRepository([]);
        var viewModel = CreateViewModel(scheduled);
        viewModel.RestDurationHours = 0;
        viewModel.RestDurationMinutes = 0;

        var succeeded = await viewModel.AddRestAtIndexAsync(0);

        Assert.False(succeeded);
        Assert.Equal("休息时长必须大于零", viewModel.ErrorMessage);
        Assert.Equal(0, scheduled.ReplaceCount);
    }

    [Fact]
    public async Task BackFromManagement_WithOpenChanges_AsksBeforeSaving()
    {
        var scheduled = new ScheduledRepository([Todo("待修改")]);
        var viewModel = CreateViewModel(scheduled);
        await viewModel.OpenManageCommand.ExecuteAsync(null);
        var bubble = Assert.Single(viewModel.ScheduledTodos);
        await bubble.ToggleExpandedAsync();
        bubble.Title = "修改后";

        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.Equal(DesktopPage.Manage, viewModel.CurrentPage);
        Assert.Equal(ConfirmationKind.LeaveManagement, viewModel.PendingConfirmation);
        Assert.Contains("保存", viewModel.ConfirmationTitle);
        Assert.Equal(0, scheduled.ReplaceCount);
    }

    [Fact]
    public async Task ConfirmLeave_SavesOpenChangesBeforeReturningToFocus()
    {
        var scheduled = new ScheduledRepository([Todo("待修改")]);
        var viewModel = CreateViewModel(scheduled);
        await viewModel.OpenManageCommand.ExecuteAsync(null);
        var bubble = Assert.Single(viewModel.ScheduledTodos);
        await bubble.ToggleExpandedAsync();
        bubble.Title = "修改后";
        await viewModel.BackCommand.ExecuteAsync(null);

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(DesktopPage.Focus, viewModel.CurrentPage);
        Assert.Equal("修改后", Assert.Single(scheduled.Saved).Title);
        Assert.Equal(1, scheduled.ReplaceCount);
    }

    [Fact]
    public async Task BubbleDelete_OpensNamedConfirmationAndDeletesOnlyAfterConfirm()
    {
        var selected = Todo("只删除这一项");
        var other = Todo("保留");
        var scheduled = new ScheduledRepository([selected, other]);
        var viewModel = CreateViewModel(scheduled);
        await viewModel.OpenManageCommand.ExecuteAsync(null);
        var bubble = viewModel.ScheduledTodos.Single(item => item.Id == selected.Id);

        bubble.DeleteCommand.Execute(null);

        Assert.Equal(ConfirmationKind.DeleteScheduled, viewModel.PendingConfirmation);
        Assert.Contains("只删除这一项", viewModel.ConfirmationMessage);
        Assert.Equal(0, scheduled.ReplaceCount);

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal([other.Id], scheduled.Saved.Select(todo => todo.Id));
        Assert.Equal(1, scheduled.ReplaceCount);
    }

    private static MainViewModel CreateViewModel(
        ScheduledRepository scheduled,
        DateTimeOffset? now = null)
    {
        var future = new FutureRepository();
        var timeProvider = new FixedTimeProvider(
            now ?? new DateTimeOffset(
                2026,
                7,
                26,
                8,
                0,
                0,
                TimeSpan.FromHours(8)));
        return new MainViewModel(
            new TodoPlanningService(scheduled, future, timeProvider),
            new ScheduleManagementService(scheduled, future, timeProvider),
            scheduled,
            timeProvider);
    }

    private static ScheduledTodo Todo(
        string title,
        int hour = 9,
        int minute = 0,
        bool mandatory = false)
    {
        var start = new DateTimeOffset(
            2026,
            7,
            26,
            hour,
            minute,
            0,
            TimeSpan.FromHours(8));
        return new ScheduledTodo(
            Guid.NewGuid(),
            title,
            new TimeRange(start, start.AddMinutes(30)),
            mandatory);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone(
                "Test",
                now.Offset,
                "Test",
                "Test");
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone(
                "MutableTest",
                now.Offset,
                "MutableTest",
                "MutableTest");
    }

    private sealed class ScheduledRepository(
        IEnumerable<ScheduledTodo> initial) : IScheduledTodoRepository
    {
        public IReadOnlyList<ScheduledTodo> Saved { get; private set; } = initial.ToArray();

        public int ReplaceCount { get; private set; }
        public IReadOnlyList<ScheduledTodo> Completed { get; private set; } = [];

        public Task<IReadOnlyList<ScheduledTodo>> LoadAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved);

        public Task ReplaceAllAsync(
            IReadOnlyList<ScheduledTodo> scheduledTodos,
            CancellationToken cancellationToken = default)
        {
            Saved = scheduledTodos.ToArray();
            ReplaceCount++;
            return Task.CompletedTask;
        }

        public Task MarkCompletedBeforeAsync(
            DateTimeOffset completedBefore,
            CancellationToken cancellationToken = default)
        {
            Completed = Saved
                .Where(todo => todo.TimeRange.End <= completedBefore)
                .ToArray();
            Saved = Saved
                .Where(todo => todo.TimeRange.End > completedBefore)
                .ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class FutureRepository(
        IEnumerable<UnscheduledTodo>? initial = null) : IUnscheduledTodoRepository
    {
        public List<UnscheduledTodo> Saved { get; } = [];
        public List<UnscheduledTodo> Active { get; } = initial?.ToList() ?? [];
        public List<Guid> PlannedIds { get; } = [];

        public Task<IReadOnlyList<UnscheduledTodo>> LoadAllActiveAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnscheduledTodo>>(Active);

        public Task<IReadOnlyList<UnscheduledTodo>> LoadByDateAsync(
            DateOnly scheduledDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnscheduledTodo>>([]);

        public Task<IReadOnlyList<UnscheduledTodo>> LoadDueOnOrBeforeAsync(
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnscheduledTodo>>(
                Active.Where(todo => todo.ScheduledDate <= date).ToArray());

        public Task<UnscheduledTodo?> LoadActiveByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Active.SingleOrDefault(todo => todo.Id == id));

        public Task SaveAsync(
            UnscheduledTodo todo,
            CancellationToken cancellationToken = default)
        {
            Saved.Add(todo);
            Active.Add(todo);
            return Task.CompletedTask;
        }

        public Task UpdateActiveAsync(
            UnscheduledTodo todo,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkPlannedAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            PlannedIds.Add(id);
            Active.RemoveAll(todo => todo.Id == id);
            return Task.CompletedTask;
        }

        public Task MarkDeletedAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

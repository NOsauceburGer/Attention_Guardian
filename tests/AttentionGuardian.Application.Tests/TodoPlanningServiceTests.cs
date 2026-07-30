using AttentionGuardian.Application;
using AttentionGuardian.Core;

namespace AttentionGuardian.Application.Tests;

public sealed class TodoPlanningServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddScheduledTodoAsync_WithoutStartTime_UsesNowAndCalculatedEnd()
    {
        var scheduledRepository = new ScheduledRepository();
        var service = CreateService(scheduledRepository);
        var request = new AddScheduledTodoRequest(
            Guid.NewGuid(),
            "从现在开始",
            TimeSpan.FromMinutes(45));

        var result = await service.AddScheduledTodoAsync(request);

        var saved = Assert.Single(scheduledRepository.Saved);
        Assert.True(result.IsSuccess);
        Assert.Equal(Now, saved.TimeRange.Start);
        Assert.Equal(Now.AddMinutes(45), saved.TimeRange.End);
    }

    [Fact]
    public async Task AddScheduledTodoAsync_Success_ReplacesWholeCalculatedSchedule()
    {
        var existing = Todo("原任务", Now.AddMinutes(30), Now.AddHours(1));
        var scheduledRepository = new ScheduledRepository([existing]);
        var service = CreateService(scheduledRepository);
        var request = new AddScheduledTodoRequest(
            Guid.NewGuid(),
            "新增任务",
            TimeSpan.FromHours(1),
            Now);

        var result = await service.AddScheduledTodoAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, scheduledRepository.Saved.Count);
        var shifted = Assert.Single(
            scheduledRepository.Saved,
            todo => todo.Id == existing.Id);
        Assert.Equal(Now.AddHours(1), shifted.TimeRange.Start);
    }

    [Fact]
    public async Task AddScheduledTodoAsync_MandatoryConflict_SavesWholePlanWithConflict()
    {
        var mandatory = Todo(
            "已有强制",
            Now.AddMinutes(30),
            Now.AddHours(1),
            mandatory: true);
        var scheduledRepository = new ScheduledRepository([mandatory]);
        var service = CreateService(scheduledRepository);
        var request = new AddScheduledTodoRequest(
            Guid.NewGuid(),
            "新增强制",
            TimeSpan.FromHours(1),
            Now,
            IsMandatory: true);

        var result = await service.AddScheduledTodoAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(result.HasUnresolvedConflicts);
        Assert.Equal(1, scheduledRepository.ReplaceCount);
        Assert.Equal(2, scheduledRepository.Saved.Count);
    }

    [Fact]
    public async Task AddScheduledTodoAsync_MandatoryConflict_NewTodoBecomesCurrent()
    {
        var existing = new ScheduledTodo(
            Guid.NewGuid(),
            "existing mandatory",
            new TimeRange(Now.AddMinutes(-15), Now.AddMinutes(45)),
            isMandatory: true,
            currentSelectionPriority: 6);
        var scheduledRepository = new ScheduledRepository([existing]);
        var service = CreateService(scheduledRepository);
        var newId = Guid.NewGuid();

        await service.AddScheduledTodoAsync(
            new AddScheduledTodoRequest(
                newId,
                "new mandatory",
                TimeSpan.FromMinutes(30),
                Now,
                IsMandatory: true));
        var openingState = await service.LoadOpeningStateAsync();

        Assert.Equal(newId, openingState.CurrentTodo?.Id);
        Assert.Equal(7, openingState.CurrentTodo?.CurrentSelectionPriority);
    }

    [Fact]
    public async Task AddScheduledTodoAsync_RejectsZeroDurationBeforeReadingSchedule()
    {
        var scheduledRepository = new ScheduledRepository();
        var service = CreateService(scheduledRepository);
        var request = new AddScheduledTodoRequest(
            Guid.NewGuid(),
            "零时长",
            TimeSpan.Zero);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AddScheduledTodoAsync(request));

        Assert.Equal(0, scheduledRepository.LoadCount);
        Assert.Equal(0, scheduledRepository.ReplaceCount);
    }

    [Fact]
    public async Task AddUnscheduledTodoAsync_WithExactDate_SavesThatDate()
    {
        var unscheduledRepository = new UnscheduledRepository();
        var service = CreateService(
            new ScheduledRepository(),
            unscheduledRepository);
        var date = new DateOnly(2026, 8, 2);

        var saved = await service.AddUnscheduledTodoAsync(
            new AddUnscheduledTodoRequest(
                Guid.NewGuid(),
                "日历选择",
                ScheduledDate: date));

        Assert.Equal(date, saved.ScheduledDate);
        Assert.Same(saved, Assert.Single(unscheduledRepository.Saved));
    }

    [Fact]
    public async Task AddUnscheduledTodoAsync_WithRelativeDate_ConvertsToExactDate()
    {
        var unscheduledRepository = new UnscheduledRepository();
        var service = CreateService(
            new ScheduledRepository(),
            unscheduledRepository);

        var saved = await service.AddUnscheduledTodoAsync(
            new AddUnscheduledTodoRequest(
                Guid.NewGuid(),
                "两天后",
                DaysFromToday: 2));

        Assert.Equal(new DateOnly(2026, 7, 28), saved.ScheduledDate);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task AddUnscheduledTodoAsync_RequiresExactlyOneDateInput(
        bool includeExactDate,
        bool includeRelativeDate)
    {
        var service = CreateService(new ScheduledRepository());
        var request = new AddUnscheduledTodoRequest(
            Guid.NewGuid(),
            "无效日期输入",
            includeExactDate ? new DateOnly(2026, 7, 27) : null,
            includeRelativeDate ? 1 : null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AddUnscheduledTodoAsync(request));
    }

    [Fact]
    public async Task AddUnscheduledTodoAsync_RejectsTodayAsRelativeDate()
    {
        var service = CreateService(new ScheduledRepository());
        var request = new AddUnscheduledTodoRequest(
            Guid.NewGuid(),
            "无效相对日期",
            DaysFromToday: 0);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.AddUnscheduledTodoAsync(request));
    }

    [Fact]
    public async Task LoadOpeningStateAsync_ReturnsCurrentTodoAndChecksTodayOnce()
    {
        var current = Todo(
            "跨零点当前任务",
            Now.AddDays(-1).AddHours(14),
            Now.AddMinutes(30));
        var scheduledRepository = new ScheduledRepository([current]);
        var overdue = new UnscheduledTodo(
            Guid.NewGuid(),
            "昨天未规划",
            new DateOnly(2026, 7, 25));
        var dueToday = new UnscheduledTodo(
            Guid.NewGuid(),
            "今天未规划",
            new DateOnly(2026, 7, 26));
        var future = new UnscheduledTodo(
            Guid.NewGuid(),
            "明天才到期",
            new DateOnly(2026, 7, 27));
        var unscheduledRepository = new UnscheduledRepository([overdue, dueToday, future]);
        var service = CreateService(scheduledRepository, unscheduledRepository);

        var state = await service.LoadOpeningStateAsync();

        Assert.Same(current, state.CurrentTodo);
        Assert.Equal([overdue, dueToday], state.DueUnscheduledTodos);
        Assert.Equal(1, unscheduledRepository.LoadByDateCount);
        Assert.Equal(new DateOnly(2026, 7, 26), unscheduledRepository.LastLoadedDate);
    }

    [Fact]
    public async Task LoadOpeningStateAsync_RebuildsPersistedMandatoryConflicts()
    {
        var existing = new ScheduledTodo(
            Guid.NewGuid(),
            "existing mandatory",
            new TimeRange(Now.AddMinutes(-30), Now.AddMinutes(30)),
            isMandatory: true,
            currentSelectionPriority: 3);
        var newlyEntered = new ScheduledTodo(
            Guid.NewGuid(),
            "new mandatory",
            new TimeRange(Now.AddMinutes(-15), Now.AddMinutes(45)),
            isMandatory: true,
            currentSelectionPriority: 4);
        var service = CreateService(
            new ScheduledRepository([existing, newlyEntered]));

        var state = await service.LoadOpeningStateAsync();

        var conflict = Assert.Single(state.MandatoryConflicts);
        Assert.Same(newlyEntered, conflict.ProposedTodo);
        Assert.Same(existing, conflict.MandatoryTodo);
    }

    [Fact]
    public async Task LoadOpeningStateAsync_IgnoresCompletedMandatoryConflicts()
    {
        var first = new ScheduledTodo(
            Guid.NewGuid(),
            "completed mandatory one",
            new TimeRange(Now.AddHours(-2), Now.AddHours(-1)),
            isMandatory: true);
        var second = new ScheduledTodo(
            Guid.NewGuid(),
            "completed mandatory two",
            new TimeRange(Now.AddMinutes(-90), Now.AddMinutes(-30)),
            isMandatory: true);
        var service = CreateService(new ScheduledRepository([first, second]));

        var state = await service.LoadOpeningStateAsync();

        Assert.Empty(state.MandatoryConflicts);
    }

    [Fact]
    public async Task PlanUnscheduledTodoAsync_SavesScheduleThenMarksSourcePlanned()
    {
        var source = new UnscheduledTodo(
            Guid.NewGuid(),
            "需要规划",
            new DateOnly(2026, 7, 26));
        var scheduledRepository = new ScheduledRepository();
        var unscheduledRepository = new UnscheduledRepository([source]);
        scheduledRepository.OnReplace = () => unscheduledRepository.Operations.Add("replace");
        var service = CreateService(scheduledRepository, unscheduledRepository);

        var result = await service.PlanUnscheduledTodoAsync(
            new PlanUnscheduledTodoRequest(
                source.Id,
                TimeSpan.FromMinutes(30),
                Now));

        var scheduled = Assert.Single(result.ScheduledTodos);
        Assert.Equal(source.Id, scheduled.Id);
        Assert.Equal(source.Title, scheduled.Title);
        Assert.Equal(["replace", "planned"], unscheduledRepository.Operations);
        Assert.DoesNotContain(source, unscheduledRepository.Saved);
    }

    [Fact]
    public async Task PlanUnscheduledTodoAsync_WhenMarkFails_RetryDoesNotReplaceScheduleAgain()
    {
        var source = new UnscheduledTodo(
            Guid.NewGuid(),
            "可安全重试",
            new DateOnly(2026, 7, 26));
        var scheduledRepository = new ScheduledRepository();
        var unscheduledRepository = new UnscheduledRepository([source])
        {
            FailNextMarkPlanned = true
        };
        scheduledRepository.OnReplace = () => unscheduledRepository.Operations.Add("replace");
        var service = CreateService(scheduledRepository, unscheduledRepository);
        var request = new PlanUnscheduledTodoRequest(
            source.Id,
            TimeSpan.FromMinutes(30),
            Now);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlanUnscheduledTodoAsync(request));
        var retry = await service.PlanUnscheduledTodoAsync(request);

        Assert.True(retry.IsSuccess);
        Assert.Equal(1, scheduledRepository.ReplaceCount);
        Assert.Equal(["replace", "planned-failed", "planned"], unscheduledRepository.Operations);
    }

    [Fact]
    public async Task PlanUnscheduledTodoAsync_WhenScheduleFails_DoesNotMarkSource()
    {
        var source = new UnscheduledTodo(
            Guid.NewGuid(),
            "保持活动",
            new DateOnly(2026, 7, 26));
        var scheduledRepository = new ScheduledRepository
        {
            ReplaceException = new InvalidOperationException("database unavailable")
        };
        var unscheduledRepository = new UnscheduledRepository([source]);
        var service = CreateService(scheduledRepository, unscheduledRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PlanUnscheduledTodoAsync(
                new PlanUnscheduledTodoRequest(source.Id, TimeSpan.FromMinutes(30), Now)));

        Assert.Contains(source, unscheduledRepository.Saved);
        Assert.Empty(unscheduledRepository.Operations);
    }

    [Fact]
    public async Task PlanUnscheduledTodoAsync_MandatoryConflict_SavesAndMarksSourcePlanned()
    {
        var source = new UnscheduledTodo(
            Guid.NewGuid(),
            "强制未来待办",
            new DateOnly(2026, 7, 26),
            isMandatory: true);
        var existing = Todo(
            "已有强制事件",
            Now,
            Now.AddHours(1),
            mandatory: true);
        var scheduledRepository = new ScheduledRepository([existing]);
        var unscheduledRepository = new UnscheduledRepository([source]);
        var service = CreateService(scheduledRepository, unscheduledRepository);

        var result = await service.PlanUnscheduledTodoAsync(
            new PlanUnscheduledTodoRequest(source.Id, TimeSpan.FromMinutes(30), Now));

        Assert.True(result.IsSuccess);
        Assert.True(result.HasUnresolvedConflicts);
        Assert.Equal(1, scheduledRepository.ReplaceCount);
        Assert.DoesNotContain(source, unscheduledRepository.Saved);
    }

    [Fact]
    public async Task PlanUnscheduledTodoAsync_UsesPriorityAfterExistingMaximum()
    {
        var source = new UnscheduledTodo(
            Guid.NewGuid(),
            "future mandatory",
            new DateOnly(2026, 7, 26),
            isMandatory: true);
        var existing = new ScheduledTodo(
            Guid.NewGuid(),
            "existing mandatory",
            new TimeRange(Now, Now.AddHours(1)),
            isMandatory: true,
            currentSelectionPriority: 12);
        var scheduledRepository = new ScheduledRepository([existing]);
        var unscheduledRepository = new UnscheduledRepository([source]);
        var service = CreateService(scheduledRepository, unscheduledRepository);

        var result = await service.PlanUnscheduledTodoAsync(
            new PlanUnscheduledTodoRequest(
                source.Id,
                TimeSpan.FromMinutes(30),
                Now));

        var planned = Assert.Single(
            result.ScheduledTodos,
            todo => todo.Id == source.Id);
        Assert.Equal(13, planned.CurrentSelectionPriority);
    }

    [Fact]
    public async Task DeleteUnscheduledTodoAsync_RequiresConfirmationAndTargetsOneTodo()
    {
        var selected = new UnscheduledTodo(
            Guid.NewGuid(),
            "只删除这一条",
            new DateOnly(2026, 7, 26));
        var other = new UnscheduledTodo(
            Guid.NewGuid(),
            "保留另一条",
            new DateOnly(2026, 7, 26));
        var unscheduledRepository = new UnscheduledRepository([selected, other]);
        var service = CreateService(new ScheduledRepository(), unscheduledRepository);

        await service.DeleteUnscheduledTodoAsync(selected.Id, isConfirmed: false);
        Assert.Equal(2, unscheduledRepository.Saved.Count);
        await service.DeleteUnscheduledTodoAsync(selected.Id, isConfirmed: true);

        Assert.Equal([other], unscheduledRepository.Saved);
    }

    private static TodoPlanningService CreateService(
        ScheduledRepository scheduledRepository,
        UnscheduledRepository? unscheduledRepository = null) =>
        new(
            scheduledRepository,
            unscheduledRepository ?? new UnscheduledRepository(),
            new ManualTimeProvider(Now));

    private static ScheduledTodo Todo(
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        bool mandatory = false) =>
        new(Guid.NewGuid(), title, new TimeRange(start, end), mandatory);

    private sealed class ScheduledRepository(
        IEnumerable<ScheduledTodo>? initial = null) : IScheduledTodoRepository
    {
        public IReadOnlyList<ScheduledTodo> Saved { get; private set; } =
            initial?.ToArray() ?? Array.Empty<ScheduledTodo>();

        public int ReplaceCount { get; private set; }

        public int LoadCount { get; private set; }

        public Action? OnReplace { get; set; }

        public Exception? ReplaceException { get; set; }

        public Task<IReadOnlyList<ScheduledTodo>> LoadAllAsync(
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult(Saved);
        }

        public Task ReplaceAllAsync(
            IReadOnlyList<ScheduledTodo> scheduledTodos,
            CancellationToken cancellationToken = default)
        {
            if (ReplaceException is not null)
            {
                throw ReplaceException;
            }

            Saved = scheduledTodos.ToArray();
            ReplaceCount++;
            OnReplace?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class UnscheduledRepository(
        IEnumerable<UnscheduledTodo>? initial = null) : IUnscheduledTodoRepository
    {
        public List<UnscheduledTodo> Saved { get; } =
            initial?.ToList() ?? [];

        public int LoadByDateCount { get; private set; }

        public DateOnly? LastLoadedDate { get; private set; }

        public List<string> Operations { get; } = [];

        public bool FailNextMarkPlanned { get; set; }

        public Task<IReadOnlyList<UnscheduledTodo>> LoadAllActiveAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnscheduledTodo>>(Saved.ToArray());

        public Task<IReadOnlyList<UnscheduledTodo>> LoadByDateAsync(
            DateOnly scheduledDate,
            CancellationToken cancellationToken = default)
        {
            LoadByDateCount++;
            LastLoadedDate = scheduledDate;
            IReadOnlyList<UnscheduledTodo> matches = Saved
                .Where(todo => todo.ScheduledDate == scheduledDate)
                .ToArray();
            return Task.FromResult(matches);
        }

        public Task<IReadOnlyList<UnscheduledTodo>> LoadDueOnOrBeforeAsync(
            DateOnly date,
            CancellationToken cancellationToken = default)
        {
            LoadByDateCount++;
            LastLoadedDate = date;
            IReadOnlyList<UnscheduledTodo> matches = Saved
                .Where(todo => todo.ScheduledDate <= date)
                .ToArray();
            return Task.FromResult(matches);
        }

        public Task<UnscheduledTodo?> LoadActiveByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved.SingleOrDefault(todo => todo.Id == id));

        public Task SaveAsync(
            UnscheduledTodo todo,
            CancellationToken cancellationToken = default)
        {
            Saved.Add(todo);
            return Task.CompletedTask;
        }

        public Task UpdateActiveAsync(
            UnscheduledTodo todo,
            CancellationToken cancellationToken = default)
        {
            var index = Saved.FindIndex(existing => existing.Id == todo.Id);
            if (index < 0)
            {
                throw new InvalidOperationException();
            }

            Saved[index] = todo;
            return Task.CompletedTask;
        }

        public Task MarkPlannedAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (FailNextMarkPlanned)
            {
                FailNextMarkPlanned = false;
                Operations.Add("planned-failed");
                throw new InvalidOperationException("mark failed");
            }

            Operations.Add("planned");
            Saved.RemoveAll(todo => todo.Id == id);
            return Task.CompletedTask;
        }

        public Task MarkDeletedAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            Saved.RemoveAll(todo => todo.Id == id);
            return Task.CompletedTask;
        }
    }
}

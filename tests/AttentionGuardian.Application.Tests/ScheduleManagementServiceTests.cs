using AttentionGuardian.Application;
using AttentionGuardian.Core;

namespace AttentionGuardian.Application.Tests;

public sealed class ScheduleManagementServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsScheduleAndMandatoryGroups()
    {
        var first = Todo("强制一", 9, 0, 10, 0, mandatory: true);
        var second = Todo("强制二", 10, 0, 11, 0, mandatory: true);
        var service = CreateService([first, second]);

        var state = await service.LoadAsync();

        Assert.Equal([first, second], state.ScheduledTodos);
        Assert.Single(state.MandatoryGroups);
    }

    [Fact]
    public async Task LoadAsync_MarksTodosCompletedAtOrBeforeCurrentTime()
    {
        var completed = Todo("已完成", 9, 0, 10, 0);
        var endingNow = Todo("刚完成", 11, 30, 12, 0);
        var current = Todo("进行中", 11, 45, 12, 30);
        var future = Todo("稍后", 13, 0, 13, 30);
        var repository = new ScheduledRepository(
            [completed, endingNow, current, future]);
        var service = CreateService(
            repository: repository,
            now: At(12, 0));

        var state = await service.LoadAsync();

        Assert.Equal(
            [current.Id, future.Id],
            state.ScheduledTodos.Select(todo => todo.Id));
        Assert.Equal([current, future], repository.Saved);
        Assert.Equal([completed, endingNow], repository.Completed);
        Assert.Equal(0, repository.ReplaceCount);
    }

    [Fact]
    public async Task ReorderAsync_SavesCompleteResultAndReportsFallback()
    {
        var first = Todo("普通一", 9, 0, 9, 30);
        var mandatory = Todo("不可移动", 9, 30, 10, 30, mandatory: true);
        var moving = Todo("普通二", 10, 30, 11, 0);
        var repository = new ScheduledRepository([first, mandatory, moving]);
        var service = CreateService(repository: repository);

        var result = await service.ReorderAsync(moving.Id, 1);

        Assert.True(result.UsedFallbackPosition);
        Assert.Equal(result.ScheduledTodos, repository.Saved);
        Assert.Equal(1, repository.ReplaceCount);
    }

    [Fact]
    public async Task EditAsync_SavesRecalculatedSchedule()
    {
        var edited = Todo("原名", 9, 0, 9, 30);
        var next = Todo("下一项", 9, 30, 10, 0);
        var repository = new ScheduledRepository([edited, next]);
        var service = CreateService(repository: repository);

        var result = await service.EditAsync(
            new EditScheduledTodoRequest(
                edited.Id,
                "新名称",
                TimeSpan.FromHours(1),
                IsMandatory: false));

        Assert.Equal("新名称", result[0].Title);
        Assert.Equal(At(10, 0), result[1].TimeRange.Start);
        Assert.Equal(result, repository.Saved);
    }

    [Fact]
    public async Task DeleteAsync_RequiresConfirmation()
    {
        var selected = Todo("删除", 9, 0, 9, 30);
        var next = Todo("保留", 9, 30, 10, 0);
        var repository = new ScheduledRepository([selected, next]);
        var service = CreateService(repository: repository);

        await service.DeleteAsync(selected.Id, isConfirmed: false);
        Assert.Equal(0, repository.ReplaceCount);
        var result = await service.DeleteAsync(selected.Id, isConfirmed: true);

        Assert.Single(result);
        Assert.Equal(At(9, 0), result[0].TimeRange.Start);
        Assert.Equal(1, repository.ReplaceCount);
    }

    [Fact]
    public async Task AddBreakAsync_SavesNormalBreak()
    {
        var repository = new ScheduledRepository();
        var service = CreateService(repository: repository);

        var result = await service.AddBreakAsync(
            Guid.NewGuid(),
            At(9, 0),
            TimeSpan.FromMinutes(20));

        var saved = Assert.Single(repository.Saved);
        Assert.Equal("休息", saved.Title);
        Assert.False(saved.IsMandatory);
        Assert.Equal(result.ScheduledTodos, repository.Saved);
    }

    [Fact]
    public async Task AddBreakAsync_UsesPriorityAfterExistingMaximum()
    {
        var existing = new ScheduledTodo(
            Guid.NewGuid(),
            "existing",
            new TimeRange(At(9, 0), At(9, 30)),
            currentSelectionPriority: 9);
        var repository = new ScheduledRepository([existing]);
        var service = CreateService(repository: repository);

        var result = await service.AddBreakAsync(
            Guid.NewGuid(),
            At(9, 30),
            TimeSpan.FromMinutes(20));

        var added = Assert.Single(
            result.ScheduledTodos,
            todo => todo.Id != existing.Id);
        Assert.Equal(10, added.CurrentSelectionPriority);
    }

    [Fact]
    public async Task FutureTodoManagement_LoadsUpdatesAndDeletesOnlySelectedItem()
    {
        var selected = new UnscheduledTodo(
            Guid.NewGuid(),
            "旧名称",
            new DateOnly(2026, 7, 27));
        var other = new UnscheduledTodo(
            Guid.NewGuid(),
            "保留",
            new DateOnly(2026, 7, 28));
        var futureRepository = new FutureRepository([other, selected]);
        var service = CreateService(futureRepository: futureRepository);

        var loaded = await service.LoadFutureTodosAsync();
        Assert.Equal([selected.Id, other.Id], loaded.Select(todo => todo.Id));

        var updated = await service.UpdateFutureTodoAsync(
            selected.Id,
            "新名称",
            new DateOnly(2026, 7, 29));
        Assert.Equal("新名称", updated.Title);
        Assert.Contains(updated, futureRepository.Saved);

        await service.DeleteFutureTodoAsync(selected.Id, isConfirmed: false);
        Assert.Equal(2, futureRepository.Saved.Count);
        await service.DeleteFutureTodoAsync(selected.Id, isConfirmed: true);
        Assert.Equal([other], futureRepository.Saved);
    }

    private static ScheduleManagementService CreateService(
        IEnumerable<ScheduledTodo>? schedule = null,
        ScheduledRepository? repository = null,
        FutureRepository? futureRepository = null,
        DateTimeOffset? now = null) =>
        new(
            repository ?? new ScheduledRepository(schedule),
            futureRepository ?? new FutureRepository(),
            new FixedTimeProvider(now ?? At(8, 0)));

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

    private sealed class ScheduledRepository(
        IEnumerable<ScheduledTodo>? initial = null) : IScheduledTodoRepository
    {
        public IReadOnlyList<ScheduledTodo> Saved { get; private set; } =
            initial?.ToArray() ?? [];

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
        public List<UnscheduledTodo> Saved { get; } =
            initial?.OrderBy(todo => todo.ScheduledDate).ToList() ?? [];

        public Task<IReadOnlyList<UnscheduledTodo>> LoadAllActiveAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnscheduledTodo>>(
                Saved.OrderBy(todo => todo.ScheduledDate).ToArray());

        public Task<IReadOnlyList<UnscheduledTodo>> LoadByDateAsync(
            DateOnly scheduledDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnscheduledTodo>>(
                Saved.Where(todo => todo.ScheduledDate == scheduledDate).ToArray());

        public Task<IReadOnlyList<UnscheduledTodo>> LoadDueOnOrBeforeAsync(
            DateOnly date,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UnscheduledTodo>>(
                Saved.Where(todo => todo.ScheduledDate <= date).ToArray());

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

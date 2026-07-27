using AttentionGuardian.Application;
using AttentionGuardian.Core;

namespace AttentionGuardian.Application.Tests;

public sealed class HandoffReminderServiceTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetPendingAsync_ReturnsTitlesAndOnlyReturnsOncePerCurrentTodo()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(30));
        var next = Todo("下一项", current.TimeRange.End, TimeSpan.FromMinutes(20));
        var repository = new ReminderRepository([current, next]);
        var service = new HandoffReminderService(
            repository,
            new ManualTimeProvider(current.TimeRange.End - TimeSpan.FromMinutes(5)));

        var first = await service.GetPendingAsync();
        var second = await service.GetPendingAsync();

        Assert.NotNull(first);
        Assert.Equal(current.Id, first.CurrentTodoId);
        Assert.Equal("当前", first.CurrentTodoTitle);
        Assert.Equal(next.Id, first.NextTodoId);
        Assert.Equal("下一项", first.NextTodoTitle);
        Assert.Null(second);
        Assert.Equal(2, repository.LoadCount);
    }

    [Fact]
    public async Task GetPendingAsync_WhenPolicyIsIneligible_ReturnsNothing()
    {
        var current = Todo("当前", Start, TimeSpan.FromMinutes(30));
        var next = Todo(
            "下一项",
            current.TimeRange.End + TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(20));
        var service = new HandoffReminderService(
            new ReminderRepository([current, next]),
            new ManualTimeProvider(current.TimeRange.End - TimeSpan.FromMinutes(5)));

        Assert.Null(await service.GetPendingAsync());
    }

    private static ScheduledTodo Todo(
        string title,
        DateTimeOffset start,
        TimeSpan duration) =>
        new(Guid.NewGuid(), title, new TimeRange(start, start + duration));

    private sealed class ReminderRepository(
        IReadOnlyList<ScheduledTodo> schedule) : IScheduledTodoRepository
    {
        public int LoadCount { get; private set; }

        public Task<IReadOnlyList<ScheduledTodo>> LoadAllAsync(
            CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult(schedule);
        }

        public Task ReplaceAllAsync(
            IReadOnlyList<ScheduledTodo> scheduledTodos,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

using AttentionGuardian.Application;
using AttentionGuardian.Core;

namespace AttentionGuardian.Application.Tests;

public sealed class FocusSessionCoordinatorTests
{
    private static readonly DateTimeOffset EventStart =
        new(2026, 7, 25, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartAsync_SavesCurrentTaskAndEvent()
    {
        var repository = new RecordingRepository();
        var coordinator = CreateCoordinator(repository, EventStart.AddHours(-2));

        var session = await coordinator.StartAsync(CreateRequest());

        Assert.NotNull(repository.Saved);
        Assert.Equal(session.CurrentTask, repository.Saved.CurrentTask);
        Assert.Same(session.NextEvent, repository.Saved.NextEvent);
    }

    [Fact]
    public async Task RestoreAsync_RecalculatesStatusUsingCurrentTime()
    {
        var repository = new RecordingRepository
        {
            Saved = new SavedFocusSession(
                "恢复的任务",
                new FixedEvent(
                    EventStart,
                    TimeSpan.FromMinutes(20),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromMinutes(10)))
        };
        var coordinator = CreateCoordinator(repository, EventStart.AddHours(-1));

        var restored = await coordinator.RestoreAsync();

        Assert.NotNull(restored);
        Assert.Equal(FocusSessionStatus.Handoff, restored.Status);
    }

    [Fact]
    public async Task ClearAsync_RemovesRepositoryAndMemoryState()
    {
        var repository = new RecordingRepository();
        var coordinator = CreateCoordinator(repository, EventStart.AddHours(-2));
        await coordinator.StartAsync(CreateRequest());

        await coordinator.ClearAsync();

        Assert.Null(repository.Saved);
        Assert.Null(coordinator.Current);
    }

    [Fact]
    public async Task StartAsync_WhenSaveFails_DoesNotLeaveMemoryState()
    {
        var repository = new RecordingRepository { FailOnSave = true };
        var coordinator = CreateCoordinator(repository, EventStart.AddHours(-2));

        await Assert.ThrowsAsync<IOException>(() =>
            coordinator.StartAsync(CreateRequest()));

        Assert.Null(coordinator.Current);
    }

    private static FocusSessionCoordinator CreateCoordinator(
        IFocusSessionRepository repository,
        DateTimeOffset currentTime) =>
        new(
            new FocusSessionService(new ManualTimeProvider(currentTime)),
            repository);

    private static StartFocusSessionRequest CreateRequest() =>
        new(
            "保存当前任务",
            EventStart,
            TimeSpan.FromMinutes(20),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(10));

    private sealed class RecordingRepository : IFocusSessionRepository
    {
        public SavedFocusSession? Saved { get; set; }

        public bool FailOnSave { get; init; }

        public Task SaveAsync(
            SavedFocusSession session,
            CancellationToken cancellationToken = default)
        {
            if (FailOnSave)
            {
                throw new IOException("Simulated save failure.");
            }

            Saved = session;
            return Task.CompletedTask;
        }

        public Task<SavedFocusSession?> LoadAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved);

        public Task ClearAsync(
            CancellationToken cancellationToken = default)
        {
            Saved = null;
            return Task.CompletedTask;
        }
    }
}

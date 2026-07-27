using AttentionGuardian.Application;
using AttentionGuardian.Core;
using AttentionGuardian.Infrastructure;

namespace AttentionGuardian.Infrastructure.Tests;

public sealed class SqliteFocusSessionRepositoryTests : IDisposable
{
    private readonly string testDirectory =
        Path.Combine(Path.GetTempPath(), $"AttentionGuardian.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndLoad_RoundTripsTaskTimeOffsetAndDurations()
    {
        var repository = CreateRepository();
        var startTime = new DateTimeOffset(
            2026, 7, 25, 18, 0, 0, TimeSpan.FromHours(8));
        var saved = new SavedFocusSession(
            "完成持久化验证",
            new FixedEvent(
                startTime,
                TimeSpan.FromMinutes(20),
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(10)));

        await repository.SaveAsync(saved);
        var loaded = await repository.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(saved.CurrentTask, loaded.CurrentTask);
        Assert.Equal(saved.NextEvent.StartTime, loaded.NextEvent.StartTime);
        Assert.Equal(saved.NextEvent.StartTime.Offset, loaded.NextEvent.StartTime.Offset);
        Assert.Equal(saved.NextEvent.PreparationDuration, loaded.NextEvent.PreparationDuration);
        Assert.Equal(saved.NextEvent.TravelDuration, loaded.NextEvent.TravelDuration);
        Assert.Equal(saved.NextEvent.SafetyBuffer, loaded.NextEvent.SafetyBuffer);
    }

    [Fact]
    public async Task SaveTwice_ReplacesTheSingleCurrentSession()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateSession("第一次"));

        await repository.SaveAsync(CreateSession("第二次"));
        var loaded = await repository.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("第二次", loaded.CurrentTask);
    }

    [Fact]
    public async Task Clear_RemovesSavedSession()
    {
        var repository = CreateRepository();
        await repository.SaveAsync(CreateSession("将被清除"));

        await repository.ClearAsync();
        var loaded = await repository.LoadAsync();

        Assert.Null(loaded);
    }

    public void Dispose()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private SqliteFocusSessionRepository CreateRepository() =>
        new(Path.Combine(testDirectory, "attention-guardian.test.db"));

    private static SavedFocusSession CreateSession(string task) =>
        new(
            task,
            new FixedEvent(
                new DateTimeOffset(2026, 7, 25, 18, 0, 0, TimeSpan.FromHours(8)),
                TimeSpan.FromMinutes(20),
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(10)));
}

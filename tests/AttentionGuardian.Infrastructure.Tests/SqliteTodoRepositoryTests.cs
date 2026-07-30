using AttentionGuardian.Core;
using AttentionGuardian.Infrastructure;
using Microsoft.Data.Sqlite;

namespace AttentionGuardian.Infrastructure.Tests;

public sealed class SqliteTodoRepositoryTests : IDisposable
{
    private readonly string testDirectory =
        Path.Combine(Path.GetTempPath(), $"AttentionGuardian.Todos.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task ScheduledRepository_ReplaceAndLoad_RoundTripsOrderedSchedule()
    {
        var repository = CreateScheduledRepository();
        var later = CreateScheduled("稍后任务", 11, isMandatory: true);
        var earlier = CreateScheduled("较早任务", 9);

        await repository.ReplaceAllAsync([later, earlier]);
        var loaded = await repository.LoadAllAsync();

        Assert.Equal([earlier, later], loaded);
    }

    [Fact]
    public async Task ScheduledRepository_RoundTripsCurrentSelectionPriority()
    {
        var repository = CreateScheduledRepository();
        var start = new DateTimeOffset(
            2026,
            7,
            27,
            11,
            0,
            0,
            TimeSpan.FromHours(8));
        var todo = new ScheduledTodo(
            Guid.NewGuid(),
            "priority",
            new TimeRange(start, start.AddHours(1)),
            isMandatory: true,
            currentSelectionPriority: 17);

        await repository.ReplaceAllAsync([todo]);
        var loaded = Assert.Single(await repository.LoadAllAsync());

        Assert.Equal(17, loaded.CurrentSelectionPriority);
        Assert.Equal(todo, loaded);
    }

    [Fact]
    public async Task ScheduledRepository_UpgradesVersionOneAndDefaultsExistingPriority()
    {
        var databasePath = Path.Combine(testDirectory, "scheduled-v1.db");
        Directory.CreateDirectory(testDirectory);
        var id = Guid.NewGuid();
        await using (var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE schema_version (
                    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
                    version INTEGER NOT NULL CHECK (version >= 0));
                INSERT INTO schema_version VALUES (1, 1);
                CREATE TABLE scheduled_todo (
                    id TEXT PRIMARY KEY NOT NULL,
                    title TEXT NOT NULL,
                    start_utc TEXT NOT NULL,
                    start_offset_minutes INTEGER NOT NULL,
                    duration_seconds INTEGER NOT NULL CHECK (duration_seconds > 0),
                    is_mandatory INTEGER NOT NULL CHECK (is_mandatory IN (0, 1)));
                CREATE INDEX scheduled_todo_start_utc_idx
                    ON scheduled_todo (start_utc, id);
                INSERT INTO scheduled_todo VALUES (
                    $id, 'legacy', '2026-07-27T01:00:00.0000000Z', 480, 3600, 1);
                """;
            command.Parameters.AddWithValue("$id", id.ToString("D"));
            await command.ExecuteNonQueryAsync();
        }

        var repository = new SqliteScheduledTodoRepository(databasePath);
        var loaded = Assert.Single(await repository.LoadAllAsync());

        Assert.Equal(id, loaded.Id);
        Assert.Equal(0, loaded.CurrentSelectionPriority);
        Assert.Equal(3L, await ReadSchemaVersionAsync(databasePath));
    }

    [Fact]
    public async Task ScheduledRepository_ReplaceAll_IsAtomicWhenInsertFails()
    {
        var repository = CreateScheduledRepository();
        var original = CreateScheduled("原计划", 8);
        await repository.ReplaceAllAsync([original]);
        var duplicate = CreateScheduled("重复标识", 10);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.ReplaceAllAsync([duplicate, duplicate]));

        Assert.Equal([original], await repository.LoadAllAsync());
    }

    [Fact]
    public async Task ScheduledRepository_RejectsFractionalSecondsWithoutReplacingOriginal()
    {
        var repository = CreateScheduledRepository();
        var original = CreateScheduled("原计划", 8);
        await repository.ReplaceAllAsync([original]);
        var start = new DateTimeOffset(2026, 7, 27, 10, 0, 0, TimeSpan.FromHours(8));
        var fractional = new ScheduledTodo(
            Guid.NewGuid(),
            "无法无损保存",
            new TimeRange(start, start.AddTicks(TimeSpan.TicksPerSecond + 1)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.ReplaceAllAsync([fractional]));

        Assert.Equal([original], await repository.LoadAllAsync());
    }

    [Fact]
    public async Task ScheduledRepository_MarkCompleted_HidesButPreservesRecord()
    {
        var databasePath = Path.Combine(testDirectory, "completed.db");
        var repository = new SqliteScheduledTodoRepository(databasePath);
        var completed = CreateScheduled("已完成", 8);
        var active = CreateScheduled("仍活动", 12);
        await repository.ReplaceAllAsync([completed, active]);

        var completedAt = new DateTimeOffset(
            2026,
            7,
            27,
            10,
            0,
            0,
            TimeSpan.FromHours(8));
        await repository.MarkCompletedBeforeAsync(completedAt);

        Assert.Equal([active], await repository.LoadAllAsync());

        await using var connection = new SqliteConnection(
            $"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT lifecycle_status, completed_utc
            FROM scheduled_todo
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", completed.Id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("completed", reader.GetString(0));
        Assert.False(reader.IsDBNull(1));
    }

    [Fact]
    public async Task UnscheduledRepository_SaveAndLoadByDate_ReturnsOnlyMatchingActiveDate()
    {
        var repository = CreateUnscheduledRepository();
        var date = new DateOnly(2026, 7, 27);
        var matching = new UnscheduledTodo(Guid.NewGuid(), "购买材料", date, true);
        await repository.SaveAsync(matching);
        await repository.SaveAsync(
            new UnscheduledTodo(Guid.NewGuid(), "后一天", date.AddDays(1)));

        var loaded = await repository.LoadByDateAsync(date);

        Assert.Equal([matching], loaded);
    }

    [Fact]
    public async Task UnscheduledRepository_DueQueryIncludesOverdueButNotFutureTodos()
    {
        var repository = CreateUnscheduledRepository();
        var today = new DateOnly(2026, 7, 27);
        var overdue = new UnscheduledTodo(Guid.NewGuid(), "昨天到期", today.AddDays(-1));
        var dueToday = new UnscheduledTodo(Guid.NewGuid(), "今天到期", today);
        await repository.SaveAsync(dueToday);
        await repository.SaveAsync(overdue);
        await repository.SaveAsync(
            new UnscheduledTodo(Guid.NewGuid(), "明天再说", today.AddDays(1)));

        var loaded = await repository.LoadDueOnOrBeforeAsync(today);

        Assert.Equal([overdue, dueToday], loaded);
    }

    [Fact]
    public async Task UnscheduledRepository_LoadAllActiveOrdersByDate()
    {
        var repository = CreateUnscheduledRepository();
        var earlier = new UnscheduledTodo(
            Guid.NewGuid(),
            "较早",
            new DateOnly(2026, 7, 27));
        var later = new UnscheduledTodo(
            Guid.NewGuid(),
            "较晚",
            new DateOnly(2026, 7, 29));
        await repository.SaveAsync(later);
        await repository.SaveAsync(earlier);

        Assert.Equal([earlier, later], await repository.LoadAllActiveAsync());
    }

    [Fact]
    public async Task UnscheduledRepository_UpdateActiveChangesOnlyActiveTodo()
    {
        var repository = CreateUnscheduledRepository();
        var active = new UnscheduledTodo(
            Guid.NewGuid(),
            "旧名称",
            new DateOnly(2026, 7, 27));
        var deleted = new UnscheduledTodo(
            Guid.NewGuid(),
            "已删除",
            new DateOnly(2026, 7, 28));
        await repository.SaveAsync(active);
        await repository.SaveAsync(deleted);
        await repository.MarkDeletedAsync(deleted.Id);
        var updated = new UnscheduledTodo(
            active.Id,
            "新名称",
            new DateOnly(2026, 7, 30),
            isMandatory: true);

        await repository.UpdateActiveAsync(updated);

        Assert.Equal(updated, await repository.LoadActiveByIdAsync(active.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.UpdateActiveAsync(
                new UnscheduledTodo(
                    deleted.Id,
                    "不能复活",
                    new DateOnly(2026, 8, 1))));
    }

    [Fact]
    public async Task UnscheduledRepository_PlannedAndDeletedTodosLeaveActiveQueries()
    {
        var repository = CreateUnscheduledRepository();
        var date = new DateOnly(2026, 7, 27);
        var planned = new UnscheduledTodo(Guid.NewGuid(), "已规划", date);
        var deleted = new UnscheduledTodo(Guid.NewGuid(), "已删除", date);
        var active = new UnscheduledTodo(Guid.NewGuid(), "仍活动", date);
        await repository.SaveAsync(planned);
        await repository.SaveAsync(deleted);
        await repository.SaveAsync(active);

        await repository.MarkPlannedAsync(planned.Id);
        await repository.MarkPlannedAsync(planned.Id);
        await repository.MarkDeletedAsync(deleted.Id);
        await repository.MarkDeletedAsync(deleted.Id);

        Assert.Null(await repository.LoadActiveByIdAsync(planned.Id));
        Assert.Null(await repository.LoadActiveByIdAsync(deleted.Id));
        Assert.Equal([active], await repository.LoadByDateAsync(date));
    }

    [Fact]
    public async Task UnscheduledRepository_RejectsIncompatibleLifecycleTransition()
    {
        var repository = CreateUnscheduledRepository();
        var todo = new UnscheduledTodo(
            Guid.NewGuid(),
            "已经规划",
            new DateOnly(2026, 7, 27));
        await repository.SaveAsync(todo);
        await repository.MarkPlannedAsync(todo.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.MarkDeletedAsync(todo.Id));
    }

    [Fact]
    public async Task Repositories_CreateSeparateVersionedSchemas()
    {
        var scheduledPath = Path.Combine(testDirectory, "scheduled.db");
        var futurePath = Path.Combine(testDirectory, "future.db");
        var scheduled = new SqliteScheduledTodoRepository(scheduledPath);
        var future = new SqliteUnscheduledTodoRepository(futurePath);

        await scheduled.ReplaceAllAsync([CreateScheduled("定时", 9)]);
        await future.SaveAsync(
            new UnscheduledTodo(Guid.NewGuid(), "未来", new DateOnly(2026, 7, 28)));

        Assert.True(File.Exists(scheduledPath));
        Assert.True(File.Exists(futurePath));
        Assert.Equal(3L, await ReadSchemaVersionAsync(scheduledPath));
        Assert.Equal(1L, await ReadSchemaVersionAsync(futurePath));
        Assert.Equal(
            ["scheduled_todo", "schema_version"],
            await ReadTableNamesAsync(scheduledPath));
        Assert.Equal(
            ["schema_version", "unscheduled_todo"],
            await ReadTableNamesAsync(futurePath));
    }

    [Fact]
    public async Task Repository_RejectsSchemaNewerThanSupportedVersion()
    {
        var databasePath = Path.Combine(testDirectory, "future-version.db");
        Directory.CreateDirectory(testDirectory);
        await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE schema_version (
                    singleton INTEGER PRIMARY KEY,
                    version INTEGER NOT NULL);
                INSERT INTO schema_version VALUES (1, 2);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = new SqliteUnscheduledTodoRepository(databasePath);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.LoadByDateAsync(new DateOnly(2026, 7, 27)));
        Assert.Contains("newer than supported", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private SqliteScheduledTodoRepository CreateScheduledRepository() =>
        new(Path.Combine(testDirectory, "scheduled.db"));

    private SqliteUnscheduledTodoRepository CreateUnscheduledRepository() =>
        new(Path.Combine(testDirectory, "future.db"));

    private static ScheduledTodo CreateScheduled(
        string title,
        int hour,
        bool isMandatory = false)
    {
        var start = new DateTimeOffset(2026, 7, 27, hour, 0, 0, TimeSpan.FromHours(8));
        return new ScheduledTodo(
            Guid.NewGuid(),
            title,
            new TimeRange(start, start.AddHours(1)),
            isMandatory);
    }

    private static async Task<long> ReadSchemaVersionAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_version WHERE singleton = 1;";
        return (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException());
    }

    private static async Task<string[]> ReadTableNamesAsync(string databasePath)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return [.. names];
    }
}

using System.Globalization;
using AttentionGuardian.Application;
using AttentionGuardian.Core;

namespace AttentionGuardian.Infrastructure;

public sealed class SqliteScheduledTodoRepository : IScheduledTodoRepository
{
    public const string DefaultDatabaseFileName = "attention-guardian-scheduled.db";

    private const string VersionOneMigrationSql =
        """
        CREATE TABLE scheduled_todo (
            id TEXT PRIMARY KEY NOT NULL,
            title TEXT NOT NULL,
            start_utc TEXT NOT NULL,
            start_offset_minutes INTEGER NOT NULL,
            duration_seconds INTEGER NOT NULL CHECK (duration_seconds > 0),
            is_mandatory INTEGER NOT NULL CHECK (is_mandatory IN (0, 1))
        );
        CREATE INDEX scheduled_todo_start_utc_idx
            ON scheduled_todo (start_utc, id);
        """;

    private const string VersionTwoMigrationSql =
        """
        ALTER TABLE scheduled_todo
        ADD COLUMN current_selection_priority INTEGER NOT NULL DEFAULT 0
            CHECK (current_selection_priority >= 0);
        """;

    private const string VersionThreeMigrationSql =
        """
        ALTER TABLE scheduled_todo
        ADD COLUMN lifecycle_status TEXT NOT NULL DEFAULT 'active'
            CHECK (lifecycle_status IN ('active', 'completed', 'deleted'));
        ALTER TABLE scheduled_todo
        ADD COLUMN completed_utc TEXT NULL;
        CREATE INDEX scheduled_todo_lifecycle_idx
            ON scheduled_todo (lifecycle_status, start_utc, id);
        """;

    private readonly SqliteDatabase database;

    public SqliteScheduledTodoRepository(string databasePath)
    {
        database = new SqliteDatabase(
            databasePath,
            VersionOneMigrationSql,
            VersionTwoMigrationSql,
            VersionThreeMigrationSql);
    }

    public async Task<IReadOnlyList<ScheduledTodo>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, title, start_utc, start_offset_minutes, duration_seconds,
                   is_mandatory, current_selection_priority
            FROM scheduled_todo
            WHERE lifecycle_status = 'active'
            ORDER BY start_utc, id;
            """;

        var todos = new List<ScheduledTodo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var utcStart = DateTimeOffset.ParseExact(
                reader.GetString(2),
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            var start = utcStart.ToOffset(TimeSpan.FromMinutes(reader.GetInt32(3)));
            var end = start + TimeSpan.FromSeconds(reader.GetInt64(4));
            todos.Add(
                new ScheduledTodo(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    new TimeRange(start, end),
                    reader.GetBoolean(5),
                    reader.GetInt64(6)));
        }

        return todos;
    }

    public async Task ReplaceAllAsync(
        IReadOnlyList<ScheduledTodo> scheduledTodos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scheduledTodos);
        if (scheduledTodos.Select(todo => todo.Id).Distinct().Count() != scheduledTodos.Count)
        {
            throw new ArgumentException(
                "Scheduled todo identifiers must be unique.",
                nameof(scheduledTodos));
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE scheduled_todo
            SET lifecycle_status = 'deleted',
                completed_utc = NULL
            WHERE lifecycle_status = 'active';
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText =
            """
            INSERT INTO scheduled_todo (
                id, title, start_utc, start_offset_minutes, duration_seconds,
                is_mandatory, current_selection_priority, lifecycle_status,
                completed_utc)
            VALUES (
                $id, $title, $startUtc, $offsetMinutes, $durationSeconds,
                $isMandatory, $currentSelectionPriority, 'active', NULL)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                start_utc = excluded.start_utc,
                start_offset_minutes = excluded.start_offset_minutes,
                duration_seconds = excluded.duration_seconds,
                is_mandatory = excluded.is_mandatory,
                current_selection_priority = excluded.current_selection_priority,
                lifecycle_status = 'active',
                completed_utc = NULL;
            """;
        var id = command.Parameters.Add("$id", Microsoft.Data.Sqlite.SqliteType.Text);
        var title = command.Parameters.Add("$title", Microsoft.Data.Sqlite.SqliteType.Text);
        var startUtc = command.Parameters.Add("$startUtc", Microsoft.Data.Sqlite.SqliteType.Text);
        var offsetMinutes = command.Parameters.Add("$offsetMinutes", Microsoft.Data.Sqlite.SqliteType.Integer);
        var durationSeconds = command.Parameters.Add("$durationSeconds", Microsoft.Data.Sqlite.SqliteType.Integer);
        var isMandatory = command.Parameters.Add("$isMandatory", Microsoft.Data.Sqlite.SqliteType.Integer);
        var currentSelectionPriority = command.Parameters.Add(
            "$currentSelectionPriority",
            Microsoft.Data.Sqlite.SqliteType.Integer);

        foreach (var todo in scheduledTodos)
        {
            id.Value = todo.Id.ToString("D");
            title.Value = todo.Title;
            startUtc.Value = todo.TimeRange.Start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
            offsetMinutes.Value = checked((int)todo.TimeRange.Start.Offset.TotalMinutes);
            durationSeconds.Value = GetWholeDurationSeconds(todo);
            isMandatory.Value = todo.IsMandatory ? 1 : 0;
            currentSelectionPriority.Value = todo.CurrentSelectionPriority;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkCompletedBeforeAsync(
        DateTimeOffset completedBefore,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE scheduled_todo
            SET lifecycle_status = 'completed',
                completed_utc = strftime(
                    '%Y-%m-%dT%H:%M:%fZ',
                    unixepoch(start_utc) + duration_seconds,
                    'unixepoch')
            WHERE lifecycle_status = 'active'
              AND unixepoch(start_utc) + duration_seconds
                  <= unixepoch($completedUtc);
            """;
        command.Parameters.AddWithValue(
            "$completedUtc",
            completedBefore.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static long GetWholeDurationSeconds(ScheduledTodo todo)
    {
        if (todo.TimeRange.Duration.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentException(
                "Scheduled todo durations must use whole seconds for SQLite persistence.",
                nameof(todo));
        }

        return checked(todo.TimeRange.Duration.Ticks / TimeSpan.TicksPerSecond);
    }
}

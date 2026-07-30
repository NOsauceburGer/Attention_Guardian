using System.Globalization;
using AttentionGuardian.Application;
using AttentionGuardian.Core;
using Microsoft.Data.Sqlite;

namespace AttentionGuardian.Infrastructure;

public sealed class SqliteFocusSessionRepository
    : IFocusSessionRepository
{
    private const string CreateTableSql =
        """
        CREATE TABLE IF NOT EXISTS focus_session (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            current_task TEXT NOT NULL,
            event_start_utc TEXT NOT NULL,
            event_offset_minutes INTEGER NOT NULL,
            preparation_seconds INTEGER NOT NULL CHECK (preparation_seconds >= 0),
            travel_seconds INTEGER NOT NULL CHECK (travel_seconds >= 0),
            safety_buffer_seconds INTEGER NOT NULL CHECK (safety_buffer_seconds >= 0)
        );
        """;

    private readonly string connectionString;

    public SqliteFocusSessionRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    public async Task SaveAsync(
        SavedFocusSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO focus_session (
                id,
                current_task,
                event_start_utc,
                event_offset_minutes,
                preparation_seconds,
                travel_seconds,
                safety_buffer_seconds)
            VALUES (1, $task, $startUtc, $offsetMinutes, $preparation, $travel, $buffer)
            ON CONFLICT(id) DO UPDATE SET
                current_task = excluded.current_task,
                event_start_utc = excluded.event_start_utc,
                event_offset_minutes = excluded.event_offset_minutes,
                preparation_seconds = excluded.preparation_seconds,
                travel_seconds = excluded.travel_seconds,
                safety_buffer_seconds = excluded.safety_buffer_seconds;
            """;

        var fixedEvent = session.NextEvent;
        command.Parameters.AddWithValue("$task", session.CurrentTask);
        command.Parameters.AddWithValue(
            "$startUtc",
            fixedEvent.StartTime.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue(
            "$offsetMinutes",
            checked((int)fixedEvent.StartTime.Offset.TotalMinutes));
        command.Parameters.AddWithValue(
            "$preparation",
            checked((long)fixedEvent.PreparationDuration.TotalSeconds));
        command.Parameters.AddWithValue(
            "$travel",
            checked((long)fixedEvent.TravelDuration.TotalSeconds));
        command.Parameters.AddWithValue(
            "$buffer",
            checked((long)fixedEvent.SafetyBuffer.TotalSeconds));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SavedFocusSession?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                current_task,
                event_start_utc,
                event_offset_minutes,
                preparation_seconds,
                travel_seconds,
                safety_buffer_seconds
            FROM focus_session
            WHERE id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var utcStart = DateTimeOffset.ParseExact(
            reader.GetString(1),
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        var offset = TimeSpan.FromMinutes(reader.GetInt32(2));
        var fixedEvent = new FixedEvent(
            utcStart.ToOffset(offset),
            TimeSpan.FromSeconds(reader.GetInt64(3)),
            TimeSpan.FromSeconds(reader.GetInt64(4)),
            TimeSpan.FromSeconds(reader.GetInt64(5)));

        return new SavedFocusSession(reader.GetString(0), fixedEvent);
    }

    public async Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM focus_session WHERE id = 1;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = CreateTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}

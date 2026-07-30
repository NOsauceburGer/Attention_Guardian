using AttentionGuardian.Application;
using AttentionGuardian.Core;

namespace AttentionGuardian.Infrastructure;

public sealed class SqliteUnscheduledTodoRepository : IUnscheduledTodoRepository
{
    public const string DefaultDatabaseFileName = "attention-guardian-future.db";

    private const string VersionOneMigrationSql =
        """
        CREATE TABLE unscheduled_todo (
            id TEXT PRIMARY KEY NOT NULL,
            title TEXT NOT NULL,
            scheduled_date TEXT NOT NULL,
            is_mandatory INTEGER NOT NULL CHECK (is_mandatory IN (0, 1)),
            lifecycle_status TEXT NOT NULL DEFAULT 'active'
                CHECK (lifecycle_status IN ('active', 'planned', 'deleted'))
        );
        CREATE INDEX unscheduled_todo_active_date_idx
            ON unscheduled_todo (scheduled_date, id)
            WHERE lifecycle_status = 'active';
        """;

    private readonly SqliteDatabase database;

    public SqliteUnscheduledTodoRepository(string databasePath)
    {
        database = new SqliteDatabase(databasePath, VersionOneMigrationSql);
    }

    public async Task<IReadOnlyList<UnscheduledTodo>> LoadAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, title, scheduled_date, is_mandatory
            FROM unscheduled_todo
            WHERE lifecycle_status = 'active'
            ORDER BY scheduled_date, id;
            """;
        return await ReadTodosAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<UnscheduledTodo>> LoadByDateAsync(
        DateOnly scheduledDate,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, title, scheduled_date, is_mandatory
            FROM unscheduled_todo
            WHERE scheduled_date = $scheduledDate
              AND lifecycle_status = 'active'
            ORDER BY id;
            """;
        command.Parameters.AddWithValue(
            "$scheduledDate",
            scheduledDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

        return await ReadTodosAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<UnscheduledTodo>> LoadDueOnOrBeforeAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, title, scheduled_date, is_mandatory
            FROM unscheduled_todo
            WHERE scheduled_date <= $date
              AND lifecycle_status = 'active'
            ORDER BY scheduled_date, id;
            """;
        command.Parameters.AddWithValue(
            "$date",
            date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        return await ReadTodosAsync(command, cancellationToken);
    }

    public async Task<UnscheduledTodo?> LoadActiveByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, title, scheduled_date, is_mandatory
            FROM unscheduled_todo
            WHERE id = $id AND lifecycle_status = 'active';
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return (await ReadTodosAsync(command, cancellationToken)).SingleOrDefault();
    }

    public async Task SaveAsync(
        UnscheduledTodo todo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(todo);

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO unscheduled_todo (
                id, title, scheduled_date, is_mandatory, lifecycle_status)
            VALUES ($id, $title, $scheduledDate, $isMandatory, 'active')
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                scheduled_date = excluded.scheduled_date,
                is_mandatory = excluded.is_mandatory;
            """;
        command.Parameters.AddWithValue("$id", todo.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", todo.Title);
        command.Parameters.AddWithValue(
            "$scheduledDate",
            todo.ScheduledDate.ToString(
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$isMandatory", todo.IsMandatory ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateActiveAsync(
        UnscheduledTodo todo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(todo);

        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE unscheduled_todo
            SET title = $title,
                scheduled_date = $scheduledDate,
                is_mandatory = $isMandatory
            WHERE id = $id AND lifecycle_status = 'active';
            SELECT changes();
            """;
        command.Parameters.AddWithValue("$id", todo.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", todo.Title);
        command.Parameters.AddWithValue(
            "$scheduledDate",
            todo.ScheduledDate.ToString(
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$isMandatory", todo.IsMandatory ? 1 : 0);
        var changed = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        if (changed == 0)
        {
            throw new InvalidOperationException(
                "The selected future todo does not exist or is no longer active.");
        }
    }

    public Task MarkPlannedAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleStatusAsync(
            id,
            targetStatus: "planned",
            allowedExistingStatus: "planned",
            cancellationToken);

    public Task MarkDeletedAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ChangeLifecycleStatusAsync(
            id,
            targetStatus: "deleted",
            allowedExistingStatus: "deleted",
            cancellationToken);

    private async Task ChangeLifecycleStatusAsync(
        Guid id,
        string targetStatus,
        string allowedExistingStatus,
        CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE unscheduled_todo
            SET lifecycle_status = $targetStatus
            WHERE id = $id
              AND lifecycle_status IN ('active', $allowedExistingStatus);
            SELECT changes();
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$targetStatus", targetStatus);
        command.Parameters.AddWithValue("$allowedExistingStatus", allowedExistingStatus);
        var changed = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        if (changed == 0)
        {
            throw new InvalidOperationException(
                "The selected future todo does not exist or has an incompatible lifecycle status.");
        }
    }

    private static async Task<IReadOnlyList<UnscheduledTodo>> ReadTodosAsync(
        Microsoft.Data.Sqlite.SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var todos = new List<UnscheduledTodo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            todos.Add(
                new UnscheduledTodo(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    DateOnly.ParseExact(
                        reader.GetString(2),
                        "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture),
                    reader.GetBoolean(3)));
        }

        return todos;
    }
}

using Microsoft.Data.Sqlite;

namespace AttentionGuardian.Infrastructure;

internal sealed class SqliteDatabase
{
    private readonly string connectionString;
    private readonly IReadOnlyList<string> migrationSql;

    public SqliteDatabase(string databasePath, params string[] migrationSql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(migrationSql);
        if (migrationSql.Length == 0
            || migrationSql.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-empty migration is required.",
                nameof(migrationSql));
        }

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
        this.migrationSql = migrationSql;
    }

    public async Task<SqliteConnection> OpenAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            await MigrateAsync(connection, cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task MigrateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)
            await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_version (
                singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
                version INTEGER NOT NULL CHECK (version >= 0)
            );
            INSERT OR IGNORE INTO schema_version (singleton, version) VALUES (1, 0);
            SELECT version FROM schema_version WHERE singleton = 1;
            """;

        var versionValue = await command.ExecuteScalarAsync(cancellationToken);
        var version = Convert.ToInt32(versionValue, System.Globalization.CultureInfo.InvariantCulture);
        if (version > migrationSql.Count)
        {
            throw new InvalidOperationException(
                $"Database schema version {version} is newer than supported version {migrationSql.Count}.");
        }

        while (version < migrationSql.Count)
        {
            var targetVersion = version + 1;
            command.CommandText =
                $"""
                {migrationSql[version]}
                UPDATE schema_version SET version = {targetVersion} WHERE singleton = 1;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            version = targetVersion;
        }

        await transaction.CommitAsync(cancellationToken);
    }
}

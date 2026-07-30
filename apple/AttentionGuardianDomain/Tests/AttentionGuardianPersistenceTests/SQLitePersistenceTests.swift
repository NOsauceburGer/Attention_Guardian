import Foundation
import Testing
import CSQLite
import AttentionGuardianApplication
import AttentionGuardianDomain
import AttentionGuardianPersistence

@Suite("Apple SQLite persistence")
struct SQLitePersistenceTests {
    @Test("version zero creates separate versioned schemas")
    func createsSeparateSchemas() async throws {
        let directory = try temporaryDirectory()
        let scheduledPath = directory.appending(path: "scheduled.sqlite").path
        let futurePath = directory.appending(path: "future.sqlite").path

        _ = try SQLiteScheduledTodoRepository(path: scheduledPath)
        _ = try SQLiteFutureTodoRepository(path: futurePath)

        #expect(try userVersion(scheduledPath) == 2)
        #expect(try userVersion(futurePath) == 2)
        #expect(try tableNames(scheduledPath) == ["scheduled_todos"])
        #expect(try tableNames(futurePath) == ["future_todos"])
    }

    @Test("container creates the production pair in one explicit directory")
    func containerCreatesDatabasePair() async throws {
        let directory = try temporaryDirectory().appending(
            path: "Application Support/AttentionGuardian")

        let container = try ApplePersistenceContainer.open(in: directory)
        let scheduled = try scheduled(
            id: "00000000-0000-0000-0000-000000000011",
            title: "container",
            start: 1_800_000_000,
            offset: 28_800,
            priority: 0)
        try await container.scheduledTodos.replaceAll([
            ScheduledTodoRecord(todo: scheduled)
        ])

        #expect(container.paths.directory == directory)
        #expect(container.paths.scheduledDatabase.lastPathComponent
            == "scheduled-todos.sqlite3")
        #expect(container.paths.futureDatabase.lastPathComponent
            == "future-todos.sqlite3")
        #expect(FileManager.default.fileExists(
            atPath: container.paths.scheduledDatabase.path))
        #expect(FileManager.default.fileExists(
            atPath: container.paths.futureDatabase.path))
        #expect(try await container.scheduledTodos.loadAll().count == 1)
    }

    @Test("newer schemas are rejected without modification")
    func rejectsNewerSchema() throws {
        let path = try temporaryDirectory().appending(path: "newer.sqlite").path
        try execute(path, "PRAGMA user_version = 99")

        #expect(throws: PersistenceError.unsupportedSchemaVersion(
            found: 99,
            supported: 2)
        ) {
            _ = try SQLiteScheduledTodoRepository(path: path)
        }
        #expect(try userVersion(path) == 99)
    }

    @Test("version one databases migrate one step without losing rows")
    func migratesVersionOne() async throws {
        let directory = try temporaryDirectory()
        let scheduledPath = directory.appending(path: "scheduled-v1.sqlite").path
        let futurePath = directory.appending(path: "future-v1.sqlite").path
        try execute(scheduledPath, """
            CREATE TABLE scheduled_todos (
                id TEXT PRIMARY KEY NOT NULL, title TEXT NOT NULL,
                start_seconds REAL NOT NULL, end_seconds REAL NOT NULL,
                utc_offset_seconds INTEGER NOT NULL, is_mandatory INTEGER NOT NULL,
                selection_priority INTEGER NOT NULL,
                status TEXT NOT NULL CHECK(status IN ('active','completed','deleted'))
            );
            INSERT INTO scheduled_todos VALUES (
                '00000000-0000-0000-0000-000000000301', 'legacy',
                1800000000, 1800003600, 28800, 0, 4, 'active'
            );
            PRAGMA user_version = 1;
            """)
        try execute(futurePath, """
            CREATE TABLE future_todos (
                id TEXT PRIMARY KEY NOT NULL, title TEXT NOT NULL,
                scheduled_date TEXT NOT NULL, is_mandatory INTEGER NOT NULL
            );
            INSERT INTO future_todos VALUES (
                '00000000-0000-0000-0000-000000000302',
                'legacy future', '2026-08-03', 0
            );
            PRAGMA user_version = 1;
            """)

        let scheduled = try SQLiteScheduledTodoRepository(path: scheduledPath)
        let future = try SQLiteFutureTodoRepository(path: futurePath)

        #expect(try userVersion(scheduledPath) == 2)
        #expect(try userVersion(futurePath) == 2)
        #expect(try await scheduled.loadAll().map(\.todo.title) == ["legacy"])
        #expect(try await future.loadAllActive().map(\.todo.title) == ["legacy future"])
        let scheduledBackup = "\(scheduledPath).pre-migration-v1.sqlite3"
        let futureBackup = "\(futurePath).pre-migration-v1.sqlite3"
        #expect(try userVersion(scheduledBackup) == 1)
        #expect(try userVersion(futureBackup) == 1)
        #expect(try !columns(
            scheduledBackup,
            table: "scheduled_todos").contains("completed_at_seconds"))
        #expect(try !columns(
            futureBackup,
            table: "future_todos").contains("status"))
    }

    @Test("failed migration rolls back its version and structure")
    func migrationRollback() throws {
        let path = try temporaryDirectory().appending(path: "broken.sqlite").path
        try execute(path, "CREATE TABLE scheduled_todos (wrong TEXT)")

        #expect(throws: (any Error).self) {
            _ = try SQLiteScheduledTodoRepository(path: path)
        }
        #expect(try userVersion(path) == 0)
        #expect(try columns(path, table: "scheduled_todos") == ["wrong"])
    }

    @Test("failed version-one migration retains a readable pre-migration backup")
    func failedMigrationRetainsBackup() throws {
        let path = try temporaryDirectory().appending(path: "broken-v1.sqlite").path
        try execute(path, """
            CREATE TABLE scheduled_todos (
                id TEXT PRIMARY KEY NOT NULL, title TEXT NOT NULL,
                start_seconds REAL NOT NULL, end_seconds REAL NOT NULL,
                utc_offset_seconds INTEGER NOT NULL, is_mandatory INTEGER NOT NULL,
                selection_priority INTEGER NOT NULL, status TEXT NOT NULL,
                completed_at_seconds REAL NULL
            );
            PRAGMA user_version = 1;
            """)

        #expect(throws: (any Error).self) {
            _ = try SQLiteScheduledTodoRepository(path: path)
        }

        let backup = "\(path).pre-migration-v1.sqlite3"
        #expect(try userVersion(path) == 1)
        #expect(try userVersion(backup) == 1)
        #expect(try tableNames(backup) == ["scheduled_todos"])
    }

    @Test("scheduled records round-trip and replacement preserves history")
    func scheduledRoundTripAndHistory() async throws {
        let path = try temporaryDirectory().appending(path: "scheduled.sqlite").path
        let active = try scheduled(
            id: "00000000-0000-0000-0000-000000000101",
            title: "active",
            start: 1_800_000_000.125,
            offset: 28_800,
            mandatory: true,
            priority: 7)
        let completed = try scheduled(
            id: "00000000-0000-0000-0000-000000000102",
            title: "completed",
            start: 1_700_000_000.5,
            offset: -18_000,
            priority: 3)
        let completedAt = Date(timeIntervalSince1970: 1_700_003_700.75)
        var repository: SQLiteScheduledTodoRepository? =
            try SQLiteScheduledTodoRepository(path: path)
        try await repository?.replaceAll([
            ScheduledTodoRecord(todo: active),
            ScheduledTodoRecord(
                todo: completed,
                status: .completed,
                completedAt: completedAt)
        ])
        try await repository?.replaceAll([
            ScheduledTodoRecord(
                todo: completed,
                status: .completed,
                completedAt: completedAt)
        ])
        repository = nil

        let reopened = try SQLiteScheduledTodoRepository(path: path)
        let loaded = try await reopened.loadAll()
        #expect(loaded.count == 2)
        #expect(loaded.first { $0.todo.id == active.id }?.status == .deleted)
        let history = try #require(loaded.first { $0.todo.id == completed.id })
        #expect(history.status == .completed)
        #expect(history.completedAt == completedAt)
        #expect(history.todo.utcOffsetSeconds == -18_000)
        #expect(history.todo.currentSelectionPriority == 3)
        #expect(history.todo.start == completed.start)
    }

    @Test("future transitions are idempotent and inactive rows stay hidden")
    func futureTransitions() async throws {
        let path = try temporaryDirectory().appending(path: "future.sqlite").path
        var repository: SQLiteFutureTodoRepository? =
            try SQLiteFutureTodoRepository(path: path)
        let planned = try future(
            id: "00000000-0000-0000-0000-000000000201",
            title: "plan me",
            date: "2026-08-01")
        let deleted = try future(
            id: "00000000-0000-0000-0000-000000000202",
            title: "delete me",
            date: "2026-08-02")
        try await repository?.save(planned)
        try await repository?.save(deleted)
        try await repository?.markPlanned(id: planned.todo.id)
        try await repository?.markPlanned(id: planned.todo.id)
        try await repository?.markDeleted(id: deleted.todo.id)
        repository = nil

        let reopened = try SQLiteFutureTodoRepository(path: path)
        #expect(try await reopened.loadAllActive().isEmpty)
        #expect(try await reopened.findActive(id: planned.todo.id) == nil)
        #expect(try scalarText(
            path,
            "SELECT status FROM future_todos WHERE id='\(planned.todo.id.uuidString.lowercased())'"
        ) == "planned")
        #expect(try scalarText(
            path,
            "SELECT status FROM future_todos WHERE id='\(deleted.todo.id.uuidString.lowercased())'"
        ) == "deleted")
    }

    @Test("real repositories support planning recovery without duplicate schedule")
    func planningRecoveryAcrossRepositories() async throws {
        let directory = try temporaryDirectory()
        let scheduled = try SQLiteScheduledTodoRepository(
            path: directory.appending(path: "scheduled.sqlite").path)
        let futureStore = try SQLiteFutureTodoRepository(
            path: directory.appending(path: "future.sqlite").path)
        let failingFuture = FailFirstPlannedRepository(base: futureStore)
        let source = try future(
            id: "00000000-0000-0000-0000-000000000401",
            title: "recoverable",
            date: "2026-08-04")
        try await failingFuture.save(source)
        let useCase = PlanFutureTodoUseCase(
            scheduledRepository: scheduled,
            futureRepository: failingFuture,
            clock: PersistenceFixedClock())
        let start = Date(timeIntervalSince1970: 1_800_100_000)

        await #expect(throws: PlannedFailure.firstAttempt) {
            try await useCase.execute(
                futureTodoId: source.todo.id,
                start: start,
                duration: 1_800,
                utcOffsetSeconds: 28_800)
        }
        let result = try await useCase.execute(
            futureTodoId: source.todo.id,
            start: start,
            duration: 1_800,
            utcOffsetSeconds: 28_800)

        #expect(!result.didWriteSchedule)
        #expect(try await scheduled.loadAll().filter {
            $0.todo.id == source.todo.id
        }.count == 1)
        #expect(try await futureStore.findActive(id: source.todo.id) == nil)
    }
}

private struct PersistenceFixedClock: Clock {
    let now = Date(timeIntervalSince1970: 1_800_000_000)
    let timeZone = TimeZone(secondsFromGMT: 28_800)!
}

private enum PlannedFailure: Error {
    case firstAttempt
}

private actor FailFirstPlannedRepository: FutureTodoRepository {
    private let base: SQLiteFutureTodoRepository
    private var shouldFail = true

    init(base: SQLiteFutureTodoRepository) {
        self.base = base
    }

    func loadAllActive() async throws -> [UnscheduledTodoRecord] {
        try await base.loadAllActive()
    }

    func findActive(id: UUID) async throws -> UnscheduledTodoRecord? {
        try await base.findActive(id: id)
    }

    func save(_ record: UnscheduledTodoRecord) async throws {
        try await base.save(record)
    }

    func markPlanned(id: UUID) async throws {
        if shouldFail {
            shouldFail = false
            throw PlannedFailure.firstAttempt
        }
        try await base.markPlanned(id: id)
    }

    func markDeleted(id: UUID) async throws {
        try await base.markDeleted(id: id)
    }
}

private func scheduled(
    id: String,
    title: String,
    start: TimeInterval,
    offset: Int,
    mandatory: Bool = false,
    priority: Int64
) throws -> ScheduledTodo {
    try ScheduledTodo(
        id: UUID(uuidString: id)!,
        title: title,
        start: Date(timeIntervalSince1970: start),
        end: Date(timeIntervalSince1970: start + 3_600.25),
        utcOffsetSeconds: offset,
        isMandatory: mandatory,
        currentSelectionPriority: priority)
}

private func future(
    id: String,
    title: String,
    date: String
) throws -> UnscheduledTodoRecord {
    UnscheduledTodoRecord(todo: try UnscheduledTodo(
        id: UUID(uuidString: id)!,
        title: title,
        scheduledDate: LocalDate(iso8601: date),
        isMandatory: true))
}

private func temporaryDirectory() throws -> URL {
    let url = FileManager.default.temporaryDirectory
        .appending(path: "AttentionGuardianPersistence-\(UUID().uuidString)")
    try FileManager.default.createDirectory(
        at: url,
        withIntermediateDirectories: true)
    return url
}

private func execute(_ path: String, _ sql: String) throws {
    var database: OpaquePointer?
    guard sqlite3_open(path, &database) == SQLITE_OK, let database else {
        throw TestSQLiteError.open
    }
    defer { sqlite3_close(database) }
    guard sqlite3_exec(database, sql, nil, nil, nil) == SQLITE_OK else {
        throw TestSQLiteError.execute
    }
}

private func userVersion(_ path: String) throws -> Int32 {
    try scalarInt(path, "PRAGMA user_version")
}

private func tableNames(_ path: String) throws -> [String] {
    try stringRows(
        path,
        "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
}

private func columns(_ path: String, table: String) throws -> [String] {
    try stringRows(path, "SELECT name FROM pragma_table_info('\(table)')")
}

private func scalarInt(_ path: String, _ sql: String) throws -> Int32 {
    var database: OpaquePointer?
    guard sqlite3_open(path, &database) == SQLITE_OK, let database else {
        throw TestSQLiteError.open
    }
    defer { sqlite3_close(database) }
    var statement: OpaquePointer?
    guard sqlite3_prepare_v2(database, sql, -1, &statement, nil) == SQLITE_OK else {
        throw TestSQLiteError.execute
    }
    defer { sqlite3_finalize(statement) }
    guard sqlite3_step(statement) == SQLITE_ROW else { throw TestSQLiteError.execute }
    return sqlite3_column_int(statement, 0)
}

private func scalarText(_ path: String, _ sql: String) throws -> String {
    try stringRows(path, sql).first ?? ""
}

private func stringRows(_ path: String, _ sql: String) throws -> [String] {
    var database: OpaquePointer?
    guard sqlite3_open(path, &database) == SQLITE_OK, let database else {
        throw TestSQLiteError.open
    }
    defer { sqlite3_close(database) }
    var statement: OpaquePointer?
    guard sqlite3_prepare_v2(database, sql, -1, &statement, nil) == SQLITE_OK else {
        throw TestSQLiteError.execute
    }
    defer { sqlite3_finalize(statement) }
    var values: [String] = []
    while sqlite3_step(statement) == SQLITE_ROW {
        guard let text = sqlite3_column_text(statement, 0) else {
            throw TestSQLiteError.execute
        }
        values.append(String(cString: text))
    }
    return values
}

private enum TestSQLiteError: Error {
    case open
    case execute
}

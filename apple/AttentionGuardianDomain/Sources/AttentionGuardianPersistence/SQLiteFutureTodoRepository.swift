import Foundation
import AttentionGuardianApplication
import AttentionGuardianDomain

public actor SQLiteFutureTodoRepository: FutureTodoRepository {
    private let store: SQLiteStore

    public init(path: String) throws {
        store = try SQLiteStore(path: path)
        try MigrationBackup.createIfNeeded(
            store: store,
            databasePath: path,
            currentVersion: FutureSchema.currentVersion)
        try FutureSchema.migrate(store)
    }

    public func loadAllActive() async throws -> [UnscheduledTodoRecord] {
        try load(whereClause: "status = 'active'")
    }

    public func findActive(id: UUID) async throws -> UnscheduledTodoRecord? {
        try load(
            whereClause: "status = 'active' AND id = ?",
            id: id).first
    }

    public func save(_ record: UnscheduledTodoRecord) async throws {
        let statement = try store.prepare("""
            INSERT INTO future_todos
                (id, title, scheduled_date, is_mandatory, status)
            VALUES (?, ?, ?, ?, ?)
            ON CONFLICT(id) DO UPDATE SET
                title=excluded.title,
                scheduled_date=excluded.scheduled_date,
                is_mandatory=excluded.is_mandatory,
                status=excluded.status
            """)
        try statement.bind(record.todo.id.uuidString.lowercased(), at: 1)
        try statement.bind(record.todo.title, at: 2)
        try statement.bind(record.todo.scheduledDate.description, at: 3)
        try statement.bind(record.todo.isMandatory ? Int32(1) : Int32(0), at: 4)
        try statement.bind(record.status.rawValue, at: 5)
        _ = try statement.step()
    }

    public func markPlanned(id: UUID) async throws {
        try transition(id: id, to: .planned)
    }

    public func markDeleted(id: UUID) async throws {
        try transition(id: id, to: .deleted)
    }

    private func transition(id: UUID, to status: UnscheduledTodoStatus) throws {
        let statement = try store.prepare("""
            UPDATE future_todos SET status = ?
            WHERE id = ? AND status = 'active'
            """)
        try statement.bind(status.rawValue, at: 1)
        try statement.bind(id.uuidString.lowercased(), at: 2)
        _ = try statement.step()
    }

    private func load(
        whereClause: String,
        id: UUID? = nil
    ) throws -> [UnscheduledTodoRecord] {
        let statement = try store.prepare("""
            SELECT id, title, scheduled_date, is_mandatory, status
            FROM future_todos
            WHERE \(whereClause)
            ORDER BY scheduled_date, id
            """)
        if let id {
            try statement.bind(id.uuidString.lowercased(), at: 1)
        }
        var records: [UnscheduledTodoRecord] = []
        while try statement.step() {
            guard let id = UUID(uuidString: try statement.text(at: 0, column: "id")),
                  let status = UnscheduledTodoStatus(
                    rawValue: try statement.text(at: 4, column: "status"))
            else {
                throw PersistenceError.invalidStoredValue(column: "id/status")
            }
            let todo = try UnscheduledTodo(
                id: id,
                title: statement.text(at: 1, column: "title"),
                scheduledDate: LocalDate(
                    iso8601: statement.text(at: 2, column: "scheduled_date")),
                isMandatory: statement.int32(at: 3) != 0)
            records.append(UnscheduledTodoRecord(todo: todo, status: status))
        }
        return records
    }
}

import Foundation
import AttentionGuardianApplication
import AttentionGuardianDomain

public actor SQLiteScheduledTodoRepository: ScheduledTodoRepository {
    private let store: SQLiteStore

    public init(path: String) throws {
        store = try SQLiteStore(path: path)
        try MigrationBackup.createIfNeeded(
            store: store,
            databasePath: path,
            currentVersion: ScheduledSchema.currentVersion)
        try ScheduledSchema.migrate(store)
    }

    public func loadAll() async throws -> [ScheduledTodoRecord] {
        let statement = try store.prepare("""
            SELECT id, title, start_seconds, end_seconds, utc_offset_seconds,
                   is_mandatory, selection_priority, status, completed_at_seconds
            FROM scheduled_todos
            ORDER BY start_seconds, end_seconds, id
            """)
        var records: [ScheduledTodoRecord] = []
        while try statement.step() {
            guard let id = UUID(uuidString: try statement.text(at: 0, column: "id")),
                  let status = ScheduledTodoStatus(
                    rawValue: try statement.text(at: 7, column: "status"))
            else {
                throw PersistenceError.invalidStoredValue(column: "id/status")
            }
            let todo = try ScheduledTodo(
                id: id,
                title: statement.text(at: 1, column: "title"),
                start: Date(timeIntervalSince1970: statement.double(at: 2)),
                end: Date(timeIntervalSince1970: statement.double(at: 3)),
                utcOffsetSeconds: Int(statement.int32(at: 4)),
                isMandatory: statement.int32(at: 5) != 0,
                currentSelectionPriority: statement.int64(at: 6))
            let completedAt = statement.isNull(at: 8)
                ? nil
                : Date(timeIntervalSince1970: statement.double(at: 8))
            records.append(ScheduledTodoRecord(
                todo: todo,
                status: status,
                completedAt: completedAt))
        }
        return records
    }

    public func replaceAll(_ records: [ScheduledTodoRecord]) async throws {
        try store.transaction {
            let incomingIDs = Set(records.map { $0.todo.id.uuidString.lowercased() })
            let active = try store.prepare(
                "SELECT id FROM scheduled_todos WHERE status = 'active'")
            var missingActiveIDs: [String] = []
            while try active.step() {
                let id = try active.text(at: 0, column: "id")
                if !incomingIDs.contains(id.lowercased()) { missingActiveIDs.append(id) }
            }

            let markDeleted = try store.prepare("""
                UPDATE scheduled_todos
                SET status = 'deleted', completed_at_seconds = NULL
                WHERE id = ? AND status = 'active'
                """)
            for id in missingActiveIDs {
                try markDeleted.bind(id, at: 1)
                _ = try markDeleted.step()
                try markDeleted.reset()
            }

            let upsert = try store.prepare("""
                INSERT INTO scheduled_todos (
                    id, title, start_seconds, end_seconds, utc_offset_seconds,
                    is_mandatory, selection_priority, status, completed_at_seconds
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(id) DO UPDATE SET
                    title=excluded.title,
                    start_seconds=excluded.start_seconds,
                    end_seconds=excluded.end_seconds,
                    utc_offset_seconds=excluded.utc_offset_seconds,
                    is_mandatory=excluded.is_mandatory,
                    selection_priority=excluded.selection_priority,
                    status=excluded.status,
                    completed_at_seconds=excluded.completed_at_seconds
                """)
            for record in records {
                try bind(record, to: upsert)
                _ = try upsert.step()
                try upsert.reset()
            }
        }
    }

    private func bind(
        _ record: ScheduledTodoRecord,
        to statement: SQLiteStatement
    ) throws {
        try statement.bind(record.todo.id.uuidString.lowercased(), at: 1)
        try statement.bind(record.todo.title, at: 2)
        try statement.bind(record.todo.start.timeIntervalSince1970, at: 3)
        try statement.bind(record.todo.end.timeIntervalSince1970, at: 4)
        try statement.bind(Int32(record.todo.utcOffsetSeconds), at: 5)
        try statement.bind(record.todo.isMandatory ? Int32(1) : Int32(0), at: 6)
        try statement.bind(record.todo.currentSelectionPriority, at: 7)
        try statement.bind(record.status.rawValue, at: 8)
        if let completedAt = record.completedAt {
            try statement.bind(completedAt.timeIntervalSince1970, at: 9)
        } else {
            try statement.bindNull(at: 9)
        }
    }
}

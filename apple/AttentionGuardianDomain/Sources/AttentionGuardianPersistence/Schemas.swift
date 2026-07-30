enum ScheduledSchema {
    static let currentVersion: Int32 = 2

    static func migrate(_ store: SQLiteStore) throws {
        let version = try store.userVersion()
        guard version <= currentVersion else {
            throw PersistenceError.unsupportedSchemaVersion(
                found: version,
                supported: currentVersion)
        }
        if version < 1 {
            try store.transaction {
                try store.execute("""
                    CREATE TABLE scheduled_todos (
                        id TEXT PRIMARY KEY NOT NULL,
                        title TEXT NOT NULL,
                        start_seconds REAL NOT NULL,
                        end_seconds REAL NOT NULL,
                        utc_offset_seconds INTEGER NOT NULL,
                        is_mandatory INTEGER NOT NULL,
                        selection_priority INTEGER NOT NULL,
                        status TEXT NOT NULL CHECK(status IN ('active','completed','deleted'))
                    );
                    PRAGMA user_version = 1;
                    """)
            }
        }
        if version < 2 {
            try store.transaction {
                try store.execute("""
                    ALTER TABLE scheduled_todos
                    ADD COLUMN completed_at_seconds REAL NULL;
                    PRAGMA user_version = 2;
                    """)
            }
        }
    }
}

enum FutureSchema {
    static let currentVersion: Int32 = 2

    static func migrate(_ store: SQLiteStore) throws {
        let version = try store.userVersion()
        guard version <= currentVersion else {
            throw PersistenceError.unsupportedSchemaVersion(
                found: version,
                supported: currentVersion)
        }
        if version < 1 {
            try store.transaction {
                try store.execute("""
                    CREATE TABLE future_todos (
                        id TEXT PRIMARY KEY NOT NULL,
                        title TEXT NOT NULL,
                        scheduled_date TEXT NOT NULL,
                        is_mandatory INTEGER NOT NULL
                    );
                    PRAGMA user_version = 1;
                    """)
            }
        }
        if version < 2 {
            try store.transaction {
                try store.execute("""
                    ALTER TABLE future_todos
                    ADD COLUMN status TEXT NOT NULL DEFAULT 'active'
                    CHECK(status IN ('active','planned','deleted'));
                    PRAGMA user_version = 2;
                    """)
            }
        }
    }
}

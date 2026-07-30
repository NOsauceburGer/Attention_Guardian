import Foundation
import CSQLite

public enum PersistenceError: Error, Equatable {
    case openFailed(String)
    case sqlite(code: Int32, message: String)
    case unsupportedSchemaVersion(found: Int32, supported: Int32)
    case invalidStoredValue(column: String)
}

final class SQLiteStore {
    private var handle: OpaquePointer?

    init(path: String) throws {
        var database: OpaquePointer?
        let code = sqlite3_open_v2(
            path,
            &database,
            SQLITE_OPEN_CREATE | SQLITE_OPEN_READWRITE | SQLITE_OPEN_FULLMUTEX,
            nil)
        guard code == SQLITE_OK, let database else {
            let message = database.map { String(cString: sqlite3_errmsg($0)) }
                ?? "SQLite did not return a database handle."
            if let database { sqlite3_close(database) }
            throw PersistenceError.openFailed(message)
        }
        handle = database
        try execute("PRAGMA foreign_keys = ON")
    }

    deinit {
        if let handle { sqlite3_close(handle) }
    }

    func execute(_ sql: String) throws {
        guard let handle else { throw PersistenceError.openFailed("Database is closed.") }
        var errorMessage: UnsafeMutablePointer<CChar>?
        let code = sqlite3_exec(handle, sql, nil, nil, &errorMessage)
        guard code == SQLITE_OK else {
            let message = errorMessage.map { String(cString: $0) }
                ?? String(cString: sqlite3_errmsg(handle))
            sqlite3_free(errorMessage)
            throw PersistenceError.sqlite(code: code, message: message)
        }
    }

    func transaction<T>(_ body: () throws -> T) throws -> T {
        try execute("BEGIN IMMEDIATE")
        do {
            let value = try body()
            try execute("COMMIT")
            return value
        } catch {
            try? execute("ROLLBACK")
            throw error
        }
    }

    func prepare(_ sql: String) throws -> SQLiteStatement {
        guard let handle else { throw PersistenceError.openFailed("Database is closed.") }
        return try SQLiteStatement(database: handle, sql: sql)
    }

    func userVersion() throws -> Int32 {
        let statement = try prepare("PRAGMA user_version")
        guard try statement.step() else {
            throw PersistenceError.invalidStoredValue(column: "user_version")
        }
        return statement.int32(at: 0)
    }

    func backup(to path: String) throws {
        guard let handle else { throw PersistenceError.openFailed("Database is closed.") }
        var destination: OpaquePointer?
        let openCode = sqlite3_open_v2(
            path,
            &destination,
            SQLITE_OPEN_CREATE | SQLITE_OPEN_READWRITE | SQLITE_OPEN_FULLMUTEX,
            nil)
        guard openCode == SQLITE_OK, let destination else {
            let message = destination.map { String(cString: sqlite3_errmsg($0)) }
                ?? "SQLite did not return a backup database handle."
            if let destination { sqlite3_close(destination) }
            throw PersistenceError.openFailed(message)
        }
        defer { sqlite3_close(destination) }

        guard let backup = sqlite3_backup_init(destination, "main", handle, "main") else {
            throw PersistenceError.sqlite(
                code: sqlite3_errcode(destination),
                message: String(cString: sqlite3_errmsg(destination)))
        }
        let stepCode = sqlite3_backup_step(backup, -1)
        let finishCode = sqlite3_backup_finish(backup)
        guard stepCode == SQLITE_DONE, finishCode == SQLITE_OK else {
            let code = finishCode == SQLITE_OK ? stepCode : finishCode
            throw PersistenceError.sqlite(
                code: code,
                message: String(cString: sqlite3_errmsg(destination)))
        }
    }
}

final class SQLiteStatement {
    private let database: OpaquePointer
    private var statement: OpaquePointer?

    init(database: OpaquePointer, sql: String) throws {
        self.database = database
        let code = sqlite3_prepare_v2(database, sql, -1, &statement, nil)
        guard code == SQLITE_OK else {
            throw PersistenceError.sqlite(
                code: code,
                message: String(cString: sqlite3_errmsg(database)))
        }
    }

    deinit {
        sqlite3_finalize(statement)
    }

    func bind(_ value: String, at index: Int32) throws {
        let code = sqlite3_bind_text(statement, index, value, -1, SQLITE_TRANSIENT)
        try check(code)
    }

    func bind(_ value: Int64, at index: Int32) throws {
        try check(sqlite3_bind_int64(statement, index, value))
    }

    func bind(_ value: Int32, at index: Int32) throws {
        try check(sqlite3_bind_int(statement, index, value))
    }

    func bind(_ value: Double, at index: Int32) throws {
        try check(sqlite3_bind_double(statement, index, value))
    }

    func bindNull(at index: Int32) throws {
        try check(sqlite3_bind_null(statement, index))
    }

    func step() throws -> Bool {
        let code = sqlite3_step(statement)
        if code == SQLITE_ROW { return true }
        if code == SQLITE_DONE { return false }
        throw PersistenceError.sqlite(
            code: code,
            message: String(cString: sqlite3_errmsg(database)))
    }

    func reset() throws {
        try check(sqlite3_reset(statement))
        try check(sqlite3_clear_bindings(statement))
    }

    func text(at index: Int32, column: String) throws -> String {
        guard let pointer = sqlite3_column_text(statement, index) else {
            throw PersistenceError.invalidStoredValue(column: column)
        }
        return String(cString: pointer)
    }

    func int64(at index: Int32) -> Int64 {
        sqlite3_column_int64(statement, index)
    }

    func int32(at index: Int32) -> Int32 {
        sqlite3_column_int(statement, index)
    }

    func double(at index: Int32) -> Double {
        sqlite3_column_double(statement, index)
    }

    func isNull(at index: Int32) -> Bool {
        sqlite3_column_type(statement, index) == SQLITE_NULL
    }

    private func check(_ code: Int32) throws {
        guard code == SQLITE_OK else {
            throw PersistenceError.sqlite(
                code: code,
                message: String(cString: sqlite3_errmsg(database)))
        }
    }
}

private let SQLITE_TRANSIENT = unsafeBitCast(
    -1,
    to: sqlite3_destructor_type.self)

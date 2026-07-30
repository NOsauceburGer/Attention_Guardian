import Foundation

enum MigrationBackup {
    static func createIfNeeded(
        store: SQLiteStore,
        databasePath: String,
        currentVersion: Int32
    ) throws {
        let storedVersion = try store.userVersion()
        guard storedVersion > 0, storedVersion < currentVersion else { return }
        let backupPath = path(
            for: databasePath,
            version: storedVersion)
        if FileManager.default.fileExists(atPath: backupPath) {
            try FileManager.default.removeItem(atPath: backupPath)
        }
        do {
            try store.backup(to: backupPath)
        } catch {
            try? FileManager.default.removeItem(atPath: backupPath)
            throw error
        }
    }

    static func path(for databasePath: String, version: Int32) -> String {
        "\(databasePath).pre-migration-v\(version).sqlite3"
    }
}

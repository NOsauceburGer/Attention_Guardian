import Foundation

public struct ApplePersistencePaths: Equatable, Sendable {
    public let directory: URL
    public let scheduledDatabase: URL
    public let futureDatabase: URL

    public init(directory: URL) {
        self.directory = directory
        scheduledDatabase = directory.appendingPathComponent(
            "scheduled-todos.sqlite3",
            isDirectory: false)
        futureDatabase = directory.appendingPathComponent(
            "future-todos.sqlite3",
            isDirectory: false)
    }
}

public struct ApplePersistenceContainer: Sendable {
    public let paths: ApplePersistencePaths
    public let scheduledTodos: SQLiteScheduledTodoRepository
    public let futureTodos: SQLiteFutureTodoRepository

    public static func open(
        in directory: URL,
        fileManager: FileManager = .default
    ) throws -> ApplePersistenceContainer {
        try fileManager.createDirectory(
            at: directory,
            withIntermediateDirectories: true)
        let paths = ApplePersistencePaths(directory: directory)
        return try ApplePersistenceContainer(
            paths: paths,
            scheduledTodos: SQLiteScheduledTodoRepository(
                path: paths.scheduledDatabase.path),
            futureTodos: SQLiteFutureTodoRepository(
                path: paths.futureDatabase.path))
    }

    public static func openInApplicationSupport(
        fileManager: FileManager = .default
    ) throws -> ApplePersistenceContainer {
        let applicationSupport = try fileManager.url(
            for: .applicationSupportDirectory,
            in: .userDomainMask,
            appropriateFor: nil,
            create: true)
        return try open(
            in: applicationSupport.appendingPathComponent(
                "AttentionGuardian",
                isDirectory: true),
            fileManager: fileManager)
    }
}

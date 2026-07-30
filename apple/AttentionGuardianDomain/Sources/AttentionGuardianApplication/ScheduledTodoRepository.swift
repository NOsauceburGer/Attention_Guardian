import AttentionGuardianDomain

public protocol ScheduledTodoRepository: Sendable {
    func loadAll() async throws -> [ScheduledTodoRecord]
    func replaceAll(_ records: [ScheduledTodoRecord]) async throws
}

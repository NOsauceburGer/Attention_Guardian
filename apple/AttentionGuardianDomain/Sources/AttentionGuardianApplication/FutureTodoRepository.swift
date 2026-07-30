import Foundation
import AttentionGuardianDomain

public protocol FutureTodoRepository: Sendable {
    func loadAllActive() async throws -> [UnscheduledTodoRecord]
    func findActive(id: UUID) async throws -> UnscheduledTodoRecord?
    func save(_ record: UnscheduledTodoRecord) async throws
    func markPlanned(id: UUID) async throws
    func markDeleted(id: UUID) async throws
}

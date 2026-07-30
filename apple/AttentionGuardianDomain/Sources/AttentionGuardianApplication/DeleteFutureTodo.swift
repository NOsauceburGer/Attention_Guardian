import Foundation
import AttentionGuardianDomain

public struct DeleteFutureTodoUseCase: Sendable {
    private let repository: any FutureTodoRepository

    public init(repository: any FutureTodoRepository) {
        self.repository = repository
    }

    public func execute(
        futureTodoId: UUID,
        confirmed: Bool
    ) async throws -> UnscheduledTodoRecord? {
        guard let active = try await repository.findActive(id: futureTodoId)
        else {
            return nil
        }
        guard confirmed else {
            return active
        }
        try await repository.markDeleted(id: futureTodoId)
        return TodoLifecycle.delete(active, confirmed: true)
    }
}

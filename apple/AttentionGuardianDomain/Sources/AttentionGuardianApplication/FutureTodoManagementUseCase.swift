import Foundation
import AttentionGuardianDomain

public struct FutureTodoManagementUseCase: Sendable {
    private let repository: any FutureTodoRepository

    public init(repository: any FutureTodoRepository) {
        self.repository = repository
    }

    public func load() async throws -> [UnscheduledTodoRecord] {
        try await repository.loadAllActive().sorted(by: stableOrder)
    }

    public func delete(
        todoId: UUID,
        confirmed: Bool
    ) async throws -> [UnscheduledTodoRecord] {
        if confirmed {
            try await repository.markDeleted(id: todoId)
        }
        return try await load()
    }

    private func stableOrder(
        _ left: UnscheduledTodoRecord,
        _ right: UnscheduledTodoRecord
    ) -> Bool {
        if left.todo.scheduledDate != right.todo.scheduledDate {
            return left.todo.scheduledDate < right.todo.scheduledDate
        }
        return left.todo.id.uuidString.lowercased()
            < right.todo.id.uuidString.lowercased()
    }
}

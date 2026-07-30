import Foundation
import AttentionGuardianDomain

public enum FutureTodoDateSelection: Equatable, Sendable {
    case exact(LocalDate)
    case daysFromToday(Int)
}

public struct AddFutureTodoRequest: Sendable {
    public let id: UUID
    public let title: String
    public let dateSelection: FutureTodoDateSelection
    public let isMandatory: Bool

    public init(
        id: UUID,
        title: String,
        dateSelection: FutureTodoDateSelection,
        isMandatory: Bool
    ) {
        self.id = id
        self.title = title
        self.dateSelection = dateSelection
        self.isMandatory = isMandatory
    }
}

public struct AddFutureTodoUseCase: Sendable {
    private let repository: any FutureTodoRepository
    private let clock: any Clock

    public init(
        repository: any FutureTodoRepository,
        clock: any Clock
    ) {
        self.repository = repository
        self.clock = clock
    }

    public func execute(
        _ request: AddFutureTodoRequest
    ) async throws -> UnscheduledTodoRecord {
        let date: LocalDate
        switch request.dateSelection {
        case .exact(let exactDate):
            date = exactDate
        case .daysFromToday(let days):
            date = try TodoLifecycle.relativeDate(
                from: clock.localDate(),
                daysFromToday: days)
        }

        let todo = try UnscheduledTodo(
            id: request.id,
            title: request.title,
            scheduledDate: date,
            isMandatory: request.isMandatory)
        let record = UnscheduledTodoRecord(todo: todo)
        try await repository.save(record)
        return record
    }

}

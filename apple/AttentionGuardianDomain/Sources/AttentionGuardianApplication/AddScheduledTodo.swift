import Foundation
import AttentionGuardianDomain

public struct AddScheduledTodoRequest: Sendable {
    public let id: UUID
    public let title: String
    public let start: Date?
    public let duration: TimeInterval
    public let utcOffsetSeconds: Int
    public let isMandatory: Bool

    public init(
        id: UUID,
        title: String,
        start: Date?,
        duration: TimeInterval,
        utcOffsetSeconds: Int,
        isMandatory: Bool
    ) {
        self.id = id
        self.title = title
        self.start = start
        self.duration = duration
        self.utcOffsetSeconds = utcOffsetSeconds
        self.isMandatory = isMandatory
    }
}

public enum AddScheduledTodoError: Error, Equatable {
    case invalidDuration
    case currentSelectionPriorityOverflow
}

public struct AddScheduledTodoResult: Equatable, Sendable {
    public let scheduledTodos: [ScheduledTodo]
    public let conflicts: [ScheduleConflict]
    public let hasRolloverToNextDay: Bool
}

public struct AddScheduledTodoUseCase: Sendable {
    private let repository: any ScheduledTodoRepository
    private let clock: any Clock

    public init(
        repository: any ScheduledTodoRepository,
        clock: any Clock
    ) {
        self.repository = repository
        self.clock = clock
    }

    public func execute(
        _ request: AddScheduledTodoRequest
    ) async throws -> AddScheduledTodoResult {
        guard request.duration > 0 else {
            throw AddScheduledTodoError.invalidDuration
        }

        let now = clock.now
        let records = try await repository.loadAll()
        let completed = TodoLifecycle.completeDue(records, at: now)
        let active = completed
            .filter { $0.status == .active }
            .map(\.todo)
        let maximumPriority = active.map(\.currentSelectionPriority).max()
        guard maximumPriority != Int64.max else {
            throw AddScheduledTodoError.currentSelectionPriorityOverflow
        }

        let priority = maximumPriority.map { $0 + 1 } ?? 0
        let start = request.start ?? now
        let proposed = try ScheduledTodo(
            id: request.id,
            title: request.title,
            start: start,
            end: start.addingTimeInterval(request.duration),
            utcOffsetSeconds: request.utcOffsetSeconds,
            isMandatory: request.isMandatory,
            currentSelectionPriority: priority)
        let trial = try ScheduleTrial.insert(proposed, into: active)
        let replacement = TodoLifecycle.replaceActiveSchedule(
            completed,
            with: trial.scheduledTodos)

        try await repository.replaceAll(replacement)
        return AddScheduledTodoResult(
            scheduledTodos: trial.scheduledTodos,
            conflicts: trial.conflicts,
            hasRolloverToNextDay: trial.hasRolloverToNextDay)
    }
}

import Foundation
import AttentionGuardianDomain

public enum PlanFutureTodoError: Error, Equatable {
    case activeFutureTodoNotFound
}

public struct PlanFutureTodoResult: Equatable, Sendable {
    public let scheduledTodo: ScheduledTodo
    public let didWriteSchedule: Bool
    public let conflicts: [ScheduleConflict]
    public let hasRolloverToNextDay: Bool
}

public struct PlanFutureTodoUseCase: Sendable {
    private let scheduledRepository: any ScheduledTodoRepository
    private let futureRepository: any FutureTodoRepository
    private let clock: any Clock

    public init(
        scheduledRepository: any ScheduledTodoRepository,
        futureRepository: any FutureTodoRepository,
        clock: any Clock
    ) {
        self.scheduledRepository = scheduledRepository
        self.futureRepository = futureRepository
        self.clock = clock
    }

    public func execute(
        futureTodoId: UUID,
        start: Date,
        duration: TimeInterval,
        utcOffsetSeconds: Int
    ) async throws -> PlanFutureTodoResult {
        guard duration > 0 else {
            throw AddScheduledTodoError.invalidDuration
        }
        let original = try await scheduledRepository.loadAll()
        let records = TodoLifecycle.completeDue(original, at: clock.now)

        if let existing = records.first(where: {
            $0.todo.id == futureTodoId
        }) {
            try await futureRepository.markPlanned(id: futureTodoId)
            return PlanFutureTodoResult(
                scheduledTodo: existing.todo,
                didWriteSchedule: false,
                conflicts: [],
                hasRolloverToNextDay: false)
        }

        guard let source = try await futureRepository.findActive(
            id: futureTodoId)
        else {
            throw PlanFutureTodoError.activeFutureTodoNotFound
        }
        let active = records.filter { $0.status == .active }.map(\.todo)
        let maximum = active.map(\.currentSelectionPriority).max()
        guard maximum != Int64.max else {
            throw AddScheduledTodoError.currentSelectionPriorityOverflow
        }
        let scheduled = try ScheduledTodo(
            id: source.todo.id,
            title: source.todo.title,
            start: start,
            end: start.addingTimeInterval(duration),
            utcOffsetSeconds: utcOffsetSeconds,
            isMandatory: source.todo.isMandatory,
            currentSelectionPriority: maximum.map { $0 + 1 } ?? 0)
        let trial = try ScheduleTrial.insert(scheduled, into: active)
        try await scheduledRepository.replaceAll(
            TodoLifecycle.replaceActiveSchedule(
                records,
                with: trial.scheduledTodos))
        try await futureRepository.markPlanned(id: futureTodoId)
        return PlanFutureTodoResult(
            scheduledTodo: scheduled,
            didWriteSchedule: true,
            conflicts: trial.conflicts,
            hasRolloverToNextDay: trial.hasRolloverToNextDay)
    }
}

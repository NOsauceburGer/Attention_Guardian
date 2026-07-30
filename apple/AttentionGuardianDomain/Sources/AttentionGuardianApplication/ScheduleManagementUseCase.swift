import Foundation
import AttentionGuardianDomain

public struct ScheduleManagementUseCase: Sendable {
    private let repository: any ScheduledTodoRepository
    private let clock: any Clock

    public init(
        repository: any ScheduledTodoRepository,
        clock: any Clock
    ) {
        self.repository = repository
        self.clock = clock
    }

    public func load() async throws -> [ScheduledTodo] {
        let context = try await loadContext()
        if context.records != context.original {
            try await repository.replaceAll(context.records)
        }
        return context.active
    }

    public func reorder(
        todoId: UUID,
        requestedIndex: Int
    ) async throws -> ScheduleReorderResult {
        let context = try await loadContext()
        let result = try ScheduleManagement.reorder(
            context.active,
            todoId: todoId,
            requestedIndex: requestedIndex)
        try await persist(result.scheduledTodos, over: context.records)
        return result
    }

    public func edit(
        todoId: UUID,
        title: String,
        duration: TimeInterval,
        isMandatory: Bool
    ) async throws -> [ScheduledTodo] {
        let context = try await loadContext()
        let schedule = try ScheduleManagement.edit(
            context.active,
            todoId: todoId,
            title: title,
            duration: duration,
            isMandatory: isMandatory)
        try await persist(schedule, over: context.records)
        return schedule
    }

    public func delete(todoId: UUID) async throws -> [ScheduledTodo] {
        let context = try await loadContext()
        let schedule = try ScheduleManagement.delete(
            context.active,
            todoId: todoId)
        let deleted = TodoLifecycle.deleteScheduled(
            context.records,
            id: todoId,
            confirmed: true)
        try await persist(schedule, over: deleted)
        return schedule
    }

    public func insertBreak(
        id: UUID,
        start: Date,
        duration: TimeInterval,
        utcOffsetSeconds: Int
    ) async throws -> ScheduleTrialResult {
        let context = try await loadContext()
        let maximum = context.active.map(\.currentSelectionPriority).max()
        guard maximum != Int64.max else {
            throw AddScheduledTodoError.currentSelectionPriorityOverflow
        }
        let result = try ScheduleManagement.insertBreak(
            into: context.active,
            id: id,
            start: start,
            duration: duration,
            utcOffsetSeconds: utcOffsetSeconds,
            currentSelectionPriority: maximum.map { $0 + 1 } ?? 0)
        try await persist(result.scheduledTodos, over: context.records)
        return result
    }

    public func editStart(
        todoId: UUID,
        newStart: Date,
        conflictResolution: StartTimeConflictResolution?
    ) async throws -> ScheduleStartEditResult {
        let context = try await loadContext()
        let result = try ScheduleManagement.editStart(
            context.active,
            todoId: todoId,
            newStart: newStart,
            conflictResolution: conflictResolution)
        guard result.rejection == .none else {
            return result
        }
        try await persist(result.scheduledTodos, over: context.records)
        return result
    }

    private func loadContext() async throws -> (
        original: [ScheduledTodoRecord],
        records: [ScheduledTodoRecord],
        active: [ScheduledTodo]
    ) {
        let original = try await repository.loadAll()
        let records = TodoLifecycle.completeDue(original, at: clock.now)
        return (
            original,
            records,
            records.filter { $0.status == .active }.map(\.todo))
    }

    private func persist(
        _ schedule: [ScheduledTodo],
        over records: [ScheduledTodoRecord]
    ) async throws {
        try await repository.replaceAll(
            TodoLifecycle.replaceActiveSchedule(records, with: schedule))
    }
}

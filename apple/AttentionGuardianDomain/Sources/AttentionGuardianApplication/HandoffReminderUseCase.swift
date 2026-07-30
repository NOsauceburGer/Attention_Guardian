import Foundation
import AttentionGuardianDomain

public actor ReminderDeduplicationState {
    private var notifiedTodoIds: Set<UUID> = []

    public init() {}

    func claim(_ todoId: UUID) -> Bool {
        notifiedTodoIds.insert(todoId).inserted
    }
}

public struct PendingHandoffReminder: Equatable, Sendable {
    public let currentTodo: ScheduledTodo
    public let nextTodo: ScheduledTodo
    public let reminderAt: Date

    public init(
        currentTodo: ScheduledTodo,
        nextTodo: ScheduledTodo,
        reminderAt: Date
    ) {
        self.currentTodo = currentTodo
        self.nextTodo = nextTodo
        self.reminderAt = reminderAt
    }
}

public struct HandoffReminderUseCase: Sendable {
    private let repository: any ScheduledTodoRepository
    private let clock: any Clock
    private let deduplicationState: ReminderDeduplicationState

    public init(
        repository: any ScheduledTodoRepository,
        clock: any Clock,
        deduplicationState: ReminderDeduplicationState
    ) {
        self.repository = repository
        self.clock = clock
        self.deduplicationState = deduplicationState
    }

    public func evaluate() async throws -> PendingHandoffReminder? {
        let records = try await repository.loadAll()
        let active = records
            .filter { $0.status == .active }
            .map(\.todo)
        let evaluation = HandoffReminderPolicy.evaluate(
            active,
            at: clock.now)
        guard evaluation.shouldNotifyNow,
              let current = evaluation.currentTodo,
              let next = evaluation.nextTodo,
              let reminderAt = evaluation.reminderAt,
              await deduplicationState.claim(current.id)
        else {
            return nil
        }
        return PendingHandoffReminder(
            currentTodo: current,
            nextTodo: next,
            reminderAt: reminderAt)
    }
}

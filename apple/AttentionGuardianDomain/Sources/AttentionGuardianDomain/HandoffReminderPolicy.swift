import Foundation

public enum HandoffReminderIneligibility: String, Equatable, Sendable {
    case none
    case noCurrentTodo
    case currentTodoTooShort
    case noAdjacentNextTodo
    case nextTodoIsBreak
    case outsideReminderWindow
}

public struct HandoffReminderEvaluation: Equatable, Sendable {
    public let currentTodo: ScheduledTodo?
    public let nextTodo: ScheduledTodo?
    public let reminderAt: Date?
    public let shouldNotifyNow: Bool
    public let ineligibility: HandoffReminderIneligibility
}

public enum HandoffReminderPolicy {
    public static let leadTime: TimeInterval = 5 * 60

    public static func evaluate(
        _ schedule: [ScheduledTodo],
        at currentTime: Date
    ) -> HandoffReminderEvaluation {
        let ordered = schedule.sorted(by: stableOrder)
        guard let current = ScheduledTodoSelector.current(
            in: ordered,
            at: currentTime)
        else {
            return ineligible(.noCurrentTodo)
        }

        guard current.duration >= leadTime else {
            return ineligible(.currentTodoTooShort, current: current)
        }

        guard let next = ordered.first(where: {
            $0.id != current.id && $0.start == current.end
        }) else {
            return ineligible(.noAdjacentNextTodo, current: current)
        }

        guard next.title != ScheduleManagement.breakTitle else {
            return ineligible(
                .nextTodoIsBreak,
                current: current,
                next: next)
        }

        let reminderAt = current.end.addingTimeInterval(-leadTime)
        let shouldNotify =
            currentTime >= reminderAt
            && currentTime < current.end
        return HandoffReminderEvaluation(
            currentTodo: current,
            nextTodo: next,
            reminderAt: reminderAt,
            shouldNotifyNow: shouldNotify,
            ineligibility: shouldNotify ? .none : .outsideReminderWindow)
    }

    private static func ineligible(
        _ reason: HandoffReminderIneligibility,
        current: ScheduledTodo? = nil,
        next: ScheduledTodo? = nil
    ) -> HandoffReminderEvaluation {
        HandoffReminderEvaluation(
            currentTodo: current,
            nextTodo: next,
            reminderAt: nil,
            shouldNotifyNow: false,
            ineligibility: reason)
    }

    private static func stableOrder(
        _ left: ScheduledTodo,
        _ right: ScheduledTodo
    ) -> Bool {
        if left.start != right.start { return left.start < right.start }
        if left.end != right.end { return left.end < right.end }
        return left.id.uuidString.lowercased() < right.id.uuidString.lowercased()
    }
}

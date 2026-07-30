import Foundation

public enum ScheduledTodoSelector {
    public static func current(
        in scheduledTodos: [ScheduledTodo],
        at instant: Date
    ) -> ScheduledTodo? {
        scheduledTodos
            .filter { $0.contains(instant) }
            .sorted(by: comesBeforeForCurrentSelection)
            .first
    }

    private static func comesBeforeForCurrentSelection(
        _ left: ScheduledTodo,
        _ right: ScheduledTodo
    ) -> Bool {
        if left.currentSelectionPriority != right.currentSelectionPriority {
            return left.currentSelectionPriority > right.currentSelectionPriority
        }
        if left.start != right.start {
            return left.start < right.start
        }
        if left.end != right.end {
            return left.end < right.end
        }
        return left.id.uuidString.lowercased() < right.id.uuidString.lowercased()
    }
}

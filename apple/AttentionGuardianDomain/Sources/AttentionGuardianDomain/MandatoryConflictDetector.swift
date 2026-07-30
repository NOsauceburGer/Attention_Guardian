import Foundation

public struct MandatoryConflict: Equatable, Sendable {
    public let first: ScheduledTodo
    public let second: ScheduledTodo
}

public enum MandatoryConflictDetector {
    public static func detect(
        in schedule: [ScheduledTodo],
        endingAfter instant: Date
    ) -> [MandatoryConflict] {
        let mandatory = schedule
            .filter { $0.isMandatory && $0.end > instant }
            .sorted(by: stableOrder)
        var conflicts: [MandatoryConflict] = []
        for firstIndex in mandatory.indices {
            for secondIndex in mandatory.index(after: firstIndex)..<mandatory.endIndex {
                let first = mandatory[firstIndex]
                let second = mandatory[secondIndex]
                if first.start < second.end && second.start < first.end {
                    conflicts.append(MandatoryConflict(first: first, second: second))
                }
            }
        }
        return conflicts
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

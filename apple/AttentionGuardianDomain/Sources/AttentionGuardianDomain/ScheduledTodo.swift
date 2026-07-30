import Foundation

public enum ScheduledTodoError: Error, Equatable {
    case emptyIdentifier
    case blankTitle
    case invalidTimeRange
    case negativeCurrentSelectionPriority
}

public struct ScheduledTodo: Equatable, Sendable {
    public let id: UUID
    public let title: String
    public let start: Date
    public let end: Date
    public let utcOffsetSeconds: Int
    public let isMandatory: Bool
    public let currentSelectionPriority: Int64

    public init(
        id: UUID,
        title: String,
        start: Date,
        end: Date,
        utcOffsetSeconds: Int = 0,
        isMandatory: Bool = false,
        currentSelectionPriority: Int64 = 0
    ) throws {
        guard id != UUID(uuid: (0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)) else {
            throw ScheduledTodoError.emptyIdentifier
        }
        guard !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw ScheduledTodoError.blankTitle
        }
        guard end > start else {
            throw ScheduledTodoError.invalidTimeRange
        }
        guard currentSelectionPriority >= 0 else {
            throw ScheduledTodoError.negativeCurrentSelectionPriority
        }

        self.id = id
        self.title = title
        self.start = start
        self.end = end
        self.utcOffsetSeconds = utcOffsetSeconds
        self.isMandatory = isMandatory
        self.currentSelectionPriority = currentSelectionPriority
    }

    public func contains(_ instant: Date) -> Bool {
        start <= instant && instant < end
    }

    public var duration: TimeInterval {
        end.timeIntervalSince(start)
    }

    public func moved(to newStart: Date) throws -> ScheduledTodo {
        try ScheduledTodo(
            id: id,
            title: title,
            start: newStart,
            end: newStart.addingTimeInterval(duration),
            utcOffsetSeconds: utcOffsetSeconds,
            isMandatory: isMandatory,
            currentSelectionPriority: currentSelectionPriority)
    }

    var localDateOrdinal: Int {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone =
            TimeZone(secondsFromGMT: utcOffsetSeconds)
            ?? TimeZone(secondsFromGMT: 0)!
        let components = calendar.dateComponents([.year, .month, .day], from: start)
        return (components.year ?? 0) * 10_000
            + (components.month ?? 0) * 100
            + (components.day ?? 0)
    }
}

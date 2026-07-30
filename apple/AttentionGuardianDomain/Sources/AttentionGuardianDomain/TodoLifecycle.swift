import Foundation

public enum TodoLifecycleError: Error, Equatable {
    case invalidLocalDate
    case blankTitle
    case invalidRelativeDays
    case invalidTransition
}

public struct LocalDate: Equatable, Comparable, Hashable, Sendable, CustomStringConvertible {
    public let year: Int
    public let month: Int
    public let day: Int

    public init(year: Int, month: Int, day: Int) throws {
        var components = DateComponents()
        components.calendar = Calendar(identifier: .gregorian)
        components.timeZone = TimeZone(secondsFromGMT: 0)
        components.year = year
        components.month = month
        components.day = day
        guard let date = components.date,
              Calendar(identifier: .gregorian).dateComponents(
                [.year, .month, .day],
                from: date) == DateComponents(year: year, month: month, day: day)
        else {
            throw TodoLifecycleError.invalidLocalDate
        }
        self.year = year
        self.month = month
        self.day = day
    }

    public init(iso8601: String) throws {
        let pieces = iso8601.split(separator: "-")
        guard pieces.count == 3,
              let year = Int(pieces[0]),
              let month = Int(pieces[1]),
              let day = Int(pieces[2])
        else {
            throw TodoLifecycleError.invalidLocalDate
        }
        try self.init(year: year, month: month, day: day)
    }

    public var description: String {
        String(format: "%04d-%02d-%02d", year, month, day)
    }

    public static func < (left: LocalDate, right: LocalDate) -> Bool {
        (left.year, left.month, left.day) < (right.year, right.month, right.day)
    }

    func adding(days: Int) throws -> LocalDate {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(secondsFromGMT: 0)!
        var components = DateComponents()
        components.year = year
        components.month = month
        components.day = day
        let date = try calendar.date(from: components)
            .flatMap { calendar.date(byAdding: .day, value: days, to: $0) }
            .unwrap(or: TodoLifecycleError.invalidLocalDate)
        let result = calendar.dateComponents([.year, .month, .day], from: date)
        return try LocalDate(
            year: result.year!,
            month: result.month!,
            day: result.day!)
    }
}

public struct UnscheduledTodo: Equatable, Sendable {
    public let id: UUID
    public let title: String
    public let scheduledDate: LocalDate
    public let isMandatory: Bool

    public init(
        id: UUID,
        title: String,
        scheduledDate: LocalDate,
        isMandatory: Bool = false
    ) throws {
        guard !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw TodoLifecycleError.blankTitle
        }
        self.id = id
        self.title = title
        self.scheduledDate = scheduledDate
        self.isMandatory = isMandatory
    }
}

public enum ScheduledTodoStatus: String, Equatable, Sendable {
    case active
    case completed
    case deleted
}

public enum UnscheduledTodoStatus: String, Equatable, Sendable {
    case active
    case planned
    case deleted
}

public struct ScheduledTodoRecord: Equatable, Sendable {
    public let todo: ScheduledTodo
    public let status: ScheduledTodoStatus
    public let completedAt: Date?

    public init(
        todo: ScheduledTodo,
        status: ScheduledTodoStatus = .active,
        completedAt: Date? = nil
    ) {
        self.todo = todo
        self.status = status
        self.completedAt = completedAt
    }
}

public struct UnscheduledTodoRecord: Equatable, Sendable {
    public let todo: UnscheduledTodo
    public let status: UnscheduledTodoStatus

    public init(
        todo: UnscheduledTodo,
        status: UnscheduledTodoStatus = .active
    ) {
        self.todo = todo
        self.status = status
    }
}

public enum TodoLifecycle {
    public static func markPlanned(
        _ record: UnscheduledTodoRecord
    ) throws -> UnscheduledTodoRecord {
        guard record.status != .deleted else {
            throw TodoLifecycleError.invalidTransition
        }
        return UnscheduledTodoRecord(todo: record.todo, status: .planned)
    }

    public static func delete(
        _ record: UnscheduledTodoRecord,
        confirmed: Bool
    ) -> UnscheduledTodoRecord {
        guard confirmed, record.status == .active else { return record }
        return UnscheduledTodoRecord(todo: record.todo, status: .deleted)
    }

    public static func completeDue(
        _ records: [ScheduledTodoRecord],
        at instant: Date
    ) -> [ScheduledTodoRecord] {
        records.map { record in
            guard record.status == .active, record.todo.end <= instant else {
                return record
            }
            return ScheduledTodoRecord(
                todo: record.todo,
                status: .completed,
                completedAt: instant)
        }
    }

    public static func deleteScheduled(
        _ records: [ScheduledTodoRecord],
        id: UUID,
        confirmed: Bool
    ) -> [ScheduledTodoRecord] {
        guard confirmed else { return records }
        return records.map { record in
            guard record.todo.id == id, record.status == .active else {
                return record
            }
            return ScheduledTodoRecord(todo: record.todo, status: .deleted)
        }
    }

    public static func dueFutureTodos(
        _ records: [UnscheduledTodoRecord],
        onOrBefore date: LocalDate
    ) -> [UnscheduledTodoRecord] {
        records
            .filter { $0.status == .active && $0.todo.scheduledDate <= date }
            .sorted {
                if $0.todo.scheduledDate != $1.todo.scheduledDate {
                    return $0.todo.scheduledDate < $1.todo.scheduledDate
                }
                return $0.todo.id.uuidString.lowercased()
                    < $1.todo.id.uuidString.lowercased()
            }
    }

    public static func replaceActiveSchedule(
        _ records: [ScheduledTodoRecord],
        with replacement: [ScheduledTodo]
    ) -> [ScheduledTodoRecord] {
        let history = records.filter { $0.status != .active }
        let active = replacement.map { ScheduledTodoRecord(todo: $0) }
        return history + active
    }

    public static func relativeDate(
        from today: LocalDate,
        daysFromToday: Int
    ) throws -> LocalDate {
        guard daysFromToday >= 1 else {
            throw TodoLifecycleError.invalidRelativeDays
        }
        return try today.adding(days: daysFromToday)
    }
}

private extension Optional {
    func unwrap(or error: @autoclosure () -> Error) throws -> Wrapped {
        guard let value = self else { throw error() }
        return value
    }
}

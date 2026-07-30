import Foundation

public enum ScheduleManagementError: Error, Equatable {
    case todoNotFound
    case invalidRequestedIndex
    case mandatoryMoveOutsideContinuousGroup
    case breakCannotBeRenamed
}

public enum StartTimeConflictResolution: String, Sendable {
    case moveExistingAfterEdited
    case truncateExistingAtNewStart
}

public enum StartTimeEditRejection: String, Sendable {
    case none
    case conflictResolutionRequired
    case mandatoryTodoOccupiesNewStart
}

public struct ScheduleReorderResult: Equatable, Sendable {
    public let scheduledTodos: [ScheduledTodo]
    public let actualIndex: Int
    public let usedFallbackPosition: Bool
}

public struct ScheduleStartEditResult: Equatable, Sendable {
    public let scheduledTodos: [ScheduledTodo]
    public let rejection: StartTimeEditRejection
    public let conflictingTodoId: UUID?
}

public enum ScheduleManagement {
    public static let breakTitle = "休息"

    public static func mandatoryContinuousGroups(
        _ schedule: [ScheduledTodo]
    ) -> [[ScheduledTodo]] {
        mandatoryGroups(schedule)
    }

    public static func reorder(
        _ schedule: [ScheduledTodo],
        todoId: UUID,
        requestedIndex: Int
    ) throws -> ScheduleReorderResult {
        let ordered = stable(schedule)
        guard ordered.indices.contains(requestedIndex) else {
            throw ScheduleManagementError.invalidRequestedIndex
        }
        guard let moving = ordered.first(where: { $0.id == todoId }) else {
            throw ScheduleManagementError.todoNotFound
        }

        if moving.isMandatory {
            return try reorderMandatory(
                ordered,
                moving: moving,
                requestedIndex: requestedIndex)
        }

        var desired = ordered.filter { $0.id != todoId }
        desired.insert(moving, at: requestedIndex)
        let rebuilt = try rebuild(desired, anchor: ordered[0].start)
        let actualIndex = rebuilt.firstIndex(where: { $0.id == todoId })!
        return ScheduleReorderResult(
            scheduledTodos: rebuilt,
            actualIndex: actualIndex,
            usedFallbackPosition: actualIndex != requestedIndex)
    }

    public static func delete(
        _ schedule: [ScheduledTodo],
        todoId: UUID
    ) throws -> [ScheduledTodo] {
        let ordered = stable(schedule)
        guard let index = ordered.firstIndex(where: { $0.id == todoId }) else {
            throw ScheduleManagementError.todoNotFound
        }
        let before = Array(ordered[..<index])
        let after = Array(ordered[(index + 1)...])
        let rebuiltAfter = try rebuild(after, anchor: ordered[index].start)
        return stable(before + rebuiltAfter)
    }

    public static func edit(
        _ schedule: [ScheduledTodo],
        todoId: UUID,
        title: String,
        duration: TimeInterval,
        isMandatory: Bool
    ) throws -> [ScheduledTodo] {
        let ordered = stable(schedule)
        guard let index = ordered.firstIndex(where: { $0.id == todoId }) else {
            throw ScheduleManagementError.todoNotFound
        }
        if ordered[index].title == breakTitle && title != breakTitle {
            throw ScheduleManagementError.breakCannotBeRenamed
        }

        let current = ordered[index]
        var desired = ordered
        desired[index] = try ScheduledTodo(
            id: current.id,
            title: title,
            start: current.start,
            end: current.start.addingTimeInterval(duration),
            utcOffsetSeconds: current.utcOffsetSeconds,
            isMandatory: isMandatory,
            currentSelectionPriority: current.currentSelectionPriority)
        return try rebuild(desired, anchor: ordered[0].start)
    }

    public static func insertBreak(
        into schedule: [ScheduledTodo],
        id: UUID,
        start: Date,
        duration: TimeInterval,
        utcOffsetSeconds: Int = 0,
        currentSelectionPriority: Int64 = 0
    ) throws -> ScheduleTrialResult {
        let rest = try ScheduledTodo(
            id: id,
            title: breakTitle,
            start: start,
            end: start.addingTimeInterval(duration),
            utcOffsetSeconds: utcOffsetSeconds,
            isMandatory: false,
            currentSelectionPriority: currentSelectionPriority)
        return try ScheduleTrial.insert(rest, into: schedule)
    }

    public static func editStart(
        _ schedule: [ScheduledTodo],
        todoId: UUID,
        newStart: Date,
        conflictResolution: StartTimeConflictResolution?
    ) throws -> ScheduleStartEditResult {
        let ordered = stable(schedule)
        guard let moving = ordered.first(where: { $0.id == todoId }) else {
            throw ScheduleManagementError.todoNotFound
        }
        let others = ordered.filter { $0.id != todoId }
        let occupying = others.first { $0.contains(newStart) }

        if let occupying, occupying.isMandatory {
            return ScheduleStartEditResult(
                scheduledTodos: ordered,
                rejection: .mandatoryTodoOccupiesNewStart,
                conflictingTodoId: occupying.id)
        }
        if let occupying, conflictResolution == nil {
            return ScheduleStartEditResult(
                scheduledTodos: ordered,
                rejection: .conflictResolutionRequired,
                conflictingTodoId: occupying.id)
        }

        var prefix = others.filter {
            $0.id != occupying?.id && $0.end <= newStart
        }
        let prefixIds = Set(prefix.map(\.id))
        let remaining = others.filter {
            $0.id != occupying?.id && !prefixIds.contains($0.id)
        }
        let moved = try moving.moved(to: newStart)

        if let occupying, conflictResolution == .truncateExistingAtNewStart {
            prefix.append(try ScheduledTodo(
                id: occupying.id,
                title: occupying.title,
                start: occupying.start,
                end: newStart,
                utcOffsetSeconds: occupying.utcOffsetSeconds,
                isMandatory: occupying.isMandatory,
                currentSelectionPriority: occupying.currentSelectionPriority))
        }

        var suffix = [moved]
        if let occupying, conflictResolution == .moveExistingAfterEdited {
            suffix.append(occupying)
        }
        suffix.append(contentsOf: remaining)
        let rebuilt = try rebuild(suffix, anchor: newStart)
        return ScheduleStartEditResult(
            scheduledTodos: stable(prefix + rebuilt),
            rejection: .none,
            conflictingTodoId: occupying?.id)
    }

    private static func reorderMandatory(
        _ ordered: [ScheduledTodo],
        moving: ScheduledTodo,
        requestedIndex: Int
    ) throws -> ScheduleReorderResult {
        guard let group = mandatoryGroups(ordered).first(
            where: { $0.contains(where: { $0.id == moving.id }) })
        else {
            throw ScheduleManagementError.mandatoryMoveOutsideContinuousGroup
        }
        let indices = group.compactMap { grouped in
            ordered.firstIndex(where: { $0.id == grouped.id })
        }.sorted()
        guard indices.contains(requestedIndex) else {
            throw ScheduleManagementError.mandatoryMoveOutsideContinuousGroup
        }

        var reordered = group.filter { $0.id != moving.id }
        reordered.insert(moving, at: indices.firstIndex(of: requestedIndex)!)
        var cursor = group.map(\.start).min()!
        var replacements: [UUID: ScheduledTodo] = [:]
        for todo in reordered {
            let shifted = try todo.moved(to: cursor)
            replacements[todo.id] = shifted
            cursor = shifted.end
        }
        let desired = stable(ordered.map { replacements[$0.id] ?? $0 })
        let rebuilt = try rebuild(desired, anchor: ordered[0].start)
        return ScheduleReorderResult(
            scheduledTodos: rebuilt,
            actualIndex: rebuilt.firstIndex(where: { $0.id == moving.id })!,
            usedFallbackPosition: false)
    }

    private static func mandatoryGroups(
        _ schedule: [ScheduledTodo]
    ) -> [[ScheduledTodo]] {
        let mandatory = stable(schedule.filter(\.isMandatory))
        var groups: [[ScheduledTodo]] = []
        var current: [ScheduledTodo] = []
        var groupEnd: Date?
        for todo in mandatory {
            if current.isEmpty || todo.start <= groupEnd! {
                current.append(todo)
                groupEnd = max(groupEnd ?? todo.end, todo.end)
            } else {
                if current.count >= 2 { groups.append(current) }
                current = [todo]
                groupEnd = todo.end
            }
        }
        if current.count >= 2 { groups.append(current) }
        return groups
    }

    private static func rebuild(
        _ desired: [ScheduledTodo],
        anchor: Date
    ) throws -> [ScheduledTodo] {
        let mandatory = stable(desired.filter(\.isMandatory))
        var result: [ScheduledTodo] = []
        var cursor = anchor
        for todo in desired {
            if todo.isMandatory {
                result.append(todo)
                cursor = max(cursor, todo.end)
                continue
            }
            var shifted = try todo.moved(to: cursor)
            for blocker in mandatory where overlaps(shifted, blocker) {
                shifted = try shifted.moved(to: blocker.end)
            }
            result.append(shifted)
            cursor = shifted.end
        }
        return stable(result)
    }

    private static func overlaps(
        _ left: ScheduledTodo,
        _ right: ScheduledTodo
    ) -> Bool {
        left.start < right.end && right.start < left.end
    }

    private static func stable(_ schedule: [ScheduledTodo]) -> [ScheduledTodo] {
        schedule.sorted {
            if $0.start != $1.start { return $0.start < $1.start }
            if $0.end != $1.end { return $0.end < $1.end }
            return $0.id.uuidString.lowercased() < $1.id.uuidString.lowercased()
        }
    }
}

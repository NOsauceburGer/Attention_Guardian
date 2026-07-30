import Foundation

public enum ScheduleTrialError: Error, Equatable {
    case duplicateIdentifier
}

public struct ScheduleConflict: Equatable, Sendable {
    public let proposedTodo: ScheduledTodo
    public let mandatoryTodo: ScheduledTodo
}

public struct ScheduleTrialResult: Equatable, Sendable {
    public let scheduledTodos: [ScheduledTodo]
    public let hasRolloverToNextDay: Bool
    public let conflicts: [ScheduleConflict]
}

public enum ScheduleTrial {
    public static func insert(
        _ proposedTodo: ScheduledTodo,
        into existingSchedule: [ScheduledTodo]
    ) throws -> ScheduleTrialResult {
        try validateIdentifiers(existingSchedule, proposedTodo: proposedTodo)

        let ordered = existingSchedule.sorted(by: stableScheduleOrder)
        let mandatoryTodos = ordered.filter(\.isMandatory)
        let conflicts = proposedTodo.isMandatory
            ? mandatoryTodos
                .filter { overlaps($0, proposedTodo) }
                .map {
                    ScheduleConflict(
                        proposedTodo: proposedTodo,
                        mandatoryTodo: $0)
                }
            : []

        let originalProposedTodo = proposedTodo
        let placedProposedTodo = proposedTodo.isMandatory
            ? proposedTodo
            : try movePastMandatoryTodos(proposedTodo, mandatoryTodos: mandatoryTodos)

        var result: [ScheduledTodo] = []
        var affected: [ScheduledTodo] = []
        for todo in ordered {
            if todo.end <= placedProposedTodo.start {
                result.append(todo)
            } else {
                affected.append(todo)
            }
        }

        result.append(placedProposedTodo)
        var occupiedUntil = placedProposedTodo.end
        var chainIsActive = true
        var hasRollover =
            placedProposedTodo.localDateOrdinal > originalProposedTodo.localDateOrdinal

        for todo in affected {
            guard chainIsActive else {
                result.append(todo)
                continue
            }

            if todo.isMandatory {
                if todo.start < occupiedUntil {
                    occupiedUntil = max(todo.end, occupiedUntil)
                    result.append(todo)
                } else {
                    chainIsActive = false
                    result.append(todo)
                }
                continue
            }

            if todo.start >= occupiedUntil {
                chainIsActive = false
                result.append(todo)
                continue
            }

            let shifted = try movePastMandatoryTodos(
                todo.moved(to: occupiedUntil),
                mandatoryTodos: mandatoryTodos)
            hasRollover =
                hasRollover || shifted.localDateOrdinal > todo.localDateOrdinal
            result.append(shifted)
            occupiedUntil = shifted.end
        }

        return ScheduleTrialResult(
            scheduledTodos: result.sorted(by: stableScheduleOrder),
            hasRolloverToNextDay: hasRollover,
            conflicts: conflicts)
    }

    private static func movePastMandatoryTodos(
        _ todo: ScheduledTodo,
        mandatoryTodos: [ScheduledTodo]
    ) throws -> ScheduledTodo {
        var moved = todo
        for mandatoryTodo in mandatoryTodos where overlaps(moved, mandatoryTodo) {
            moved = try moved.moved(to: mandatoryTodo.end)
        }
        return moved
    }

    private static func overlaps(
        _ left: ScheduledTodo,
        _ right: ScheduledTodo
    ) -> Bool {
        left.start < right.end && right.start < left.end
    }

    private static func stableScheduleOrder(
        _ left: ScheduledTodo,
        _ right: ScheduledTodo
    ) -> Bool {
        if left.start != right.start {
            return left.start < right.start
        }
        if left.end != right.end {
            return left.end < right.end
        }
        return left.id.uuidString.lowercased() < right.id.uuidString.lowercased()
    }

    private static func validateIdentifiers(
        _ existingSchedule: [ScheduledTodo],
        proposedTodo: ScheduledTodo
    ) throws {
        let identifiers = existingSchedule.map(\.id)
        guard Set(identifiers).count == identifiers.count,
              !identifiers.contains(proposedTodo.id) else {
            throw ScheduleTrialError.duplicateIdentifier
        }
    }
}

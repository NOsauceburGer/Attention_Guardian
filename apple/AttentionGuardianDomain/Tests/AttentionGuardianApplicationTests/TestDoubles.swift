import Foundation
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

struct FixedClock: Clock {
    let now: Date

    let timeZone: TimeZone

    init(
        now: Date,
        timeZone: TimeZone = TimeZone(secondsFromGMT: 0)!
    ) {
        self.now = now
        self.timeZone = timeZone
    }
}

final class ScheduledTodoRepositoryFake:
    ScheduledTodoRepository,
    @unchecked Sendable
{
    var records: [ScheduledTodoRecord]
    private(set) var loadCount = 0
    private(set) var replacements: [[ScheduledTodoRecord]] = []
    var replacementError: (any Error)?

    init(records: [ScheduledTodoRecord]) {
        self.records = records
    }

    func loadAll() async throws -> [ScheduledTodoRecord] {
        loadCount += 1
        return records
    }

    func replaceAll(_ records: [ScheduledTodoRecord]) async throws {
        if let replacementError {
            throw replacementError
        }
        replacements.append(records)
        self.records = records
    }
}

extension Array {
    var only: Element? {
        count == 1 ? self[0] : nil
    }
}

final class FutureTodoRepositoryFake:
    FutureTodoRepository,
    @unchecked Sendable
{
    enum Failure: Error {
        case markPlanned
    }

    var records: [UnscheduledTodoRecord]
    private(set) var loadCount = 0
    private(set) var savedRecords: [UnscheduledTodoRecord] = []
    var markPlannedFailuresRemaining = 0
    private(set) var operationLog: [String] = []
    private(set) var markDeletedCount = 0

    init(records: [UnscheduledTodoRecord] = []) {
        self.records = records
    }

    func loadAllActive() async throws -> [UnscheduledTodoRecord] {
        loadCount += 1
        return records.filter { $0.status == .active }
    }

    func save(_ record: UnscheduledTodoRecord) async throws {
        savedRecords.append(record)
        records.append(record)
    }

    func findActive(id: UUID) async throws -> UnscheduledTodoRecord? {
        operationLog.append("findActive")
        return records.first {
            $0.todo.id == id && $0.status == .active
        }
    }

    func markPlanned(id: UUID) async throws {
        operationLog.append("markPlanned")
        if markPlannedFailuresRemaining > 0 {
            markPlannedFailuresRemaining -= 1
            throw Failure.markPlanned
        }
        if let index = records.firstIndex(where: { $0.todo.id == id }) {
            records[index] = try TodoLifecycle.markPlanned(records[index])
        }
    }

    func markDeleted(id: UUID) async throws {
        markDeletedCount += 1
        if let index = records.firstIndex(where: { $0.todo.id == id }) {
            records[index] = TodoLifecycle.delete(
                records[index],
                confirmed: true)
        }
    }
}

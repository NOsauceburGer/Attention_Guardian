import Foundation
import Testing
@testable import AttentionGuardianDomain

@Suite("Shared lifecycle vectors")
struct LifecycleVectorTests {
    @Test
    func sharedDomainCasesMatchSwiftDomain() throws {
        let envelope: LifecycleEnvelope = try LifecycleLoader.load(
            "application-lifecycle.json")

        for vector in envelope.cases {
            switch vector.input.action {
            case "planTwice":
                var record = try futureRecord(vector.input.futureTodo)
                record = try TodoLifecycle.markPlanned(record)
                record = try TodoLifecycle.markPlanned(record)
                #expect(record.status.rawValue == vector.expected.futureStatus)
            case "deleteFuture":
                var record = try futureRecord(vector.input.futureTodo)
                record = TodoLifecycle.delete(record, confirmed: vector.input.isConfirmed!)
                #expect(record.status.rawValue == vector.expected.futureStatus)
            case "loadManagement":
                let now = try LifecycleInstant.parse(vector.input.now!).date
                let records = try vector.input.scheduledTodos!.map {
                    ScheduledTodoRecord(todo: try $0.domainValue())
                }
                let updated = TodoLifecycle.completeDue(records, at: now)
                let expectedActive = try expectedUUIDs(
                    vector.expected.activeTodoIds)
                #expect(
                    updated.filter { $0.status == .active }.map(\.todo.id)
                        == expectedActive)
                try expectHistory(updated, expected: vector.expected.history!)
            case "loadOpening":
                let today = try LocalDate(
                    iso8601: try #require(vector.input.now).prefix(10).description)
                let records = try vector.input.futureTodos!.map {
                    UnscheduledTodoRecord(todo: try $0.domainValue())
                }
                let expectedDue = try expectedUUIDs(
                    vector.expected.dueFutureTodoIds)
                #expect(
                    TodoLifecycle.dueFutureTodos(records, onOrBefore: today)
                        .map(\.todo.id)
                        == expectedDue)
            case "deleteScheduled":
                let records = try vector.input.scheduledTodos!.map {
                    ScheduledTodoRecord(todo: try $0.domainValue())
                }
                let updated = TodoLifecycle.deleteScheduled(
                    records,
                    id: try #require(UUID(uuidString: vector.input.todoId!)),
                    confirmed: vector.input.isConfirmed!)
                let expectedActive = try expectedUUIDs(
                    vector.expected.activeTodoIds)
                #expect(
                    updated.filter { $0.status == .active }.map(\.todo.id)
                        == expectedActive)
                try expectHistory(updated, expected: vector.expected.history!)
            case "replaceSchedule":
                let now = try LifecycleInstant.parse(vector.input.now!).date
                let records = TodoLifecycle.completeDue(
                    try vector.input.scheduledTodos!.map {
                        ScheduledTodoRecord(todo: try $0.domainValue())
                    },
                    at: now)
                let replaced = TodoLifecycle.replaceActiveSchedule(
                    records,
                    with: try vector.input.replacementSchedule!.map {
                        try $0.domainValue()
                    })
                try expectHistory(replaced, expected: vector.expected.history!)
            case "addRelativeFuture":
                let today = try LocalDate(
                    iso8601: try #require(vector.input.now).prefix(10).description)
                let actual = try TodoLifecycle.relativeDate(
                    from: today,
                    daysFromToday: vector.input.daysFromToday!)
                #expect(actual.description == vector.expected.savedDate)
            default:
                Issue.record("Unsupported lifecycle action \(vector.input.action)")
            }
        }
    }

    private func futureRecord(_ vector: LifecycleFutureVector?) throws
        -> UnscheduledTodoRecord
    {
        UnscheduledTodoRecord(todo: try #require(vector).domainValue())
    }

    private func expectedUUIDs(_ values: [String]?) throws -> [UUID] {
        try #require(values).map { try #require(UUID(uuidString: $0)) }
    }

    private func expectHistory(
        _ records: [ScheduledTodoRecord],
        expected: [LifecycleHistoryVector]
    ) throws {
        let byId: [UUID: ScheduledTodoRecord] =
            Dictionary(uniqueKeysWithValues: records.map { ($0.todo.id, $0) })
        for item in expected {
            let identifier = try #require(UUID(uuidString: item.id))
            let record = try #require(byId[identifier])
            #expect(record.status.rawValue == item.status)
            #expect(
                record.completedAt
                    == item.completedAt.map { try! LifecycleInstant.parse($0).date })
        }
    }
}

private struct LifecycleEnvelope: Decodable { let cases: [LifecycleCase] }
private struct LifecycleCase: Decodable {
    let input: LifecycleInput
    let expected: LifecycleExpected
}
private struct LifecycleInput: Decodable {
    let action: String
    let now: String?
    let todoId: String?
    let isConfirmed: Bool?
    let daysFromToday: Int?
    let futureTodo: LifecycleFutureVector?
    let futureTodos: [LifecycleFutureVector]?
    let scheduledTodos: [LifecycleScheduledVector]?
    let replacementSchedule: [LifecycleScheduledVector]?
}
private struct LifecycleExpected: Decodable {
    let futureStatus: String?
    let activeTodoIds: [String]?
    let dueFutureTodoIds: [String]?
    let savedDate: String?
    let history: [LifecycleHistoryVector]?
}
private struct LifecycleHistoryVector: Decodable {
    let id: String
    let status: String
    let completedAt: String?
}
private struct LifecycleFutureVector: Decodable {
    let id: String
    let title: String
    let scheduledDate: String
    let isMandatory: Bool
    func domainValue() throws -> UnscheduledTodo {
        try UnscheduledTodo(
            id: #require(UUID(uuidString: id)),
            title: title,
            scheduledDate: LocalDate(iso8601: scheduledDate),
            isMandatory: isMandatory)
    }
}
private struct LifecycleScheduledVector: Decodable {
    let id: String
    let title: String
    let start: String
    let end: String
    let isMandatory: Bool
    let currentSelectionPriority: Int64
    func domainValue() throws -> ScheduledTodo {
        let parsed = try LifecycleInstant.parse(start)
        return try ScheduledTodo(
            id: #require(UUID(uuidString: id)),
            title: title,
            start: parsed.date,
            end: LifecycleInstant.parse(end).date,
            utcOffsetSeconds: parsed.utcOffsetSeconds,
            isMandatory: isMandatory,
            currentSelectionPriority: currentSelectionPriority)
    }
}
private enum LifecycleLoader {
    static func load<Value: Decodable>(_ fileName: String) throws -> Value {
        let packageDirectory = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent()
        let repositoryRoot = packageDirectory
            .deletingLastPathComponent().deletingLastPathComponent()
        return try JSONDecoder().decode(
            Value.self,
            from: Data(contentsOf: repositoryRoot
                .appending(path: "test-vectors/v1")
                .appending(path: fileName)))
    }
}
private enum LifecycleInstant {
    static func parse(_ value: String) throws -> (date: Date, utcOffsetSeconds: Int) {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withColonSeparatorInTimeZone]
        let date = try #require(formatter.date(from: value))
        let offset = value.suffix(6)
        let hours = try #require(Int(offset.dropFirst().prefix(2)))
        let minutes = try #require(Int(offset.suffix(2)))
        return (date, (offset.first == "-" ? -1 : 1) * (hours * 60 + minutes) * 60)
    }
}

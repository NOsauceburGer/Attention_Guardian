import Foundation
import Testing
@testable import AttentionGuardianDomain

@Suite("Shared reminder vectors")
struct ReminderVectorTests {
    @Test
    func sharedVectorsMatchSwiftDomain() throws {
        let envelope: ReminderEnvelope = try ReminderVectorLoader.load(
            "evaluate-reminder.json")

        for vector in envelope.cases {
            let schedule = try vector.input.schedule.map { try $0.domainValue() }
            let now = try ReminderInstant.parse(vector.input.now).date

            let actual = HandoffReminderPolicy.evaluate(schedule, at: now)

            #expect(
                actual.shouldNotifyNow == vector.expected.shouldNotifyNow,
                Comment(rawValue: vector.id))
            #expect(
                actual.ineligibility.rawValue == vector.expected.ineligibility,
                Comment(rawValue: vector.id))
            #expect(
                actual.currentTodo?.id.uuidString.lowercased()
                    == vector.expected.currentTodoId?.lowercased(),
                Comment(rawValue: vector.id))
            #expect(
                actual.nextTodo?.id.uuidString.lowercased()
                    == vector.expected.nextTodoId?.lowercased(),
                Comment(rawValue: vector.id))
        }
    }
}

private struct ReminderEnvelope: Decodable {
    let cases: [ReminderCase]
}

private struct ReminderCase: Decodable {
    let id: String
    let input: ReminderInput
    let expected: ReminderExpected
}

private struct ReminderInput: Decodable {
    let now: String
    let schedule: [ReminderTodoVector]
}

private struct ReminderExpected: Decodable {
    let shouldNotifyNow: Bool
    let ineligibility: String
    let currentTodoId: String?
    let nextTodoId: String?
}

private struct ReminderTodoVector: Decodable {
    let id: String
    let title: String
    let start: String
    let end: String
    let isMandatory: Bool
    let currentSelectionPriority: Int64

    func domainValue() throws -> ScheduledTodo {
        let parsedStart = try ReminderInstant.parse(start)
        return try ScheduledTodo(
            id: try #require(UUID(uuidString: id)),
            title: title,
            start: parsedStart.date,
            end: ReminderInstant.parse(end).date,
            utcOffsetSeconds: parsedStart.utcOffsetSeconds,
            isMandatory: isMandatory,
            currentSelectionPriority: currentSelectionPriority)
    }
}

private enum ReminderVectorLoader {
    static func load<Value: Decodable>(_ fileName: String) throws -> Value {
        let packageDirectory = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let repositoryRoot = packageDirectory
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let url = repositoryRoot
            .appending(path: "test-vectors/v1")
            .appending(path: fileName)
        return try JSONDecoder().decode(Value.self, from: Data(contentsOf: url))
    }
}

private enum ReminderInstant {
    static func parse(_ value: String) throws -> (date: Date, utcOffsetSeconds: Int) {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withColonSeparatorInTimeZone]
        let date = try #require(formatter.date(from: value))
        let offset = value.suffix(6)
        let hours = try #require(Int(offset.dropFirst().prefix(2)))
        let minutes = try #require(Int(offset.suffix(2)))
        let sign = offset.first == "-" ? -1 : 1
        return (date, sign * ((hours * 60 + minutes) * 60))
    }
}

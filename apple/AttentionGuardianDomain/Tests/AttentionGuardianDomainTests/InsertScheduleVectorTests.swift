import Foundation
import Testing
@testable import AttentionGuardianDomain

@Suite("Shared insert-schedule vectors")
struct InsertScheduleVectorTests {
    @Test
    func sharedVectorsMatchSwiftDomain() throws {
        let envelope: InsertScheduleEnvelope = try InsertVectorLoader.load(
            "insert-schedule.json")

        #expect(envelope.schemaVersion == 1)
        #expect(envelope.operation == "insertSchedule")

        for vector in envelope.cases {
            let existing = try vector.input.schedule.map { try $0.domainValue() }
            let proposed = try vector.input.proposedTodo.domainValue()

            let actual = try ScheduleTrial.insert(proposed, into: existing)
            let expected = try vector.expected.schedule.map { try $0.domainValue() }

            #expect(actual.scheduledTodos == expected, Comment(rawValue: vector.id))
            #expect(
                actual.hasRolloverToNextDay == vector.expected.hasRolloverToNextDay,
                Comment(rawValue: vector.id))
            #expect(
                actual.conflicts.map {
                    ConflictVector(
                        proposedTodoId: $0.proposedTodo.id.uuidString.lowercased(),
                        mandatoryTodoId: $0.mandatoryTodo.id.uuidString.lowercased())
                } == vector.expected.conflicts,
                Comment(rawValue: vector.id))
        }
    }
}

private struct InsertScheduleEnvelope: Decodable {
    let schemaVersion: Int
    let operation: String
    let cases: [InsertScheduleCase]
}

private struct InsertScheduleCase: Decodable {
    let id: String
    let input: InsertScheduleInput
    let expected: InsertScheduleExpected
}

private struct InsertScheduleInput: Decodable {
    let schedule: [InsertTodoVector]
    let proposedTodo: InsertTodoVector
}

private struct InsertScheduleExpected: Decodable {
    let schedule: [InsertTodoVector]
    let hasRolloverToNextDay: Bool
    let conflicts: [ConflictVector]
}

private struct ConflictVector: Codable, Equatable {
    let proposedTodoId: String
    let mandatoryTodoId: String
}

private struct InsertTodoVector: Decodable {
    let id: String
    let title: String
    let start: String
    let end: String
    let isMandatory: Bool
    let currentSelectionPriority: Int64

    func domainValue() throws -> ScheduledTodo {
        guard let identifier = UUID(uuidString: id) else {
            throw InsertVectorError.invalidUUID(id)
        }
        let parsedStart = try InsertVectorInstant.parse(start)
        let parsedEnd = try InsertVectorInstant.parse(end)
        return try ScheduledTodo(
            id: identifier,
            title: title,
            start: parsedStart.date,
            end: parsedEnd.date,
            utcOffsetSeconds: parsedStart.utcOffsetSeconds,
            isMandatory: isMandatory,
            currentSelectionPriority: currentSelectionPriority)
    }
}

private enum InsertVectorLoader {
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

private enum InsertVectorInstant {
    static func parse(_ value: String) throws -> (date: Date, utcOffsetSeconds: Int) {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withColonSeparatorInTimeZone]
        guard let date = formatter.date(from: value) else {
            throw InsertVectorError.invalidInstant(value)
        }
        guard value.count >= 6 else {
            throw InsertVectorError.invalidInstant(value)
        }
        let offset = value.suffix(6)
        guard let hours = Int(offset.dropFirst().prefix(2)),
              let minutes = Int(offset.suffix(2)) else {
            throw InsertVectorError.invalidInstant(value)
        }
        let sign = offset.first == "-" ? -1 : 1
        return (date, sign * ((hours * 60 + minutes) * 60))
    }
}

private enum InsertVectorError: Error {
    case invalidUUID(String)
    case invalidInstant(String)
}

import Foundation
import Testing
@testable import AttentionGuardianDomain

@Suite("Shared select-current vectors")
struct SelectCurrentVectorTests {
    @Test
    func sharedVectorsMatchSwiftDomain() throws {
        let envelope: SelectCurrentEnvelope = try SharedVectorLoader.load(
            "select-current.json")

        #expect(envelope.schemaVersion == 1)
        #expect(envelope.operation == "selectCurrent")

        for vector in envelope.cases {
            let schedule = try vector.input.schedule.map { try $0.domainValue() }
            let now = try VectorInstant.parse(vector.input.now)

            let actual = ScheduledTodoSelector.current(in: schedule, at: now)

            #expect(
                actual?.id.uuidString.lowercased()
                    == vector.expected.currentTodoId?.lowercased(),
                Comment(rawValue: vector.id))
        }
    }
}

private struct SelectCurrentEnvelope: Decodable {
    let schemaVersion: Int
    let operation: String
    let cases: [SelectCurrentCase]
}

private struct SelectCurrentCase: Decodable {
    let id: String
    let input: SelectCurrentInput
    let expected: SelectCurrentExpected
}

private struct SelectCurrentInput: Decodable {
    let now: String
    let schedule: [ScheduledTodoVector]
}

private struct SelectCurrentExpected: Decodable {
    let currentTodoId: String?
}

private struct ScheduledTodoVector: Decodable {
    let id: String
    let title: String
    let start: String
    let end: String
    let isMandatory: Bool
    let currentSelectionPriority: Int64

    func domainValue() throws -> ScheduledTodo {
        guard let identifier = UUID(uuidString: id) else {
            throw VectorError.invalidUUID(id)
        }

        return try ScheduledTodo(
            id: identifier,
            title: title,
            start: VectorInstant.parse(start),
            end: VectorInstant.parse(end),
            isMandatory: isMandatory,
            currentSelectionPriority: currentSelectionPriority)
    }
}

private enum SharedVectorLoader {
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

private enum VectorInstant {
    static func parse(_ value: String) throws -> Date {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withColonSeparatorInTimeZone]
        guard let date = formatter.date(from: value) else {
            throw VectorError.invalidInstant(value)
        }

        return date
    }
}

private enum VectorError: Error {
    case invalidUUID(String)
    case invalidInstant(String)
}

import Foundation
import Testing
@testable import AttentionGuardianDomain

@Suite("Shared manage-schedule vectors")
struct ManageScheduleVectorTests {
    @Test
    func sharedVectorsMatchSwiftDomain() throws {
        let envelope: ManageEnvelope = try ManageVectorLoader.load("manage-schedule.json")

        for vector in envelope.cases {
            let schedule = try vector.input.schedule.map { try $0.domainValue() }
            let todoId = try #require(UUID(uuidString: vector.input.todoId))

            switch vector.input.action {
            case "reorder":
                let result = try ScheduleManagement.reorder(
                    schedule,
                    todoId: todoId,
                    requestedIndex: try #require(vector.input.requestedIndex))
                try expectSchedule(result.scheduledTodos, vector: vector)
                #expect(result.actualIndex == vector.expected.actualIndex)
                #expect(result.usedFallbackPosition == vector.expected.usedFallbackPosition)
            case "delete":
                try expectSchedule(
                    try ScheduleManagement.delete(schedule, todoId: todoId),
                    vector: vector)
            case "insertBreak":
                let parsedStart = try ManageInstant.parse(try #require(vector.input.start))
                let result = try ScheduleManagement.insertBreak(
                    into: schedule,
                    id: todoId,
                    start: parsedStart.date,
                    duration: TimeInterval(try #require(vector.input.durationSeconds)),
                    utcOffsetSeconds: parsedStart.utcOffsetSeconds,
                    currentSelectionPriority:
                        try #require(vector.input.currentSelectionPriority))
                try expectSchedule(result.scheduledTodos, vector: vector)
            case "edit":
                do {
                    let result = try ScheduleManagement.edit(
                        schedule,
                        todoId: todoId,
                        title: try #require(vector.input.title),
                        duration: TimeInterval(try #require(vector.input.durationSeconds)),
                        isMandatory: try #require(vector.input.isMandatory as Bool?))
                    #expect(vector.expected.status != "rejected")
                    try expectSchedule(result, vector: vector)
                } catch ScheduleManagementError.breakCannotBeRenamed {
                    #expect(vector.expected.reason == "breakCannotBeRenamed")
                }
            case "editStart":
                let result = try ScheduleManagement.editStart(
                    schedule,
                    todoId: todoId,
                    newStart: try ManageInstant.parse(
                        try #require(vector.input.newStart)).date,
                    conflictResolution: vector.input.conflictResolution.flatMap(
                        StartTimeConflictResolution.init(rawValue:)))
                #expect(
                    result.rejection.rawValue
                        == (vector.expected.reason ?? "none"))
                if vector.expected.status == "applied" {
                    try expectSchedule(result.scheduledTodos, vector: vector)
                }
            default:
                Issue.record("Unsupported action \(vector.input.action)")
            }
        }
    }

    private func expectSchedule(
        _ actual: [ScheduledTodo],
        vector: ManageCase
    ) throws {
        let expected = try #require(vector.expected.schedule)
            .map { try $0.domainValue() }
        #expect(actual == expected, Comment(rawValue: vector.id))
    }
}

private struct ManageEnvelope: Decodable {
    let cases: [ManageCase]
}

private struct ManageCase: Decodable {
    let id: String
    let input: ManageInput
    let expected: ManageExpected
}

private struct ManageInput: Decodable {
    let action: String
    let todoId: String
    let requestedIndex: Int?
    let start: String?
    let newStart: String?
    let durationSeconds: Int?
    let currentSelectionPriority: Int64?
    let title: String?
    let isMandatory: Bool?
    let conflictResolution: String?
    let schedule: [ManageTodoVector]
}

private struct ManageExpected: Decodable {
    let schedule: [ManageTodoVector]?
    let actualIndex: Int?
    let usedFallbackPosition: Bool?
    let status: String?
    let reason: String?
}

private struct ManageTodoVector: Decodable {
    let id: String
    let title: String
    let start: String
    let end: String
    let isMandatory: Bool
    let currentSelectionPriority: Int64

    func domainValue() throws -> ScheduledTodo {
        let parsedStart = try ManageInstant.parse(start)
        return try ScheduledTodo(
            id: try #require(UUID(uuidString: id)),
            title: title,
            start: parsedStart.date,
            end: ManageInstant.parse(end).date,
            utcOffsetSeconds: parsedStart.utcOffsetSeconds,
            isMandatory: isMandatory,
            currentSelectionPriority: currentSelectionPriority)
    }
}

private enum ManageVectorLoader {
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

private enum ManageInstant {
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

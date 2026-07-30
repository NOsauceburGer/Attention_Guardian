import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Shared application lifecycle vectors")
struct ApplicationLifecycleVectorTests {
    @Test
    func sharedVectorsMatchApplicationOrchestration() async throws {
        let cases = try loadCases()
        #expect(cases.count == 8)

        for vector in cases {
            let id = try string(vector, "id")
            let input = try dictionary(vector, "input")
            let expected = try dictionary(vector, "expected")
            switch try string(input, "action") {
            case "planTwice":
                try await runPlanTwice(input, expected, id)
            case "deleteFuture":
                try await runDeleteFuture(input, expected, id)
            case "loadManagement":
                try await runLoadManagement(input, expected, id)
            case "loadOpening":
                try await runLoadOpening(input, expected, id)
            case "deleteScheduled":
                try await runDeleteScheduled(input, expected, id)
            case "replaceSchedule":
                try await runReplaceSchedule(input, expected, id)
            case "addRelativeFuture":
                try await runAddRelativeFuture(input, expected, id)
            default:
                Issue.record("Unsupported Application vector \(id)")
            }
        }
    }

    private func runPlanTwice(
        _ input: [String: Any], _ expected: [String: Any], _ caseId: String
    ) async throws {
        let future = try futureRecord(dictionary(input, "futureTodo"))
        let scheduled = ScheduledTodoRepositoryFake(records: [])
        let futures = FutureTodoRepositoryFake(records: [future])
        futures.markPlannedFailuresRemaining =
            (input["failFirstMarkPlanned"] as? Bool) == true ? 1 : 0
        let start = try instant(string(input, "start"))
        let useCase = PlanFutureTodoUseCase(
            scheduledRepository: scheduled,
            futureRepository: futures,
            clock: FixedClock(now: start.addingTimeInterval(-60)))
        if futures.markPlannedFailuresRemaining > 0 {
            do {
                _ = try await useCase.execute(
                    futureTodoId: future.todo.id,
                    start: start,
                    duration: try number(input, "durationSeconds"),
                    utcOffsetSeconds: 28_800)
                Issue.record("Expected first planned mark failure")
            } catch FutureTodoRepositoryFake.Failure.markPlanned {}
        }
        _ = try await useCase.execute(
            futureTodoId: future.todo.id,
            start: start,
            duration: try number(input, "durationSeconds"),
            utcOffsetSeconds: 28_800)
        let expectedReplaceCount = try integer(expected, "replaceCount")
        let expectedStatus = try string(expected, "futureStatus")
        #expect(
            scheduled.replacements.count == expectedReplaceCount,
            Comment(rawValue: caseId))
        #expect(futures.records[0].status.rawValue == expectedStatus)
    }

    private func runDeleteFuture(
        _ input: [String: Any], _ expected: [String: Any], _ caseId: String
    ) async throws {
        let record = try futureRecord(dictionary(input, "futureTodo"))
        let repository = FutureTodoRepositoryFake(records: [record])
        let result = try await DeleteFutureTodoUseCase(repository: repository)
            .execute(
                futureTodoId: record.todo.id,
                confirmed: input["isConfirmed"] as? Bool == true)
        let expectedStatus = try string(expected, "futureStatus")
        #expect(result?.status.rawValue == expectedStatus,
            Comment(rawValue: caseId))
    }

    private func runLoadManagement(
        _ input: [String: Any], _ expected: [String: Any], _ caseId: String
    ) async throws {
        let now = try instant(string(input, "now"))
        let repository = ScheduledTodoRepositoryFake(
            records: try scheduledRecords(input))
        let active = try await ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: now)).load()
        let expectedIds = try strings(expected, "activeTodoIds")
        #expect(active.map { $0.id.uuidString.lowercased() } ==
            expectedIds, Comment(rawValue: caseId))
        try expectHistory(repository.records, expected)
    }

    private func runLoadOpening(
        _ input: [String: Any], _ expected: [String: Any], _ caseId: String
    ) async throws {
        let nowText = try string(input, "now")
        let future = try dictionaries(input, "futureTodos").map(futureRecord)
        let state = try await LoadOpeningStateUseCase(
            scheduledRepository: ScheduledTodoRepositoryFake(records: []),
            futureRepository: FutureTodoRepositoryFake(records: future),
            clock: FixedClock(
                now: try instant(nowText),
                timeZone: TimeZone(secondsFromGMT: 28_800)!)).execute()
        let expectedIds = try strings(expected, "dueFutureTodoIds")
        #expect(state.dueFutureTodos.map {
            $0.todo.id.uuidString.lowercased()
        } == expectedIds,
        Comment(rawValue: caseId))
    }

    private func runDeleteScheduled(
        _ input: [String: Any], _ expected: [String: Any], _ caseId: String
    ) async throws {
        let repository = ScheduledTodoRepositoryFake(
            records: try scheduledRecords(input))
        _ = try await ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: try instant(string(input, "now"))))
            .delete(todoId: try uuid(string(input, "todoId")))
        let expectedIds = try strings(expected, "activeTodoIds")
        #expect(repository.records.filter {
            $0.status == .active
        }.map { $0.todo.id.uuidString.lowercased() } ==
            expectedIds, Comment(rawValue: caseId))
        try expectHistory(repository.records, expected)
    }

    private func runReplaceSchedule(
        _ input: [String: Any], _ expected: [String: Any], _ caseId: String
    ) async throws {
        let records = try scheduledRecords(input)
        let replacement = try dictionaries(input, "replacementSchedule")
            .map(scheduledTodo)
        let added = try #require(replacement.last)
        let repository = ScheduledTodoRepositoryFake(records: records)
        _ = try await AddScheduledTodoUseCase(
            repository: repository,
            clock: FixedClock(now: try instant(string(input, "now"))))
            .execute(AddScheduledTodoRequest(
                id: added.id, title: added.title, start: added.start,
                duration: added.duration,
                utcOffsetSeconds: added.utcOffsetSeconds,
                isMandatory: added.isMandatory))
        try expectHistory(repository.records, expected)
        #expect(repository.records.contains { $0.todo.id == added.id },
            Comment(rawValue: caseId))
    }

    private func runAddRelativeFuture(
        _ input: [String: Any], _ expected: [String: Any], _ caseId: String
    ) async throws {
        let source = try futureRecord(dictionary(input, "futureTodo"))
        let repository = FutureTodoRepositoryFake()
        let saved = try await AddFutureTodoUseCase(
            repository: repository,
            clock: FixedClock(
                now: try instant(string(input, "now")),
                timeZone: TimeZone(secondsFromGMT: 28_800)!))
            .execute(AddFutureTodoRequest(
                id: source.todo.id,
                title: source.todo.title,
                dateSelection: .daysFromToday(
                    try integer(input, "daysFromToday")),
                isMandatory: source.todo.isMandatory))
        let expectedDate = try string(expected, "savedDate")
        #expect(saved.todo.scheduledDate.description ==
            expectedDate, Comment(rawValue: caseId))
    }

    private func expectHistory(
        _ records: [ScheduledTodoRecord], _ expected: [String: Any]
    ) throws {
        for item in try dictionaries(expected, "history") {
            let id = try uuid(string(item, "id"))
            let record = try #require(records.first { $0.todo.id == id })
            let expectedStatus = try string(item, "status")
            #expect(record.status.rawValue == expectedStatus)
            if let completedAt = item["completedAt"] as? String {
                let expectedInstant = try instant(completedAt)
                #expect(record.completedAt == expectedInstant)
            } else {
                #expect(record.completedAt == nil)
            }
        }
    }
}

private func loadCases() throws -> [[String: Any]] {
    let package = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent().deletingLastPathComponent()
        .deletingLastPathComponent()
    let root = package.deletingLastPathComponent().deletingLastPathComponent()
    let data = try Data(contentsOf: root.appending(
        path: "test-vectors/v1/application-lifecycle.json"))
    let json = try #require(
        JSONSerialization.jsonObject(with: data) as? [String: Any])
    return try #require(json["cases"] as? [[String: Any]])
}

private func futureRecord(_ value: [String: Any]) throws
    -> UnscheduledTodoRecord
{
    UnscheduledTodoRecord(todo: try UnscheduledTodo(
        id: uuid(string(value, "id")),
        title: string(value, "title"),
        scheduledDate: LocalDate(iso8601: string(value, "scheduledDate")),
        isMandatory: value["isMandatory"] as? Bool == true))
}

private func scheduledRecords(_ input: [String: Any]) throws
    -> [ScheduledTodoRecord]
{
    try dictionaries(input, "scheduledTodos").map {
        ScheduledTodoRecord(todo: try scheduledTodo($0))
    }
}

private func scheduledTodo(_ value: [String: Any]) throws -> ScheduledTodo {
    let startText = try string(value, "start")
    return try ScheduledTodo(
        id: uuid(string(value, "id")),
        title: string(value, "title"),
        start: instant(startText),
        end: instant(string(value, "end")),
        utcOffsetSeconds: startText.hasSuffix("+08:00") ? 28_800 : 0,
        isMandatory: value["isMandatory"] as? Bool == true,
        currentSelectionPriority: Int64(try integer(
            value, "currentSelectionPriority")))
}

private func instant(_ value: String) throws -> Date {
    try #require(ISO8601DateFormatter().date(from: value))
}
private func uuid(_ value: String) throws -> UUID {
    try #require(UUID(uuidString: value))
}
private func dictionary(
    _ value: [String: Any], _ key: String
) throws -> [String: Any] {
    try #require(value[key] as? [String: Any])
}
private func dictionaries(
    _ value: [String: Any], _ key: String
) throws -> [[String: Any]] {
    try #require(value[key] as? [[String: Any]])
}
private func string(_ value: [String: Any], _ key: String) throws -> String {
    try #require(value[key] as? String)
}
private func strings(
    _ value: [String: Any], _ key: String
) throws -> [String] {
    try #require(value[key] as? [String])
}
private func integer(_ value: [String: Any], _ key: String) throws -> Int {
    try #require(value[key] as? Int)
}
private func number(_ value: [String: Any], _ key: String) throws
    -> TimeInterval
{
    TimeInterval(try integer(value, key))
}

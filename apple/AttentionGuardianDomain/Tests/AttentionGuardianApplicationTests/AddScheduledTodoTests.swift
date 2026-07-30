import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Add scheduled todo")
struct AddScheduledTodoTests {
    @Test
    func omittedStartUsesClockAndPersistsOneCompleteReplacement() async throws {
        let now = Date(timeIntervalSince1970: 1_775_000_000)
        let repository = ScheduledTodoRepositoryFake(records: [])
        let useCase = AddScheduledTodoUseCase(
            repository: repository,
            clock: FixedClock(now: now))
        let id = try #require(
            UUID(uuidString: "00000000-0000-0000-0000-000000000501"))

        let result = try await useCase.execute(
            AddScheduledTodoRequest(
                id: id,
                title: "第一项",
                start: nil,
                duration: 1_800,
                utcOffsetSeconds: 28_800,
                isMandatory: false))

        let saved = try #require(repository.replacements.only)
        #expect(saved.count == 1)
        #expect(saved[0].todo.id == id)
        #expect(saved[0].todo.start == now)
        #expect(saved[0].todo.end == now.addingTimeInterval(1_800))
        #expect(saved[0].todo.currentSelectionPriority == 0)
        #expect(result.scheduledTodos == saved.map(\.todo))
    }

    @Test
    func completesDueRecordsPreservesHistoryAndUsesNextActivePriority()
        async throws
    {
        let now = Date(timeIntervalSince1970: 1_775_010_000)
        let old = try ScheduledTodo(
            id: try requiredUUID("00000000-0000-0000-0000-000000000511"),
            title: "已到期",
            start: now.addingTimeInterval(-7_200),
            end: now.addingTimeInterval(-3_600),
            currentSelectionPriority: 99)
        let active = try ScheduledTodo(
            id: try requiredUUID("00000000-0000-0000-0000-000000000512"),
            title: "活动项",
            start: now.addingTimeInterval(3_600),
            end: now.addingTimeInterval(5_400),
            currentSelectionPriority: 4)
        let historical = try ScheduledTodo(
            id: try requiredUUID("00000000-0000-0000-0000-000000000513"),
            title: "删除历史",
            start: now.addingTimeInterval(-20_000),
            end: now.addingTimeInterval(-19_000))
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: old),
            ScheduledTodoRecord(todo: active),
            ScheduledTodoRecord(todo: historical, status: .deleted)
        ])
        let useCase = AddScheduledTodoUseCase(
            repository: repository,
            clock: FixedClock(now: now))
        let explicitStart = now.addingTimeInterval(7_200)

        _ = try await useCase.execute(
            AddScheduledTodoRequest(
                id: try requiredUUID(
                    "00000000-0000-0000-0000-000000000514"),
                title: "新增",
                start: explicitStart,
                duration: 1_800,
                utcOffsetSeconds: 0,
                isMandatory: false))

        let saved = try #require(repository.replacements.only)
        let completed = try #require(saved.first { $0.todo.id == old.id })
        let deleted = try #require(saved.first {
            $0.todo.id == historical.id
        })
        let added = try #require(saved.first { $0.todo.title == "新增" })
        #expect(completed.status == .completed)
        #expect(completed.completedAt == now)
        #expect(deleted.status == .deleted)
        #expect(added.todo.start == explicitStart)
        #expect(added.todo.currentSelectionPriority == 5)
    }

    @Test
    func returnsMandatoryConflictAndPersistsTheValidPlan() async throws {
        let start = Date(timeIntervalSince1970: 1_775_020_000)
        let existing = try ScheduledTodo(
            id: try requiredUUID("00000000-0000-0000-0000-000000000521"),
            title: "固定 A",
            start: start,
            end: start.addingTimeInterval(3_600),
            isMandatory: true,
            currentSelectionPriority: 0)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: existing)
        ])
        let useCase = AddScheduledTodoUseCase(
            repository: repository,
            clock: FixedClock(now: start.addingTimeInterval(-3_600)))

        let result = try await useCase.execute(
            AddScheduledTodoRequest(
                id: try requiredUUID(
                    "00000000-0000-0000-0000-000000000522"),
                title: "固定 B",
                start: start.addingTimeInterval(1_800),
                duration: 3_600,
                utcOffsetSeconds: 0,
                isMandatory: true))

        #expect(result.conflicts.count == 1)
        #expect(result.conflicts[0].mandatoryTodo.id == existing.id)
        #expect(repository.replacements.count == 1)
    }

    @Test
    func invalidDurationDoesNotReadOrWrite() async {
        let repository = ScheduledTodoRepositoryFake(records: [])
        let useCase = AddScheduledTodoUseCase(
            repository: repository,
            clock: FixedClock(
                now: Date(timeIntervalSince1970: 1_775_030_000)))

        await #expect(throws: AddScheduledTodoError.invalidDuration) {
            try await useCase.execute(
                AddScheduledTodoRequest(
                    id: UUID(),
                    title: "无效",
                    start: nil,
                    duration: 0,
                    utcOffsetSeconds: 0,
                    isMandatory: false))
        }
        #expect(repository.loadCount == 0)
        #expect(repository.replacements.isEmpty)
    }

    @Test
    func duplicateIdentifierTrialFailureDoesNotWrite() async throws {
        let start = Date(timeIntervalSince1970: 1_775_040_000)
        let id = try requiredUUID(
            "00000000-0000-0000-0000-000000000531")
        let existing = try ScheduledTodo(
            id: id,
            title: "已有",
            start: start,
            end: start.addingTimeInterval(1_800))
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: existing)
        ])
        let useCase = AddScheduledTodoUseCase(
            repository: repository,
            clock: FixedClock(now: start.addingTimeInterval(-3_600)))

        await #expect(throws: ScheduleTrialError.duplicateIdentifier) {
            try await useCase.execute(
                AddScheduledTodoRequest(
                    id: id,
                    title: "重复",
                    start: start.addingTimeInterval(3_600),
                    duration: 1_800,
                    utcOffsetSeconds: 0,
                    isMandatory: false))
        }
        #expect(repository.replacements.isEmpty)
    }

    @Test
    func returnsRolloverWhenDomainMovesAnExistingTodoIntoNextDay()
        async throws
    {
        let dayStart = Date(timeIntervalSince1970: 1_774_828_800)
        let existing = try ScheduledTodo(
            id: try requiredUUID("00000000-0000-0000-0000-000000000541"),
            title: "深夜任务",
            start: dayStart.addingTimeInterval(23 * 3_600 + 30 * 60),
            end: dayStart.addingTimeInterval(24 * 3_600),
            utcOffsetSeconds: 0,
            currentSelectionPriority: 0)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: existing)
        ])
        let useCase = AddScheduledTodoUseCase(
            repository: repository,
            clock: FixedClock(now: dayStart))

        let result = try await useCase.execute(
            AddScheduledTodoRequest(
                id: try requiredUUID(
                    "00000000-0000-0000-0000-000000000542"),
                title: "插入任务",
                start: dayStart.addingTimeInterval(23 * 3_600 + 45 * 60),
                duration: 3_600,
                utcOffsetSeconds: 0,
                isMandatory: false))

        #expect(result.hasRolloverToNextDay)
        #expect(repository.replacements.count == 1)
    }
}

private func requiredUUID(_ value: String) throws -> UUID {
    try #require(UUID(uuidString: value))
}

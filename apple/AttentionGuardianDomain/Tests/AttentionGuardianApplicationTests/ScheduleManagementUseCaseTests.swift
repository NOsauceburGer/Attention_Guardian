import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Schedule management application")
struct ScheduleManagementUseCaseTests {
    @Test
    func futureManagementLoadsOnlyWhenExplicitlyRequestedAndSortsByDate() async throws {
        let later = try futureItem(
            "00000000-0000-0000-0000-000000000892",
            "稍后",
            "2026-08-02")
        let earlier = try futureItem(
            "00000000-0000-0000-0000-000000000891",
            "较早",
            "2026-08-01")
        let repository = FutureTodoRepositoryFake(records: [later, earlier])
        let useCase = FutureTodoManagementUseCase(repository: repository)

        #expect(repository.loadCount == 0)

        let loaded = try await useCase.load()

        #expect(repository.loadCount == 1)
        #expect(loaded.map(\.todo.title) == ["较早", "稍后"])
    }

    @Test
    func futureManagementDeleteRequiresConfirmation() async throws {
        let record = try futureItem(
            "00000000-0000-0000-0000-000000000893",
            "以后处理",
            "2026-08-03")
        let repository = FutureTodoRepositoryFake(records: [record])
        let useCase = FutureTodoManagementUseCase(repository: repository)

        let cancelled = try await useCase.delete(
            todoId: record.todo.id,
            confirmed: false)
        #expect(cancelled.only?.status == .active)
        #expect(repository.markDeletedCount == 0)

        let deleted = try await useCase.delete(
            todoId: record.todo.id,
            confirmed: true)
        #expect(deleted.isEmpty)
        #expect(repository.markDeletedCount == 1)
    }

    @Test
    func reorderCallsDomainAndPersistsOneWholeReplacement() async throws {
        let now = Date(timeIntervalSince1970: 1_775_300_000)
        let first = try item("00000000-0000-0000-0000-000000000801",
            "第一", now, 1_800)
        let second = try item("00000000-0000-0000-0000-000000000802",
            "第二", first.end, 1_800)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: first),
            ScheduledTodoRecord(todo: second)
        ])
        let service = ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: now.addingTimeInterval(-60)))

        let result = try await service.reorder(
            todoId: second.id,
            requestedIndex: 0)

        #expect(result.scheduledTodos.map(\.id) == [second.id, first.id])
        #expect(repository.replacements.count == 1)
        #expect(repository.records.filter {
            $0.status == .active
        }.map(\.todo.id) == [second.id, first.id])
    }

    @Test
    func reorderPreviewUsesDomainForEveryIndexWithoutWriting() async throws {
        let now = Date(timeIntervalSince1970: 1_775_305_000)
        let first = try item(
            "00000000-0000-0000-0000-000000000805",
            "第一", now, 1_800)
        let blocker = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000806")),
            title: "不可移动",
            start: first.end,
            end: first.end.addingTimeInterval(1_800),
            isMandatory: true)
        let moving = try item(
            "00000000-0000-0000-0000-000000000807",
            "移动", blocker.end, 1_800)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: first),
            ScheduledTodoRecord(todo: blocker),
            ScheduledTodoRecord(todo: moving)
        ])
        let service = ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: now.addingTimeInterval(-60)))

        let previews = try await service.previewReorder(todoId: moving.id)

        #expect(previews.map(\.requestedIndex) == [0, 1, 2])
        #expect(previews[1].actualIndex == 2)
        #expect(previews[1].usedFallbackPosition)
        #expect(repository.replacements.isEmpty)
    }

    @Test
    func mandatoryReorderPreviewIncludesOnlyItsContinuousGroup() async throws {
        let now = Date(timeIntervalSince1970: 1_775_307_000)
        let first = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000808")),
            title: "固定一",
            start: now,
            end: now.addingTimeInterval(1_800),
            isMandatory: true)
        let second = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000809")),
            title: "固定二",
            start: first.end,
            end: first.end.addingTimeInterval(1_800),
            isMandatory: true)
        let ordinary = try item(
            "00000000-0000-0000-0000-000000000810",
            "普通", second.end, 1_800)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: first),
            ScheduledTodoRecord(todo: second),
            ScheduledTodoRecord(todo: ordinary)
        ])
        let service = ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: now.addingTimeInterval(-60)))

        let previews = try await service.previewReorder(todoId: first.id)

        #expect(previews.map(\.requestedIndex) == [0, 1])
        #expect(previews.map(\.actualIndex) == [0, 1])
        #expect(repository.replacements.isEmpty)
    }

    @Test
    func rejectedStartEditDoesNotWrite() async throws {
        let now = Date(timeIntervalSince1970: 1_775_310_000)
        let moving = try item("00000000-0000-0000-0000-000000000811",
            "移动", now, 1_800)
        let fixed = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000812")),
            title: "固定",
            start: moving.end,
            end: moving.end.addingTimeInterval(1_800),
            isMandatory: true)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: moving),
            ScheduledTodoRecord(todo: fixed)
        ])
        let service = ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: now.addingTimeInterval(-60)))

        let result = try await service.editStart(
            todoId: moving.id,
            newStart: fixed.start.addingTimeInterval(60),
            conflictResolution: nil)

        #expect(result.rejection == .mandatoryTodoOccupiesNewStart)
        #expect(repository.replacements.isEmpty)
    }

    @Test
    func combinedEditWaitsForConflictChoiceBeforeWriting() async throws {
        let now = Date(timeIntervalSince1970: 1_775_315_000)
        let existing = try item(
            "00000000-0000-0000-0000-000000000815",
            "原事件", now, 1_800)
        let moving = try item(
            "00000000-0000-0000-0000-000000000816",
            "待修改", existing.end, 1_800)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: existing),
            ScheduledTodoRecord(todo: moving)
        ])
        let service = ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: now.addingTimeInterval(-60)))
        let newStart = now.addingTimeInterval(600)

        let waiting = try await service.edit(
            todoId: moving.id,
            title: "已修改",
            duration: 2_400,
            isMandatory: true,
            newStart: newStart,
            conflictResolution: nil)

        #expect(waiting.rejection == .conflictResolutionRequired)
        #expect(waiting.conflictingTodoId == existing.id)
        #expect(repository.replacements.isEmpty)

        let saved = try await service.edit(
            todoId: moving.id,
            title: "已修改",
            duration: 2_400,
            isMandatory: true,
            newStart: newStart,
            conflictResolution: .moveExistingAfterEdited)

        #expect(saved.rejection == .none)
        #expect(repository.replacements.count == 1)
        #expect(repository.records.first {
            $0.todo.id == moving.id
        }?.todo.title == "已修改")
        #expect(repository.records.first {
            $0.todo.id == moving.id
        }?.todo.start == newStart)
        #expect(repository.records.first {
            $0.todo.id == moving.id
        }?.todo.isMandatory == true)
    }

    @Test
    func loadEditInsertBreakAndDeletePreserveLifecycleRecords() async throws {
        let now = Date(timeIntervalSince1970: 1_775_320_000)
        let original = try item(
            "00000000-0000-0000-0000-000000000821",
            "原任务", now, 1_800)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: original)
        ])
        let service = ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: now.addingTimeInterval(-60)))

        #expect(try await service.load().map(\.id) == [original.id])
        let edited = try await service.edit(
            todoId: original.id,
            title: "已编辑",
            duration: 2_400,
            isMandatory: false)
        #expect(edited[0].title == "已编辑")
        let breakId = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000822"))
        _ = try await service.insertBreak(
            id: breakId,
            start: edited[0].end,
            duration: 1_200,
            utcOffsetSeconds: 0)
        _ = try await service.delete(todoId: original.id)

        #expect(repository.records.first {
            $0.todo.id == original.id
        }?.status == .deleted)
        #expect(repository.records.first {
            $0.todo.id == breakId
        }?.status == .active)
    }

    @Test
    func breakTemplateCanInsertBeforeATodoAndThenAppendAgain() async throws {
        let now = Date(timeIntervalSince1970: 1_775_325_000)
        let first = try item(
            "00000000-0000-0000-0000-000000000823",
            "第一", now, 1_800)
        let second = try item(
            "00000000-0000-0000-0000-000000000824",
            "第二", first.end, 1_800)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: first),
            ScheduledTodoRecord(todo: second)
        ])
        let service = ScheduleManagementUseCase(
            repository: repository,
            clock: FixedClock(now: now.addingTimeInterval(-60)))
        let firstBreakId = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000825"))
        let secondBreakId = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000826"))

        let inserted = try await service.insertBreak(
            id: firstBreakId,
            beforeTodoId: second.id,
            duration: 1_200)
        #expect(inserted.scheduledTodos.map(\.id) == [
            first.id, firstBreakId, second.id
        ])

        let appended = try await service.insertBreak(
            id: secondBreakId,
            beforeTodoId: nil,
            duration: 1_200)
        #expect(appended.scheduledTodos.map(\.id) == [
            first.id, firstBreakId, second.id, secondBreakId
        ])
        #expect(appended.scheduledTodos.filter {
            $0.title == ScheduleManagement.breakTitle
        }.map(\.duration) == [1_200, 1_200])
        #expect(repository.replacements.count == 2)
    }

    private func item(
        _ id: String, _ title: String, _ start: Date, _ duration: TimeInterval
    ) throws -> ScheduledTodo {
        try ScheduledTodo(
            id: #require(UUID(uuidString: id)),
            title: title,
            start: start,
            end: start.addingTimeInterval(duration))
    }

    private func futureItem(
        _ id: String,
        _ title: String,
        _ date: String
    ) throws -> UnscheduledTodoRecord {
        UnscheduledTodoRecord(todo: try UnscheduledTodo(
            id: #require(UUID(uuidString: id)),
            title: title,
            scheduledDate: LocalDate(iso8601: date)))
    }
}

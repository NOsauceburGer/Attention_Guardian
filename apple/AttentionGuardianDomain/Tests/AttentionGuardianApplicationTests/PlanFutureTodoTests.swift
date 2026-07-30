import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Plan future todo")
struct PlanFutureTodoTests {
    private enum Failure: Error {
        case replaceSchedule
    }

    @Test
    func retryAfterFirstMarkFailureDoesNotReplaceScheduleTwice() async throws {
        let id = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000901"))
        let source = UnscheduledTodoRecord(todo: try UnscheduledTodo(
            id: id,
            title: "需要规划",
            scheduledDate: LocalDate(iso8601: "2026-07-30")))
        let scheduledRepository = ScheduledTodoRepositoryFake(records: [])
        let futureRepository = FutureTodoRepositoryFake(records: [source])
        futureRepository.markPlannedFailuresRemaining = 1
        let useCase = PlanFutureTodoUseCase(
            scheduledRepository: scheduledRepository,
            futureRepository: futureRepository,
            clock: FixedClock(
                now: Date(timeIntervalSince1970: 1_775_390_000)))
        let start = Date(timeIntervalSince1970: 1_775_400_000)

        await #expect(throws: FutureTodoRepositoryFake.Failure.markPlanned) {
            try await useCase.execute(
                futureTodoId: id,
                start: start,
                duration: 1_800,
                utcOffsetSeconds: 28_800)
        }

        let result = try await useCase.execute(
            futureTodoId: id,
            start: start,
            duration: 1_800,
            utcOffsetSeconds: 28_800)

        #expect(scheduledRepository.replacements.count == 1)
        #expect(result.scheduledTodo.id == id)
        #expect(!result.didWriteSchedule)
        #expect(futureRepository.records.first?.status == .planned)
        #expect(futureRepository.operationLog == [
            "findActive", "markPlanned", "markPlanned"
        ])
    }

    @Test
    func scheduleWriteFailureDoesNotMarkFutureTodoPlanned() async throws {
        let id = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000911"))
        let source = UnscheduledTodoRecord(todo: try UnscheduledTodo(
            id: id,
            title: "保持活动",
            scheduledDate: LocalDate(iso8601: "2026-07-30")))
        let scheduledRepository = ScheduledTodoRepositoryFake(records: [])
        scheduledRepository.replacementError = Failure.replaceSchedule
        let futureRepository = FutureTodoRepositoryFake(records: [source])
        let useCase = PlanFutureTodoUseCase(
            scheduledRepository: scheduledRepository,
            futureRepository: futureRepository,
            clock: FixedClock(
                now: Date(timeIntervalSince1970: 1_775_390_000)))

        await #expect(throws: Failure.replaceSchedule) {
            try await useCase.execute(
                futureTodoId: id,
                start: Date(timeIntervalSince1970: 1_775_400_000),
                duration: 1_800,
                utcOffsetSeconds: 28_800)
        }

        #expect(futureRepository.records.first?.status == .active)
        #expect(futureRepository.operationLog == ["findActive"])
    }
}

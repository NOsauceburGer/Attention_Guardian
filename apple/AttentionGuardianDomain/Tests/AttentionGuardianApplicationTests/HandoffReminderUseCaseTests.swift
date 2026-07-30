import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Handoff reminder application")
struct HandoffReminderUseCaseTests {
    @Test
    func sameCurrentTodoProducesOnlyOnePendingReminderPerProcess()
        async throws
    {
        let start = Date(timeIntervalSince1970: 1_775_500_000)
        let current = try item(
            "00000000-0000-0000-0000-000000001001",
            "当前", start, 1_800)
        let next = try item(
            "00000000-0000-0000-0000-000000001002",
            "下一项", current.end, 1_800)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: current),
            ScheduledTodoRecord(todo: next)
        ])
        let state = ReminderDeduplicationState()
        let service = HandoffReminderUseCase(
            repository: repository,
            clock: FixedClock(
                now: current.end.addingTimeInterval(-60)),
            deduplicationState: state)

        let first = try await service.evaluate()
        let second = try await service.evaluate()

        #expect(first?.currentTodo.id == current.id)
        #expect(first?.nextTodo.id == next.id)
        #expect(second == nil)
    }

    @Test
    func ineligibleEvaluationDoesNotConsumeLaterEligibleReminder()
        async throws
    {
        let start = Date(timeIntervalSince1970: 1_775_510_000)
        let current = try item(
            "00000000-0000-0000-0000-000000001011",
            "当前", start, 1_800)
        let next = try item(
            "00000000-0000-0000-0000-000000001012",
            "下一项", current.end, 1_800)
        let repository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: current),
            ScheduledTodoRecord(todo: next)
        ])
        let state = ReminderDeduplicationState()
        let early = HandoffReminderUseCase(
            repository: repository,
            clock: FixedClock(now: start.addingTimeInterval(60)),
            deduplicationState: state)
        let eligible = HandoffReminderUseCase(
            repository: repository,
            clock: FixedClock(
                now: current.end.addingTimeInterval(-60)),
            deduplicationState: state)

        #expect(try await early.evaluate() == nil)
        #expect(try await eligible.evaluate() != nil)
    }

    private func item(
        _ id: String,
        _ title: String,
        _ start: Date,
        _ duration: TimeInterval
    ) throws -> ScheduledTodo {
        try ScheduledTodo(
            id: #require(UUID(uuidString: id)),
            title: title,
            start: start,
            end: start.addingTimeInterval(duration))
    }
}

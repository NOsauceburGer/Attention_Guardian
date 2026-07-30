import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Load opening state")
struct LoadOpeningStateTests {
    @Test
    func completesDueSelectsCurrentAndLoadsDueFutureTodos() async throws {
        let now = try #require(
            ISO8601DateFormatter().date(from: "2026-04-05T08:00:00Z"))
        let due = try scheduled("00000000-0000-0000-0000-000000000711",
            start: now.addingTimeInterval(-3_600), end: now)
        let current = try scheduled("00000000-0000-0000-0000-000000000712",
            start: now, end: now.addingTimeInterval(3_600), priority: 2)
        let scheduledRepository = ScheduledTodoRepositoryFake(records: [
            ScheduledTodoRecord(todo: due), ScheduledTodoRecord(todo: current)
        ])
        let futureRepository = FutureTodoRepositoryFake(records: [
            try future("00000000-0000-0000-0000-000000000713", "2026-04-04"),
            try future("00000000-0000-0000-0000-000000000714", "2026-04-05"),
            try future("00000000-0000-0000-0000-000000000715", "2026-04-06")
        ])
        let useCase = LoadOpeningStateUseCase(
            scheduledRepository: scheduledRepository,
            futureRepository: futureRepository,
            clock: FixedClock(
                now: now,
                timeZone: TimeZone(secondsFromGMT: 0)!))

        let state = try await useCase.execute()

        #expect(state.currentTodo?.id == current.id)
        #expect(state.dueFutureTodos.map(\.todo.id) == Array(
            futureRepository.records.prefix(2)).map(\.todo.id))
        #expect(scheduledRepository.replacements.count == 1)
        #expect(scheduledRepository.records.first {
            $0.todo.id == due.id
        }?.status == .completed)
        #expect(futureRepository.loadCount == 1)
    }

    private func scheduled(
        _ id: String, start: Date, end: Date, priority: Int64 = 0
    ) throws -> ScheduledTodo {
        try ScheduledTodo(
            id: #require(UUID(uuidString: id)), title: id,
            start: start, end: end, currentSelectionPriority: priority)
    }

    private func future(_ id: String, _ date: String) throws
        -> UnscheduledTodoRecord
    {
        UnscheduledTodoRecord(todo: try UnscheduledTodo(
            id: #require(UUID(uuidString: id)), title: id,
            scheduledDate: LocalDate(iso8601: date)))
    }
}

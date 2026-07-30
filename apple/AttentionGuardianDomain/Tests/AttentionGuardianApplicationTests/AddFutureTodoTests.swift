import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Add future todo")
struct AddFutureTodoTests {
    @Test
    func exactDateSavesOneActiveFutureTodo() async throws {
        let repository = FutureTodoRepositoryFake()
        let useCase = AddFutureTodoUseCase(
            repository: repository,
            clock: FixedClock(
                now: Date(timeIntervalSince1970: 1_775_100_000)))
        let id = try requiredFutureUUID(
            "00000000-0000-0000-0000-000000000601")
        let date = try LocalDate(iso8601: "2026-08-05")

        let saved = try await useCase.execute(
            AddFutureTodoRequest(
                id: id,
                title: "预约体检",
                dateSelection: .exact(date),
                isMandatory: false))

        #expect(repository.savedRecords == [saved])
        #expect(saved.todo.id == id)
        #expect(saved.todo.scheduledDate == date)
        #expect(saved.status == .active)
    }

    @Test
    func relativeDateUsesClockLocalTodayInsteadOfUTCDate() async throws {
        let now = try #require(
            ISO8601DateFormatter().date(from: "2026-07-29T16:30:00Z"))
        let timeZone = try #require(TimeZone(secondsFromGMT: 8 * 3_600))
        let repository = FutureTodoRepositoryFake()
        let useCase = AddFutureTodoUseCase(
            repository: repository,
            clock: FixedClock(now: now, timeZone: timeZone))

        let saved = try await useCase.execute(
            AddFutureTodoRequest(
                id: try requiredFutureUUID(
                    "00000000-0000-0000-0000-000000000602"),
                title: "两天后事项",
                dateSelection: .daysFromToday(2),
                isMandatory: true))

        #expect(saved.todo.scheduledDate.description == "2026-08-01")
        #expect(saved.todo.isMandatory)
        #expect(repository.savedRecords.count == 1)
    }

    @Test
    func invalidRelativeDaysDoesNotWrite() async {
        let repository = FutureTodoRepositoryFake()
        let useCase = AddFutureTodoUseCase(
            repository: repository,
            clock: FixedClock(
                now: Date(timeIntervalSince1970: 1_775_100_000)))

        await #expect(throws: TodoLifecycleError.invalidRelativeDays) {
            try await useCase.execute(
                AddFutureTodoRequest(
                    id: UUID(),
                    title: "无效日期",
                    dateSelection: .daysFromToday(0),
                    isMandatory: false))
        }
        #expect(repository.savedRecords.isEmpty)
    }
}

private func requiredFutureUUID(_ value: String) throws -> UUID {
    try #require(UUID(uuidString: value))
}

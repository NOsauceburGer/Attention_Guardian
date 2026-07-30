import Foundation
import Testing
import AttentionGuardianDomain
@testable import AttentionGuardianApplication

@Suite("Delete future todo")
struct DeleteFutureTodoTests {
    @Test(arguments: [false, true])
    func writesOnlyWhenConfirmed(confirmed: Bool) async throws {
        let id = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000001101"))
        let record = UnscheduledTodoRecord(todo: try UnscheduledTodo(
            id: id, title: "未来事项",
            scheduledDate: LocalDate(iso8601: "2026-08-01")))
        let repository = FutureTodoRepositoryFake(records: [record])
        let useCase = DeleteFutureTodoUseCase(repository: repository)

        let result = try await useCase.execute(
            futureTodoId: id,
            confirmed: confirmed)

        #expect(result?.status == (confirmed ? .deleted : .active))
        #expect(repository.markDeletedCount == (confirmed ? 1 : 0))
    }
}

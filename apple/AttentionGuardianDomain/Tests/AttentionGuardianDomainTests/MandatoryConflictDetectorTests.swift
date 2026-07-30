import Foundation
import Testing
@testable import AttentionGuardianDomain

@Suite("Mandatory conflict detector")
struct MandatoryConflictDetectorTests {
    @Test
    func returnsOnlyOverlappingUnfinishedMandatoryPairs() throws {
        let now = Date(timeIntervalSince1970: 1_775_200_000)
        let first = try todo("00000000-0000-0000-0000-000000000701",
            start: now, end: now.addingTimeInterval(3_600), mandatory: true)
        let second = try todo("00000000-0000-0000-0000-000000000702",
            start: now.addingTimeInterval(1_800),
            end: now.addingTimeInterval(5_400), mandatory: true)
        let touching = try todo("00000000-0000-0000-0000-000000000703",
            start: second.end, end: second.end.addingTimeInterval(900),
            mandatory: true)
        let normal = try todo("00000000-0000-0000-0000-000000000704",
            start: now, end: now.addingTimeInterval(3_600), mandatory: false)

        let conflicts = MandatoryConflictDetector.detect(
            in: [normal, touching, second, first],
            endingAfter: now)

        #expect(conflicts.count == 1)
        #expect(conflicts[0].first.id == first.id)
        #expect(conflicts[0].second.id == second.id)
    }

    private func todo(
        _ id: String, start: Date, end: Date, mandatory: Bool
    ) throws -> ScheduledTodo {
        try ScheduledTodo(
            id: #require(UUID(uuidString: id)),
            title: id,
            start: start,
            end: end,
            isMandatory: mandatory)
    }
}

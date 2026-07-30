import Foundation
import Testing
@testable import AttentionGuardianDomain

@Suite("Mandatory schedule groups")
struct MandatoryScheduleGroupTests {
    @Test("overlapping and touching mandatory events form public continuous groups")
    func detectsContinuousGroups() throws {
        let start = Date(timeIntervalSince1970: 1_775_500_000)
        let first = try todo("00000000-0000-0000-0000-000000000951",
            start: start, duration: 1_800, mandatory: true)
        let touching = try todo("00000000-0000-0000-0000-000000000952",
            start: first.end, duration: 1_800, mandatory: true)
        let overlapping = try todo("00000000-0000-0000-0000-000000000953",
            start: touching.start.addingTimeInterval(600),
            duration: 1_800, mandatory: true)
        let isolated = try todo("00000000-0000-0000-0000-000000000954",
            start: overlapping.end.addingTimeInterval(60),
            duration: 1_800, mandatory: true)
        let ordinary = try todo("00000000-0000-0000-0000-000000000955",
            start: isolated.end, duration: 1_800, mandatory: false)

        let groups = ScheduleManagement.mandatoryContinuousGroups(
            [ordinary, isolated, overlapping, touching, first])

        #expect(groups.map { $0.map(\.id) } == [[
            first.id, touching.id, overlapping.id
        ]])
    }

    private func todo(
        _ id: String,
        start: Date,
        duration: TimeInterval,
        mandatory: Bool
    ) throws -> ScheduledTodo {
        try ScheduledTodo(
            id: #require(UUID(uuidString: id)),
            title: id,
            start: start,
            end: start.addingTimeInterval(duration),
            isMandatory: mandatory)
    }
}

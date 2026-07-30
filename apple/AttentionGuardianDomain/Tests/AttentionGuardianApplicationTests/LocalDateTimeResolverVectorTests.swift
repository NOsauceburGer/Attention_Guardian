import Foundation
import Testing
@testable import AttentionGuardianApplication

@Suite("Shared local date-time vectors")
struct LocalDateTimeResolverVectorTests {
    @Test
    func sharedVectorsMatchApplicationResolver() throws {
        let envelope: LocalTimeEnvelope = try loadLocalTimeVector()

        for vector in envelope.cases {
            let actual = try LocalDateTimeResolver.resolve(
                vector.input.localDateTime,
                timeZoneId: vector.input.timeZoneId)
            switch (vector.expected.status, actual) {
            case ("resolved", .resolved(let instant, let offsetSeconds)):
                let expectedText = try #require(vector.expected.instant)
                let expected = try #require(
                    ISO8601DateFormatter().date(
                        from: expectedText))
                #expect(instant == expected, Comment(rawValue: vector.id))
                #expect(offsetSeconds == -18_000, Comment(rawValue: vector.id))
            case ("invalid", .invalid), ("ambiguous", .ambiguous):
                break
            default:
                Issue.record(
                    "Unexpected resolution \(actual) for \(vector.id)")
            }
        }
    }
}

private struct LocalTimeEnvelope: Decodable {
    let cases: [LocalTimeCase]
}
private struct LocalTimeCase: Decodable {
    let id: String
    let input: LocalTimeInput
    let expected: LocalTimeExpected
}
private struct LocalTimeInput: Decodable {
    let localDateTime: String
    let timeZoneId: String
}
private struct LocalTimeExpected: Decodable {
    let status: String
    let instant: String?
}

private func loadLocalTimeVector<Value: Decodable>() throws -> Value {
    let packageDirectory = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
    let repositoryRoot = packageDirectory
        .deletingLastPathComponent()
        .deletingLastPathComponent()
    return try JSONDecoder().decode(
        Value.self,
        from: Data(contentsOf: repositoryRoot
            .appending(path: "test-vectors/v1/resolve-local-date-time.json")))
}

import Foundation
import Testing
import AttentionGuardianApplication
import AttentionGuardianDomain
import AttentionGuardianInfrastructure

@Suite("Apple handoff notifications")
struct AppleHandoffNotificationSenderTests {
    @Test("authorized sender includes current and next titles")
    func sendsExpectedContent() async throws {
        let center = NotificationCenterFake(status: .authorized)
        let sender = AppleHandoffNotificationSender(center: center)
        let reminder = try makeReminder()

        let result = try await sender.send(reminder)

        #expect(result == .sent)
        let requests = await center.requests
        #expect(requests == [AppleNotificationRequest(
            identifier: "handoff-\(reminder.currentTodo.id.uuidString.lowercased())",
            title: "即将交接",
            body: "当前：专注写作\n下一项：团队会议"
        )])
    }

    @Test("undecided permission does not prompt or enqueue implicitly")
    func undecidedDoesNotPrompt() async throws {
        let center = NotificationCenterFake(status: .notDetermined)
        let sender = AppleHandoffNotificationSender(center: center)

        let result = try await sender.send(makeReminder())

        #expect(result == .authorizationRequired)
        #expect(await center.requestAuthorizationCalls == 0)
        #expect(await center.requests.isEmpty)
    }

    @Test("denied permission is explicit and does not enqueue")
    func deniedDoesNotSend() async throws {
        let center = NotificationCenterFake(status: .denied)
        let sender = AppleHandoffNotificationSender(center: center)

        let result = try await sender.send(makeReminder())

        #expect(result == .denied)
        #expect(await center.requests.isEmpty)
    }

    @Test("permission request is an explicit user-triggered operation")
    func requestsPermissionExplicitly() async throws {
        let center = NotificationCenterFake(
            status: .notDetermined,
            authorizationResult: true)
        let sender = AppleHandoffNotificationSender(center: center)

        #expect(try await sender.requestAuthorization())
        #expect(await center.requestAuthorizationCalls == 1)
    }
}

private actor NotificationCenterFake: AppleNotificationCenter {
    private let status: AppleNotificationAuthorization
    private let authorizationResult: Bool
    private(set) var requests: [AppleNotificationRequest] = []
    private(set) var requestAuthorizationCalls = 0

    init(
        status: AppleNotificationAuthorization,
        authorizationResult: Bool = false
    ) {
        self.status = status
        self.authorizationResult = authorizationResult
    }

    func authorizationStatus() async -> AppleNotificationAuthorization {
        status
    }

    func requestAuthorization() async throws -> Bool {
        requestAuthorizationCalls += 1
        return authorizationResult
    }

    func add(_ request: AppleNotificationRequest) async throws {
        requests.append(request)
    }
}

private func makeReminder() throws -> PendingHandoffReminder {
    let current = try ScheduledTodo(
        id: UUID(uuidString: "00000000-0000-0000-0000-000000000501")!,
        title: "专注写作",
        start: Date(timeIntervalSince1970: 1_800_000_000),
        end: Date(timeIntervalSince1970: 1_800_003_600))
    let next = try ScheduledTodo(
        id: UUID(uuidString: "00000000-0000-0000-0000-000000000502")!,
        title: "团队会议",
        start: current.end,
        end: current.end.addingTimeInterval(1_800))
    return PendingHandoffReminder(
        currentTodo: current,
        nextTodo: next,
        reminderAt: current.end.addingTimeInterval(-300))
}

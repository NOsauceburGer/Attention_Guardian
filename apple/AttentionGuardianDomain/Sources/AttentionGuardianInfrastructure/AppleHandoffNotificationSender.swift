import Foundation
import AttentionGuardianApplication

public enum HandoffNotificationSendResult: Equatable, Sendable {
    case sent
    case authorizationRequired
    case denied
}

public struct AppleHandoffNotificationSender: Sendable {
    private let center: any AppleNotificationCenter

    public init(center: any AppleNotificationCenter) {
        self.center = center
    }

    public func authorizationStatus() async -> AppleNotificationAuthorization {
        await center.authorizationStatus()
    }

    public func requestAuthorization() async throws -> Bool {
        try await center.requestAuthorization()
    }

    public func send(
        _ reminder: PendingHandoffReminder
    ) async throws -> HandoffNotificationSendResult {
        switch await center.authorizationStatus() {
        case .notDetermined:
            return .authorizationRequired
        case .denied:
            return .denied
        case .authorized, .provisional:
            try await center.add(AppleNotificationRequest(
                identifier: "handoff-\(reminder.currentTodo.id.uuidString.lowercased())",
                title: "即将交接",
                body: """
                    当前：\(reminder.currentTodo.title)
                    下一项：\(reminder.nextTodo.title)
                    """))
            return .sent
        }
    }
}

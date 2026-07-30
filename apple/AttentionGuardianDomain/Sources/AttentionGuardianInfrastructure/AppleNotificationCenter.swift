import Foundation
import UserNotifications

public enum AppleNotificationAuthorization: Equatable, Sendable {
    case notDetermined
    case denied
    case authorized
    case provisional
}

public struct AppleNotificationRequest: Equatable, Sendable {
    public let identifier: String
    public let title: String
    public let body: String

    public init(identifier: String, title: String, body: String) {
        self.identifier = identifier
        self.title = title
        self.body = body
    }
}

public protocol AppleNotificationCenter: Sendable {
    func authorizationStatus() async -> AppleNotificationAuthorization
    func requestAuthorization() async throws -> Bool
    func add(_ request: AppleNotificationRequest) async throws
}

@available(macOS 10.15, iOS 13.0, *)
public actor UserNotificationCenterAdapter: AppleNotificationCenter {
    private let center: UNUserNotificationCenter

    public init(center: UNUserNotificationCenter = .current()) {
        self.center = center
    }

    public func authorizationStatus() async -> AppleNotificationAuthorization {
        await withCheckedContinuation { continuation in
            center.getNotificationSettings { settings in
                continuation.resume(returning: Self.map(settings.authorizationStatus))
            }
        }
    }

    public func requestAuthorization() async throws -> Bool {
        try await withCheckedThrowingContinuation {
            (continuation: CheckedContinuation<Bool, any Error>) in
            center.requestAuthorization(options: [.alert, .sound]) {
                granted,
                error in
                if let error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume(returning: granted)
                }
            }
        }
    }

    public func add(_ request: AppleNotificationRequest) async throws {
        let content = UNMutableNotificationContent()
        content.title = request.title
        content.body = request.body
        content.sound = .default
        let notification = UNNotificationRequest(
            identifier: request.identifier,
            content: content,
            trigger: nil)
        try await withCheckedThrowingContinuation {
            (continuation: CheckedContinuation<Void, any Error>) in
            center.add(notification) { error in
                if let error {
                    continuation.resume(throwing: error)
                } else {
                    continuation.resume()
                }
            }
        }
    }

    private static func map(
        _ status: UNAuthorizationStatus
    ) -> AppleNotificationAuthorization {
        switch status {
        case .notDetermined:
            .notDetermined
        case .denied:
            .denied
        case .authorized:
            .authorized
        case .provisional:
            .provisional
        @unknown default:
            status.rawValue == 4 ? .provisional : .denied
        }
    }
}

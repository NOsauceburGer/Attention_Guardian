import Foundation
import AttentionGuardianDomain

public protocol Clock: Sendable {
    var now: Date { get }
    var timeZone: TimeZone { get }
}

extension Clock {
    func localDate() throws -> LocalDate {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = timeZone
        let components = calendar.dateComponents([.year, .month, .day], from: now)
        guard let year = components.year,
              let month = components.month,
              let day = components.day else {
            throw TodoLifecycleError.invalidLocalDate
        }
        return try LocalDate(year: year, month: month, day: day)
    }
}

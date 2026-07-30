import Foundation

public enum LocalDateTimeResolution: Equatable, Sendable {
    case resolved(instant: Date, utcOffsetSeconds: Int)
    case invalid
    case ambiguous
}

public enum LocalDateTimeResolverError: Error, Equatable {
    case invalidFormat
    case unknownTimeZone
}

public enum LocalDateTimeResolver {
    public static func resolve(
        _ localDateTime: String,
        timeZoneId: String
    ) throws -> LocalDateTimeResolution {
        guard let timeZone = TimeZone(identifier: timeZoneId) else {
            throw LocalDateTimeResolverError.unknownTimeZone
        }
        let naiveFormatter = formatter(
            timeZone: TimeZone(secondsFromGMT: 0)!)
        guard let naive = naiveFormatter.date(from: localDateTime),
              naiveFormatter.string(from: naive) == localDateTime
        else {
            throw LocalDateTimeResolverError.invalidFormat
        }

        var offsets: Set<Int> = []
        for seconds in stride(from: -172_800, through: 172_800, by: 21_600) {
            offsets.insert(timeZone.secondsFromGMT(
                for: naive.addingTimeInterval(TimeInterval(seconds))))
        }
        let localFormatter = formatter(timeZone: timeZone)
        let candidates = Set(offsets.compactMap { offset -> Date? in
            let candidate = naive.addingTimeInterval(TimeInterval(-offset))
            guard timeZone.secondsFromGMT(for: candidate) == offset,
                  localFormatter.string(from: candidate) == localDateTime
            else {
                return nil
            }
            return candidate
        })
        if candidates.isEmpty {
            return .invalid
        }
        if candidates.count > 1 {
            return .ambiguous
        }
        let instant = candidates.first!
        return .resolved(
            instant: instant,
            utcOffsetSeconds: timeZone.secondsFromGMT(for: instant))
    }

    private static func formatter(timeZone: TimeZone) -> DateFormatter {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = timeZone
        formatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"
        formatter.isLenient = false
        return formatter
    }
}

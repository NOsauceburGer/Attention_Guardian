import SwiftUI

public enum AGColor {
    public static let ambientTop = Color(
        red: 48 / 255,
        green: 132 / 255,
        blue: 166 / 255)
    public static let ambientMiddle = Color(
        red: 41 / 255,
        green: 99 / 255,
        blue: 143 / 255)
    public static let ambientDeep = Color(
        red: 28 / 255,
        green: 58 / 255,
        blue: 111 / 255)
    public static let mist = Color(
        red: 126 / 255,
        green: 220 / 255,
        blue: 225 / 255)
    public static let violetMist = Color(
        red: 111 / 255,
        green: 123 / 255,
        blue: 211 / 255)
    public static let ice = Color(
        red: 128 / 255,
        green: 205 / 255,
        blue: 245 / 255)
    public static let mandatory = Color(
        red: 208 / 255,
        green: 93 / 255,
        blue: 82 / 255)
    public static let warning = Color(
        red: 224 / 255,
        green: 181 / 255,
        blue: 82 / 255)
    public static let rest = Color(
        red: 115 / 255,
        green: 190 / 255,
        blue: 170 / 255)
}

public enum AGSpace {
    public static let compact: CGFloat = 8
    public static let related: CGFloat = 12
    public static let component: CGFloat = 16
    public static let section: CGFloat = 24
    public static let major: CGFloat = 36
}

public enum AGLayout {
    public static let readableMaximum: CGFloat = 680
    public static let minimumTouchTarget: CGFloat = 44
    public static let dashboardCornerRadius: CGFloat = 32
    public static let componentCornerRadius: CGFloat = 18

    public static func horizontalInset(
        compact: Bool,
        accessibilityText: Bool
    ) -> CGFloat {
        if accessibilityText { return AGSpace.component }
        return compact ? AGSpace.component : AGSpace.major
    }
}

public enum AGMotion {
    public static let calmSpring = Animation.spring(
        response: 0.34,
        dampingFraction: 0.86,
        blendDuration: 0.12)
    public static let settle = Animation.spring(
        response: 0.42,
        dampingFraction: 0.9,
        blendDuration: 0.14)
    public static let spatialLift = Animation.spring(
        response: 0.32,
        dampingFraction: 0.88,
        blendDuration: 0.1)
    public static let spatialFollow = Animation.interactiveSpring(
        response: 0.24,
        dampingFraction: 0.84,
        blendDuration: 0.08)
    public static let spatialSettle = Animation.spring(
        response: 0.4,
        dampingFraction: 0.86,
        blendDuration: 0.12)
    public static let spatialTargetScale: CGFloat = 1.032
    public static let spatialBubbleDiameter: CGFloat = 78
    public static let spatialTargetHysteresis: CGFloat = 10
}

import SwiftUI

public struct AmbientBackground: View {
    @Environment(\.accessibilityReduceMotion)
    private var reduceMotion
    @State private var transitionStartedAt = Date.distantPast

    private let isFocused: Bool

    public init(isFocused: Bool) {
        self.isFocused = isFocused
    }

    public var body: some View {
        TimelineView(.animation(
            minimumInterval: isFocused ? 1 / 24 : 1 / 30,
            paused: reduceMotion)
        ) { context in
            let phase = reduceMotion ? 0 : animationPhase(at: context.date)
            let pulse = reduceMotion ? 0 : transitionPulse(at: context.date)
            Group {
                if #available(macOS 15.0, iOS 18.0, *) {
                    mesh(phase: phase, pulse: pulse)
                } else {
                    fallback(phase: phase, pulse: pulse)
                }
            }
        }
        .saturation(isFocused ? 0.82 : 1)
        .animation(
            reduceMotion ? nil : .easeInOut(duration: 1.2),
            value: isFocused)
        .onChange(of: isFocused) {
            transitionStartedAt = Date()
        }
        .ignoresSafeArea()
        .accessibilityHidden(true)
    }

    @available(macOS 15.0, iOS 18.0, *)
    private func mesh(phase: Double, pulse: Double) -> some View {
        let drift = Float(isFocused ? 0.035 : 0.055)
        let pulseDrift = Float(pulse * 0.035)
        return MeshGradient(
            width: 3,
            height: 3,
            points: [
                [0, 0], [0.5, 0], [1, 0],
                [
                    0,
                    0.5 + drift * Float(sin(phase * 0.73))
                ],
                [
                    0.5 + (drift + pulseDrift) * Float(sin(phase)),
                    0.5 + (drift + pulseDrift) * Float(cos(phase * 0.83))
                ],
                [
                    1,
                    0.5 + drift * Float(cos(phase * 0.61))
                ],
                [0, 1], [0.54, 1], [1, 1]
            ],
            colors: [
                AGColor.ambientTop, AGColor.mist, AGColor.ambientMiddle,
                AGColor.ambientMiddle, AGColor.violetMist, AGColor.ambientDeep,
                AGColor.ambientDeep, AGColor.ambientMiddle, AGColor.ambientDeep
            ],
            smoothsColors: true)
    }

    private func fallback(phase: Double, pulse: Double) -> some View {
        let movement = (isFocused ? 0.04 : 0.07) + pulse * 0.035
        return LinearGradient(
            colors: [
                AGColor.ambientTop,
                AGColor.ambientMiddle,
                AGColor.ambientDeep
            ],
            startPoint: UnitPoint(
                x: 0.12 + movement * sin(phase * 0.72),
                y: 0.08 + movement * cos(phase * 0.58)),
            endPoint: UnitPoint(
                x: 0.88 + movement * cos(phase * 0.64),
                y: 0.92 + movement * sin(phase * 0.76)))
        .overlay {
            RadialGradient(
                colors: [
                    AGColor.violetMist.opacity(0.28),
                    .clear
                ],
                center: .topTrailing,
                startRadius: 0,
                endRadius: 420)
        }
    }

    private func animationPhase(at date: Date) -> Double {
        let speed = isFocused ? 0.075 : 0.13
        return date.timeIntervalSinceReferenceDate * speed
    }

    private func transitionPulse(at date: Date) -> Double {
        let elapsed = date.timeIntervalSince(transitionStartedAt)
        guard elapsed >= 0, elapsed < 1.6 else { return 0 }
        return sin(.pi * elapsed / 1.6) * (1 - elapsed / 1.6)
    }
}

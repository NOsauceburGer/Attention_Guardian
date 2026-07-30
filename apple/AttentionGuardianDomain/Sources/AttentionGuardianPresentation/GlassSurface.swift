import SwiftUI

public struct GlassSurface<Content: View>: View {
    @Environment(\.accessibilityReduceTransparency)
    private var reduceTransparency
    @Environment(\.colorSchemeContrast)
    private var contrast

    private let shape: RoundedRectangle
    private let content: Content

    public init(
        cornerRadius: CGFloat = AGLayout.componentCornerRadius,
        @ViewBuilder content: () -> Content
    ) {
        shape = RoundedRectangle(
            cornerRadius: cornerRadius,
            style: .continuous)
        self.content = content()
    }

    public var body: some View {
        content
            .background {
                if reduceTransparency {
                    shape.fill(AGColor.ambientDeep)
                } else {
                    shape.fill(.thinMaterial)
                }
            }
            .overlay {
                shape.strokeBorder(
                    .white.opacity(contrast == .increased ? 0.42 : 0.2),
                    lineWidth: contrast == .increased ? 1.5 : 0.75)
            }
            .shadow(
                color: AGColor.ambientDeep.opacity(0.2),
                radius: 10,
                y: 4)
            .clipShape(shape)
    }
}

public struct GlassCapsuleButtonStyle: ButtonStyle {
    @Environment(\.accessibilityReduceMotion)
    private var reduceMotion

    public init() {}

    public func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.headline)
            .foregroundStyle(.primary)
            .padding(.horizontal, AGSpace.section)
            .frame(minHeight: AGLayout.minimumTouchTarget)
            .background(.thinMaterial, in: Capsule())
            .overlay {
                Capsule().strokeBorder(
                    .white.opacity(configuration.isPressed ? 0.35 : 0.2),
                    lineWidth: 0.75)
            }
            .shadow(
                color: AGColor.ambientDeep.opacity(
                    configuration.isPressed ? 0.12 : 0.2),
                radius: configuration.isPressed ? 4 : 8,
                y: configuration.isPressed ? 1 : 3)
            .scaleEffect(configuration.isPressed && !reduceMotion ? 0.98 : 1)
            .animation(
                reduceMotion ? nil : AGMotion.calmSpring,
                value: configuration.isPressed)
            .contentShape(Capsule())
    }
}

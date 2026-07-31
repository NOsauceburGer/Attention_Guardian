import SwiftUI

struct LiquidGlassSurface<S: Shape>: View {
    @Environment(\.accessibilityReduceTransparency)
    private var reduceTransparency

    let shape: S
    var isInteractive = true

    var body: some View {
        if reduceTransparency {
            shape.fill(AGColor.ambientMiddle)
        } else if #available(macOS 26.0, iOS 26.0, *) {
            shape
                .fill(.clear)
                .glassEffect(
                    .clear.interactive(isInteractive),
                    in: shape)
        } else {
            shape.fill(.ultraThinMaterial)
        }
    }
}

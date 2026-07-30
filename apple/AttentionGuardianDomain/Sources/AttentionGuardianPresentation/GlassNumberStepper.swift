import SwiftUI

public struct GlassNumberStepper: View {
    @Binding private var value: Int
    private let range: ClosedRange<Int>
    private let label: String

    public init(
        _ label: String,
        value: Binding<Int>,
        in range: ClosedRange<Int>
    ) {
        self.label = label
        _value = value
        self.range = range
    }

    public var body: some View {
        HStack(spacing: AGSpace.compact) {
            stepButton(
                systemName: "minus",
                accessibilityLabel: "减少\(label)") {
                    value = max(range.lowerBound, value - 1)
                }

            TextField(
                label,
                value: $value,
                format: .number)
                .textFieldStyle(.plain)
                .multilineTextAlignment(.center)
                .frame(minWidth: AGLayout.minimumTouchTarget)
                .onSubmit(clamp)
                .accessibilityValue("\(value)")

            stepButton(
                systemName: "plus",
                accessibilityLabel: "增加\(label)") {
                    value = min(range.upperBound, value + 1)
                }
        }
        .padding(.horizontal, AGSpace.compact)
        .frame(minHeight: AGLayout.minimumTouchTarget)
        .background(.thinMaterial, in: Capsule())
        .overlay {
            Capsule().strokeBorder(.white.opacity(0.2), lineWidth: 0.75)
        }
        .accessibilityElement(children: .contain)
    }

    private func stepButton(
        systemName: String,
        accessibilityLabel: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            Image(systemName: systemName)
                .frame(
                    minWidth: AGLayout.minimumTouchTarget,
                    minHeight: AGLayout.minimumTouchTarget)
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(accessibilityLabel)
    }

    private func clamp() {
        value = min(range.upperBound, max(range.lowerBound, value))
    }
}

import SwiftUI

public struct GlassPicker<Value: Hashable, Label: View>: View {
    @Binding private var selection: Value
    private let values: [Value]
    private let label: (Value) -> Label

    public init(
        selection: Binding<Value>,
        values: [Value],
        @ViewBuilder label: @escaping (Value) -> Label
    ) {
        _selection = selection
        self.values = values
        self.label = label
    }

    public var body: some View {
        Menu {
            ForEach(values, id: \.self) { value in
                Button {
                    selection = value
                } label: {
                    label(value)
                }
            }
        } label: {
            HStack {
                Spacer(minLength: AGSpace.compact)
                label(selection)
                Spacer(minLength: AGSpace.compact)
                Image(systemName: "chevron.down")
                    .font(.caption.weight(.semibold))
            }
            .frame(minHeight: AGLayout.minimumTouchTarget)
            .padding(.horizontal, AGSpace.related)
            .background(.thinMaterial, in: RoundedRectangle(
                cornerRadius: AGLayout.componentCornerRadius,
                style: .continuous))
        }
        .menuStyle(.borderlessButton)
        .buttonStyle(.plain)
    }
}

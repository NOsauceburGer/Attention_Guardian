import SwiftUI

public enum GuardianDestination: String, CaseIterable, Identifiable, Sendable {
    case focus
    case add
    case manage

    public var id: String { rawValue }

    var title: String {
        switch self {
        case .focus: "专注"
        case .add: "添加事件"
        case .manage: "事件管理"
        }
    }

    var symbol: String {
        switch self {
        case .focus: "circle.circle"
        case .add: "plus"
        case .manage: "list.bullet"
        }
    }
}

public struct FloatingBottomDrawer: View {
    @Environment(\.accessibilityReduceMotion)
    private var reduceMotion
    @Binding private var selection: GuardianDestination
    @State private var isExpanded = false
    @Namespace private var selectionLens

    public init(selection: Binding<GuardianDestination>) {
        _selection = selection
    }

    public var body: some View {
        VStack(spacing: AGSpace.related) {
            Button {
                withAnimation(reduceMotion ? nil : AGMotion.settle) {
                    isExpanded.toggle()
                }
            } label: {
                Capsule()
                    .fill(.white.opacity(0.5))
                    .frame(width: 42, height: 5)
                    .frame(minHeight: AGLayout.minimumTouchTarget)
            }
            .buttonStyle(.plain)
            .accessibilityLabel(isExpanded ? "收起底部导航" : "展开底部导航")

            if isExpanded {
                HStack(spacing: AGSpace.compact) {
                    ForEach(GuardianDestination.allCases) { destination in
                        tab(destination)
                    }
                }
                .padding(AGSpace.compact)
                .background(.thinMaterial, in: Capsule())
                .overlay {
                    Capsule().strokeBorder(.white.opacity(0.2), lineWidth: 0.75)
                }
                .transition(.opacity.combined(with: .move(edge: .bottom)))
            }
        }
    }

    private func tab(_ destination: GuardianDestination) -> some View {
        Button {
            withAnimation(reduceMotion ? nil : AGMotion.calmSpring) {
                selection = destination
            }
        } label: {
            Label(destination.title, systemImage: destination.symbol)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(selection == destination ? AGColor.ice : .primary)
                .padding(.horizontal, AGSpace.component)
                .frame(minHeight: AGLayout.minimumTouchTarget)
                .background {
                    if selection == destination {
                        Capsule()
                            .fill(.thinMaterial)
                            .matchedGeometryEffect(
                                id: "guardian-selection-lens",
                                in: selectionLens)
                    }
                }
                .contentShape(Capsule())
        }
        .buttonStyle(.plain)
        .accessibilityAddTraits(
            selection == destination ? .isSelected : [])
    }
}

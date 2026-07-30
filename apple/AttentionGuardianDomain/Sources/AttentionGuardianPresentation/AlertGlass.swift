import SwiftUI

public enum AlertGlassTone: Sendable {
    case neutral
    case mandatory
    case warning

    var color: Color {
        switch self {
        case .neutral: AGColor.ice
        case .mandatory: AGColor.mandatory
        case .warning: AGColor.warning
        }
    }
}

public struct AlertGlass<Details: View>: View {
    @Environment(\.accessibilityReduceMotion)
    private var reduceMotion

    @State private var isExpanded = false
    private let title: String
    private let tone: AlertGlassTone
    private let details: Details

    public init(
        title: String,
        tone: AlertGlassTone,
        @ViewBuilder details: () -> Details
    ) {
        self.title = title
        self.tone = tone
        self.details = details()
    }

    public var body: some View {
        GlassSurface {
            VStack(alignment: .leading, spacing: AGSpace.related) {
                HStack(spacing: AGSpace.related) {
                    Circle()
                        .fill(tone.color)
                        .frame(width: 8, height: 8)
                        .accessibilityHidden(true)
                    Text(title)
                        .font(.subheadline.weight(.semibold))
                    Spacer(minLength: AGSpace.compact)
                    Image(systemName: isExpanded ? "chevron.up" : "chevron.down")
                        .font(.caption.weight(.semibold))
                        .accessibilityHidden(true)
                }
                if isExpanded {
                    details
                        .transition(.opacity)
                }
            }
            .padding(AGSpace.component)
            .contentShape(Rectangle())
            .onTapGesture(count: 2) {
                withAnimation(reduceMotion ? nil : AGMotion.settle) {
                    isExpanded.toggle()
                }
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityAction(
            named: isExpanded ? "收起详情" : "展开详情") {
                isExpanded.toggle()
            }
    }
}

import SwiftUI

public enum FocusDashboardState: Equatable, Sendable {
    case loading
    case empty
    case ready(title: String)
    case focused(title: String)

    var isFocused: Bool {
        if case .focused = self { true } else { false }
    }
}

public struct FocusDashboard: View {
    @Environment(\.horizontalSizeClass)
    private var horizontalSizeClass
    @Environment(\.dynamicTypeSize)
    private var dynamicTypeSize

    private let state: FocusDashboardState
    private let onStart: () -> Void
    @Binding private var destination: GuardianDestination

    public init(
        state: FocusDashboardState,
        destination: Binding<GuardianDestination>,
        onStart: @escaping () -> Void
    ) {
        self.state = state
        _destination = destination
        self.onStart = onStart
    }

    public var body: some View {
        ZStack {
            AmbientBackground(isFocused: state.isFocused)

            VStack(spacing: AGSpace.section) {
                Spacer(minLength: AGSpace.section)

                mainContent
                    .frame(maxWidth: AGLayout.readableMaximum)
                    .padding(.horizontal, horizontalInset)

                Spacer(minLength: AGSpace.section)

                FloatingBottomDrawer(selection: $destination)
                    .padding(.horizontal, horizontalInset)
                    .padding(.bottom, AGSpace.related)
            }
        }
        .foregroundStyle(.white)
    }

    @ViewBuilder
    private var mainContent: some View {
        switch state {
        case .loading:
            ProgressView()
                .controlSize(.small)
                .accessibilityLabel("正在读取事项")

        case .empty:
            VStack(spacing: AGSpace.component) {
                Text("现在没有需要执行的事项")
                    .font(.title2.weight(.semibold))
                Text("未来的事项仍由你决定何时添加")
                    .font(.body)
                    .foregroundStyle(.secondary)
            }
            .multilineTextAlignment(.center)
            .accessibilityElement(children: .combine)

        case .ready:
            Button("开始", action: onStart)
                .buttonStyle(GlassCapsuleButtonStyle())
                .accessibilityHint("开始后才会显示当前事项")

        case let .focused(title):
            GlassSurface(cornerRadius: AGLayout.dashboardCornerRadius) {
                VStack(spacing: AGSpace.section) {
                    Text(title)
                        .font(.largeTitle.weight(.semibold))
                        .multilineTextAlignment(.center)
                        .lineLimit(dynamicTypeSize.isAccessibilitySize ? nil : 3)
                        .fixedSize(horizontal: false, vertical: true)

                    Text("未来的事项已为你托管")
                        .font(.body)
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity)
                .padding(AGSpace.major)
            }
            .accessibilityElement(children: .combine)
        }
    }

    private var horizontalInset: CGFloat {
        AGLayout.horizontalInset(
            compact: horizontalSizeClass == .compact,
            accessibilityText: dynamicTypeSize.isAccessibilitySize)
    }
}

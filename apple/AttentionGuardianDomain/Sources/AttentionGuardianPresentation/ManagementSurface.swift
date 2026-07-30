import SwiftUI
import AttentionGuardianDomain

public struct ManagementDisplayState: Equatable, Sendable {
    public var showsStartTimes: Bool

    public init(showsStartTimes: Bool = false) {
        self.showsStartTimes = showsStartTimes
    }

    public var startTimeToggleTitle: String {
        showsStartTimes ? "隐藏开始时间" : "显示开始时间"
    }
}

public struct ManagementScheduledItem: Identifiable, Equatable, Sendable {
    public let todo: ScheduledTodo

    public init(todo: ScheduledTodo) {
        self.todo = todo
    }

    public var id: UUID { todo.id }
    public var title: String { todo.title }
    public var start: Date { todo.start }
    public var isMandatory: Bool { todo.isMandatory }
    public var isBreak: Bool { todo.title == ScheduleManagement.breakTitle }
}

public struct ManagementFutureItem: Identifiable, Equatable, Sendable {
    public let record: UnscheduledTodoRecord

    public init(record: UnscheduledTodoRecord) {
        self.record = record
    }

    public var id: UUID { record.todo.id }
    public var title: String { record.todo.title }
    public var dateText: String { record.todo.scheduledDate.description }
}

public struct ManagementSurface: View {
    @Environment(\.horizontalSizeClass)
    private var horizontalSizeClass
    @Environment(\.dynamicTypeSize)
    private var dynamicTypeSize

    private let scheduledItems: [ManagementScheduledItem]
    private let futureItems: [ManagementFutureItem]?
    private let isLoading: Bool
    private let onLoadFutureTodos: () async -> Void
    private let onDeleteScheduled: (UUID) async throws -> Void
    private let onDeleteFuture: (UUID) async throws -> Void
    private let onBack: () -> Void

    @State private var displayState = ManagementDisplayState()
    @State private var isFutureExpanded = false
    @State private var deletion: DeletionRequest?
    @State private var errorMessage: String?

    public init(
        scheduledItems: [ManagementScheduledItem],
        futureItems: [ManagementFutureItem]?,
        isLoading: Bool,
        onLoadFutureTodos: @escaping () async -> Void,
        onDeleteScheduled: @escaping (UUID) async throws -> Void,
        onDeleteFuture: @escaping (UUID) async throws -> Void,
        onBack: @escaping () -> Void
    ) {
        self.scheduledItems = scheduledItems
        self.futureItems = futureItems
        self.isLoading = isLoading
        self.onLoadFutureTodos = onLoadFutureTodos
        self.onDeleteScheduled = onDeleteScheduled
        self.onDeleteFuture = onDeleteFuture
        self.onBack = onBack
    }

    public var body: some View {
        ZStack {
            AmbientBackground(isFocused: false)

            ScrollView {
                VStack(spacing: AGSpace.section) {
                    header
                    scheduledSection
                    futureSection
                }
                .frame(maxWidth: AGLayout.readableMaximum)
                .padding(.horizontal, horizontalInset)
                .padding(.vertical, AGSpace.section)
            }
        }
        .foregroundStyle(.white)
        .confirmationDialog(
            deletion.map { "删除“\($0.title)”？" } ?? "",
            isPresented: Binding(
                get: { deletion != nil },
                set: { if !$0 { deletion = nil } }),
            titleVisibility: .visible
        ) {
            Button("删除", role: .destructive) {
                guard let request = deletion else { return }
                deletion = nil
                Task { await confirmDeletion(request) }
            }
            Button("取消", role: .cancel) {
                deletion = nil
            }
        } message: {
            Text("删除后，这条事项将退出活动列表。")
        }
        .alert(
            "暂时无法完成操作",
            isPresented: Binding(
                get: { errorMessage != nil },
                set: { if !$0 { errorMessage = nil } })
        ) {
            Button("知道了", role: .cancel) {}
        } message: {
            Text(errorMessage ?? "")
        }
    }

    private var header: some View {
        ViewThatFits {
            HStack(spacing: AGSpace.component) {
                headerTitle
                Spacer()
                headerActions
            }
            VStack(alignment: .leading, spacing: AGSpace.component) {
                headerTitle
                headerActions
            }
        }
    }

    private var headerTitle: some View {
        VStack(alignment: .leading, spacing: AGSpace.compact) {
            Text("事件管理")
                .font(.largeTitle.weight(.semibold))
            Text("按执行顺序查看活动事项")
                .foregroundStyle(.secondary)
        }
    }

    private var headerActions: some View {
        ViewThatFits {
            HStack(spacing: AGSpace.related) {
                startTimeToggle
                backButton
            }
            VStack(alignment: .leading, spacing: AGSpace.compact) {
                startTimeToggle
                backButton
            }
        }
    }

    private var startTimeToggle: some View {
        Button(displayState.startTimeToggleTitle) {
            displayState.showsStartTimes.toggle()
        }
        .buttonStyle(GlassCapsuleButtonStyle())
    }

    private var backButton: some View {
        Button("返回专注", action: onBack)
            .buttonStyle(GlassCapsuleButtonStyle())
    }

    @ViewBuilder
    private var scheduledSection: some View {
        if isLoading {
            ProgressView()
                .accessibilityLabel("正在读取当日待办")
        } else if scheduledItems.isEmpty {
            GlassSurface {
                Text("当前没有活动的当日待办")
                    .frame(maxWidth: .infinity)
                    .padding(AGSpace.section)
            }
        } else {
            LazyVStack(spacing: AGSpace.related) {
                ForEach(scheduledItems) { item in
                    scheduledBubble(item)
                }
            }
        }
    }

    private func scheduledBubble(
        _ item: ManagementScheduledItem
    ) -> some View {
        GlassSurface {
            HStack(spacing: AGSpace.component) {
                if item.isMandatory {
                    Image(systemName: "lock.fill")
                        .foregroundStyle(AGColor.mandatory)
                        .accessibilityLabel("不可移动事件")
                } else if item.isBreak {
                    Image(systemName: "cup.and.saucer.fill")
                        .foregroundStyle(AGColor.rest)
                        .accessibilityLabel("休息")
                }

                Text(item.title)
                    .font(.headline)
                    .frame(maxWidth: .infinity, alignment: .leading)

                if displayState.showsStartTimes {
                    Text(item.start, style: .time)
                        .monospacedDigit()
                        .foregroundStyle(.secondary)
                        .accessibilityLabel("开始时间")
                }

                Button("删除", role: .destructive) {
                    deletion = DeletionRequest(
                        id: item.id,
                        title: item.title,
                        kind: .scheduled)
                }
                .buttonStyle(.plain)
            }
            .padding(AGSpace.component)
        }
        .accessibilityElement(children: .contain)
    }

    private var futureSection: some View {
        GlassSurface {
            VStack(spacing: AGSpace.component) {
                Button {
                    isFutureExpanded.toggle()
                    if isFutureExpanded, futureItems == nil {
                        Task { await onLoadFutureTodos() }
                    }
                } label: {
                    HStack {
                        Text(isFutureExpanded ? "收起未来待办" : "展开未来待办")
                            .font(.headline)
                        Spacer()
                        Image(systemName: isFutureExpanded
                            ? "chevron.up" : "chevron.down")
                    }
                    .frame(minHeight: AGLayout.minimumTouchTarget)
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)

                if isFutureExpanded {
                    if let futureItems {
                        if futureItems.isEmpty {
                            Text("没有活动的未来待办")
                                .foregroundStyle(.secondary)
                        } else {
                            LazyVStack(spacing: AGSpace.related) {
                                ForEach(futureItems) { item in
                                    futureBubble(item)
                                }
                            }
                        }
                    } else {
                        ProgressView()
                            .accessibilityLabel("正在读取未来待办")
                    }
                }
            }
            .padding(AGSpace.component)
        }
    }

    private func futureBubble(_ item: ManagementFutureItem) -> some View {
        HStack(spacing: AGSpace.component) {
            VStack(alignment: .leading, spacing: AGSpace.compact) {
                Text(item.title)
                    .font(.headline)
                Text(item.dateText)
                    .foregroundStyle(.secondary)
            }
            Spacer()
            Button("删除", role: .destructive) {
                deletion = DeletionRequest(
                    id: item.id,
                    title: item.title,
                    kind: .future)
            }
            .buttonStyle(.plain)
        }
        .padding(AGSpace.component)
        .background(.thinMaterial, in: RoundedRectangle(
            cornerRadius: AGLayout.componentCornerRadius,
            style: .continuous))
    }

    private func confirmDeletion(_ request: DeletionRequest) async {
        do {
            switch request.kind {
            case .scheduled:
                try await onDeleteScheduled(request.id)
            case .future:
                try await onDeleteFuture(request.id)
            }
        } catch {
            errorMessage = "本地事项没有改变，请稍后重试。"
        }
    }

    private var horizontalInset: CGFloat {
        AGLayout.horizontalInset(
            compact: horizontalSizeClass == .compact,
            accessibilityText: dynamicTypeSize.isAccessibilitySize)
    }
}

private struct DeletionRequest {
    enum Kind {
        case scheduled
        case future
    }

    let id: UUID
    let title: String
    let kind: Kind
}

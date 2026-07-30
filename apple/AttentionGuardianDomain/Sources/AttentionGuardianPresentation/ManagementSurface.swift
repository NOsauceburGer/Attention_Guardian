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

public struct ManagementScheduledEditDraft: Equatable, Sendable {
    public let id: UUID
    public var title: String
    public var hour: Int
    public var minute: Int
    public var durationHours: Int
    public var durationMinutes: Int
    public var isMandatory: Bool

    public init(item: ManagementScheduledItem) {
        let todo = item.todo
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(
            secondsFromGMT: todo.utcOffsetSeconds)
            ?? TimeZone(secondsFromGMT: 0)!
        let components = calendar.dateComponents(
            [.hour, .minute],
            from: todo.start)
        id = todo.id
        title = todo.title
        hour = components.hour ?? 0
        minute = components.minute ?? 0
        let totalMinutes = Int(todo.duration / 60)
        durationHours = totalMinutes / 60
        durationMinutes = totalMinutes % 60
        isMandatory = todo.isMandatory
    }

    public var isValid: Bool {
        !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
            && durationHours * 60 + durationMinutes > 0
    }
}

public enum ManagementScheduledEditOutcome: Equatable, Sendable {
    case saved
    case conflictResolutionRequired(conflictingTitle: String)
    case mandatoryStartRejected(conflictingTitle: String)
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
    private let onSaveScheduled: (
        ManagementScheduledEditDraft,
        StartTimeConflictResolution?
    ) async throws -> ManagementScheduledEditOutcome
    private let onDeleteScheduled: (UUID) async throws -> Void
    private let onDeleteFuture: (UUID) async throws -> Void
    private let onBack: () -> Void

    @State private var displayState = ManagementDisplayState()
    @State private var isFutureExpanded = false
    @State private var deletion: DeletionRequest?
    @State private var editDraft: ManagementScheduledEditDraft?
    @State private var pendingConflictTitle: String?
    @State private var isLeavingWithEdit = false
    @State private var shouldLeaveAfterSave = false
    @State private var isSavingEdit = false
    @State private var errorMessage: String?

    public init(
        scheduledItems: [ManagementScheduledItem],
        futureItems: [ManagementFutureItem]?,
        isLoading: Bool,
        onLoadFutureTodos: @escaping () async -> Void,
        onSaveScheduled: @escaping (
            ManagementScheduledEditDraft,
            StartTimeConflictResolution?
        ) async throws -> ManagementScheduledEditOutcome,
        onDeleteScheduled: @escaping (UUID) async throws -> Void,
        onDeleteFuture: @escaping (UUID) async throws -> Void,
        onBack: @escaping () -> Void
    ) {
        self.scheduledItems = scheduledItems
        self.futureItems = futureItems
        self.isLoading = isLoading
        self.onLoadFutureTodos = onLoadFutureTodos
        self.onSaveScheduled = onSaveScheduled
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
        .confirmationDialog(
            pendingConflictTitle.map {
                "开始时间与“\($0)”重叠"
            } ?? "",
            isPresented: Binding(
                get: { pendingConflictTitle != nil },
                set: { if !$0 { pendingConflictTitle = nil } }),
            titleVisibility: .visible
        ) {
            Button("原事件移到此事件之后") {
                pendingConflictTitle = nil
                Task {
                    await saveEdit(
                        resolution: .moveExistingAfterEdited)
                }
            }
            Button("原事件在此时间结束") {
                pendingConflictTitle = nil
                Task {
                    await saveEdit(
                        resolution: .truncateExistingAtNewStart)
                }
            }
            Button("取消", role: .cancel) {
                pendingConflictTitle = nil
                shouldLeaveAfterSave = false
            }
        } message: {
            Text("请选择如何处理正在这个时刻执行的普通事件。")
        }
        .confirmationDialog(
            "还有未收起的修改",
            isPresented: $isLeavingWithEdit,
            titleVisibility: .visible
        ) {
            Button("保存并离开") {
                shouldLeaveAfterSave = true
                Task { await saveEdit(resolution: nil) }
            }
            Button("不保存并离开", role: .destructive) {
                editDraft = nil
                shouldLeaveAfterSave = false
                onBack()
            }
            Button("继续编辑", role: .cancel) {}
        } message: {
            Text("你可以保存修改、不保存离开，或返回继续编辑。")
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
        Button("返回专注", action: requestBack)
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
            VStack(spacing: AGSpace.component) {
                scheduledSummary(item)
                    .onTapGesture(count: 2) {
                        toggleEdit(item)
                    }

                if editDraft?.id == item.id {
                    scheduledEditor(item)
                        .transition(.opacity)
                }
            }
            .padding(AGSpace.component)
        }
        .accessibilityAction(
            named: editDraft?.id == item.id
                ? "收起并保存编辑" : "展开编辑"
        ) {
            toggleEdit(item)
        }
        .accessibilityElement(children: .contain)
    }

    private func scheduledSummary(
        _ item: ManagementScheduledItem
    ) -> some View {
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
    }

    @ViewBuilder
    private func scheduledEditor(
        _ item: ManagementScheduledItem
    ) -> some View {
        if let binding = editDraftBinding(for: item.id) {
            VStack(alignment: .leading, spacing: AGSpace.component) {
                editorField("名称") {
                    TextField("要做什么？", text: binding.title)
                        .textFieldStyle(.plain)
                        .padding(.horizontal, AGSpace.component)
                        .frame(minHeight: AGLayout.minimumTouchTarget)
                        .background(.thinMaterial, in: RoundedRectangle(
                            cornerRadius: AGLayout.componentCornerRadius,
                            style: .continuous))
                        .disabled(item.isBreak)
                }

                editorField("开始时间") {
                    HStack(spacing: AGSpace.related) {
                        GlassPicker(
                            selection: binding.hour,
                            values: Array(0...23)
                        ) {
                            Text(String(format: "%02d", $0))
                        }
                        Text(":")
                        GlassPicker(
                            selection: binding.minute,
                            values: Array(0...59)
                        ) {
                            Text(String(format: "%02d", $0))
                        }
                    }
                }

                editorField("持续时间") {
                    ViewThatFits {
                        HStack(spacing: AGSpace.component) {
                            durationEditor(binding)
                        }
                        VStack(spacing: AGSpace.related) {
                            durationEditor(binding)
                        }
                    }
                }

                Toggle(
                    "不可移动事件",
                    isOn: binding.isMandatory)
                    .toggleStyle(.switch)

                Text("再次双击气泡会统一试算并保存")
                    .font(.footnote)
                    .foregroundStyle(.secondary)

                if isSavingEdit {
                    ProgressView()
                        .accessibilityLabel("正在保存修改")
                }
            }
        }
    }

    @ViewBuilder
    private func durationEditor(
        _ binding: Binding<ManagementScheduledEditDraft>
    ) -> some View {
        LabeledContent("小时") {
            GlassNumberStepper(
                "小时",
                value: binding.durationHours,
                in: 0...23)
        }
        LabeledContent("分钟") {
            GlassNumberStepper(
                "分钟",
                value: binding.durationMinutes,
                in: 0...59)
        }
    }

    private func editorField<Content: View>(
        _ title: String,
        @ViewBuilder content: () -> Content
    ) -> some View {
        VStack(alignment: .leading, spacing: AGSpace.compact) {
            Text(title)
                .font(.subheadline.weight(.semibold))
            content()
        }
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

    private func toggleEdit(_ item: ManagementScheduledItem) {
        guard !isSavingEdit else { return }
        if editDraft?.id == item.id {
            Task { await saveEdit(resolution: nil) }
        } else if editDraft == nil {
            editDraft = ManagementScheduledEditDraft(item: item)
        } else {
            errorMessage = "请先收起正在编辑的事项。"
        }
    }

    private func saveEdit(
        resolution: StartTimeConflictResolution?
    ) async {
        guard let draft = editDraft, draft.isValid else {
            errorMessage = "名称不能为空，持续时间必须大于零。"
            shouldLeaveAfterSave = false
            return
        }
        isSavingEdit = true
        defer { isSavingEdit = false }
        do {
            switch try await onSaveScheduled(draft, resolution) {
            case .saved:
                editDraft = nil
                if shouldLeaveAfterSave {
                    shouldLeaveAfterSave = false
                    onBack()
                }
            case let .conflictResolutionRequired(title):
                pendingConflictTitle = title
            case let .mandatoryStartRejected(title):
                shouldLeaveAfterSave = false
                errorMessage = "“\(title)”是不可移动事件，这个开始时间无法使用。"
            }
        } catch {
            shouldLeaveAfterSave = false
            errorMessage = "本地事项没有改变，请稍后重试。"
        }
    }

    private func requestBack() {
        if editDraft == nil {
            onBack()
        } else {
            isLeavingWithEdit = true
        }
    }

    private func editDraftBinding(
        for id: UUID
    ) -> Binding<ManagementScheduledEditDraft>? {
        guard editDraft?.id == id else { return nil }
        return Binding(
            get: { editDraft! },
            set: { editDraft = $0 })
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

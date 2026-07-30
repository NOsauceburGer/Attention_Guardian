import SwiftUI
import AttentionGuardianDomain

public struct ScheduledTodoDraft: Equatable, Sendable {
    public let title: String
    public let hour: Int
    public let minute: Int
    public let durationHours: Int
    public let durationMinutes: Int
    public let isMandatory: Bool
}

public enum FutureDateDraft: Equatable, Sendable {
    case relative(days: Int)
    case exact(LocalDate)
}

public struct FutureTodoDraft: Equatable, Sendable {
    public let title: String
    public let date: FutureDateDraft
    public let isMandatory: Bool
}

public struct AddEventFlow: View {
    private enum Step {
        case chooseType
        case scheduled
        case future
    }

    @State private var step: Step = .chooseType
    private let onCancel: () -> Void
    private let onSaveScheduled: (ScheduledTodoDraft) async throws -> Void
    private let onSaveFuture: (FutureTodoDraft) async throws -> Void

    public init(
        onCancel: @escaping () -> Void,
        onSaveScheduled: @escaping (ScheduledTodoDraft) async throws -> Void,
        onSaveFuture: @escaping (FutureTodoDraft) async throws -> Void
    ) {
        self.onCancel = onCancel
        self.onSaveScheduled = onSaveScheduled
        self.onSaveFuture = onSaveFuture
    }

    public var body: some View {
        ZStack {
            AmbientBackground(isFocused: false)
            Group {
                switch step {
                case .chooseType:
                    typeSelection
                case .scheduled:
                    ScheduledTodoForm(
                        onBack: { step = .chooseType },
                        onSave: onSaveScheduled)
                case .future:
                    FutureTodoForm(
                        onBack: { step = .chooseType },
                        onSave: onSaveFuture)
                }
            }
            .frame(maxWidth: AGLayout.readableMaximum)
            .padding(.horizontal, AGSpace.component)
            .padding(.vertical, AGSpace.section)
        }
        .foregroundStyle(.white)
    }

    private var typeSelection: some View {
        GlassSurface(cornerRadius: AGLayout.dashboardCornerRadius) {
            VStack(spacing: AGSpace.section) {
                Text("添加事件")
                    .font(.largeTitle.weight(.semibold))
                Text("先选择事项属于哪一种")
                    .foregroundStyle(.secondary)

                ViewThatFits {
                    HStack(spacing: AGSpace.component) {
                        typeButton("今天要做什么？", systemImage: "clock") {
                            step = .scheduled
                        }
                        typeButton("这些事情之后做", systemImage: "calendar") {
                            step = .future
                        }
                    }
                    VStack(spacing: AGSpace.component) {
                        typeButton("今天要做什么？", systemImage: "clock") {
                            step = .scheduled
                        }
                        typeButton("这些事情之后做", systemImage: "calendar") {
                            step = .future
                        }
                    }
                }

                Button("取消", action: onCancel)
                    .buttonStyle(.plain)
            }
            .padding(AGSpace.major)
        }
    }

    private func typeButton(
        _ title: String,
        systemImage: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            Label(title, systemImage: systemImage)
                .frame(maxWidth: .infinity)
        }
        .buttonStyle(GlassCapsuleButtonStyle())
    }
}

private struct ScheduledTodoForm: View {
    @State private var title = ""
    @State private var hour = Calendar.current.component(.hour, from: Date())
    @State private var minute = Calendar.current.component(.minute, from: Date())
    @State private var durationHours = 0
    @State private var durationMinutes = 30
    @State private var isMandatory = false
    @State private var isSaving = false
    @State private var errorMessage: String?

    let onBack: () -> Void
    let onSave: (ScheduledTodoDraft) async throws -> Void

    var body: some View {
        formSurface(title: "当日待办") {
            field("名称") {
                GlassTextField("要做什么？", text: $title)
            }
            field("开始时间") {
                HStack(spacing: AGSpace.related) {
                    GlassPicker(selection: $hour, values: Array(0...23)) {
                        Text(String(format: "%02d", $0))
                    }
                    Text(":")
                    GlassPicker(selection: $minute, values: Array(0...59)) {
                        Text(String(format: "%02d", $0))
                    }
                }
            }
            field("持续时间") {
                ViewThatFits {
                    HStack(spacing: AGSpace.component) { durationControls }
                    VStack(spacing: AGSpace.related) { durationControls }
                }
            }
            Toggle("不可移动事件", isOn: $isMandatory)
                .toggleStyle(.switch)
            actions(saveDisabled:
                title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                || durationHours + durationMinutes == 0)
        }
    }

    @ViewBuilder private var durationControls: some View {
        LabeledContent("小时") {
            GlassNumberStepper("小时", value: $durationHours, in: 0...23)
        }
        LabeledContent("分钟") {
            GlassNumberStepper("分钟", value: $durationMinutes, in: 0...59)
        }
    }

    private func actions(saveDisabled: Bool) -> some View {
        FormActions(
            isSaving: isSaving,
            saveDisabled: saveDisabled,
            errorMessage: errorMessage,
            onBack: onBack
        ) {
            isSaving = true
            defer { isSaving = false }
            do {
                try await onSave(ScheduledTodoDraft(
                    title: title,
                    hour: hour,
                    minute: minute,
                    durationHours: durationHours,
                    durationMinutes: durationMinutes,
                    isMandatory: isMandatory))
            } catch {
                errorMessage = "保存失败，请检查输入后重试。"
            }
        }
    }
}

private struct FutureTodoForm: View {
    @State private var title = ""
    @State private var relativeDays: Int? = 1
    @State private var year = Calendar.current.component(.year, from: Date())
    @State private var month = Calendar.current.component(.month, from: Date())
    @State private var day = Calendar.current.component(.day, from: Date())
    @State private var isMandatory = false
    @State private var isSaving = false
    @State private var errorMessage: String?
    @State private var isApplyingRelativeDate = false

    let onBack: () -> Void
    let onSave: (FutureTodoDraft) async throws -> Void

    var body: some View {
        formSurface(title: "未来待办") {
            field("名称") {
                GlassTextField("之后要做什么？", text: $title)
            }
            field("快捷日期") {
                HStack(spacing: AGSpace.related) {
                    relativeButton("一天后", days: 1)
                    relativeButton("两天后", days: 2)
                }
            }
            field("具体日期") {
                ViewThatFits {
                    HStack(spacing: AGSpace.related) { datePickers }
                    VStack(spacing: AGSpace.related) { datePickers }
                }
            }
            Toggle("不可移动事件", isOn: $isMandatory)
                .toggleStyle(.switch)
            FormActions(
                isSaving: isSaving,
                saveDisabled: title.trimmingCharacters(
                    in: .whitespacesAndNewlines).isEmpty,
                errorMessage: errorMessage,
                onBack: onBack
            ) {
                isSaving = true
                defer { isSaving = false }
                do {
                    let date: FutureDateDraft
                    if let relativeDays {
                        date = .relative(days: relativeDays)
                    } else {
                        date = .exact(try LocalDate(
                            year: year,
                            month: month,
                            day: day))
                    }
                    try await onSave(FutureTodoDraft(
                        title: title,
                        date: date,
                        isMandatory: isMandatory))
                } catch {
                    errorMessage = "日期或名称无效，请修改后重试。"
                }
            }
        }
        .onChange(of: year) { clampDay() }
        .onChange(of: month) { clampDay() }
        .onChange(of: day) {
            if relativeDays != nil && !isApplyingRelativeDate {
                relativeDays = nil
            }
        }
    }

    @ViewBuilder private var datePickers: some View {
        GlassPicker(selection: $year, values: Array(year...(year + 10))) {
            Text("\($0) 年")
        }
        GlassPicker(selection: $month, values: Array(1...12)) {
            Text("\($0) 月")
        }
        GlassPicker(selection: $day, values: Array(1...daysInMonth)) {
            Text("\($0) 日")
        }
    }

    private func relativeButton(_ title: String, days: Int) -> some View {
        Button(title) {
            if relativeDays == days {
                relativeDays = nil
            } else {
                isApplyingRelativeDate = true
                relativeDays = days
                let calendar = Calendar.current
                let target = calendar.date(
                    byAdding: .day,
                    value: days,
                    to: Date()) ?? Date()
                year = calendar.component(.year, from: target)
                month = calendar.component(.month, from: target)
                day = calendar.component(.day, from: target)
                relativeDays = days
                DispatchQueue.main.async {
                    isApplyingRelativeDate = false
                }
            }
        }
        .buttonStyle(GlassCapsuleButtonStyle())
        .tint(relativeDays == days ? AGColor.ice : nil)
    }

    private var daysInMonth: Int {
        var components = DateComponents()
        components.year = year
        components.month = month
        return Calendar(identifier: .gregorian)
            .range(of: .day, in: .month, for: components.date ?? Date())?
            .count ?? 28
    }

    private func clampDay() {
        day = min(day, daysInMonth)
        if !isApplyingRelativeDate {
            relativeDays = nil
        }
    }
}

private struct GlassTextField: View {
    let prompt: String
    @Binding var text: String

    init(_ prompt: String, text: Binding<String>) {
        self.prompt = prompt
        _text = text
    }

    var body: some View {
        TextField(prompt, text: $text)
            .textFieldStyle(.plain)
            .padding(.horizontal, AGSpace.component)
            .frame(minHeight: AGLayout.minimumTouchTarget)
            .background(.thinMaterial, in: RoundedRectangle(
                cornerRadius: AGLayout.componentCornerRadius,
                style: .continuous))
    }
}

private struct FormActions: View {
    let isSaving: Bool
    let saveDisabled: Bool
    let errorMessage: String?
    let onBack: () -> Void
    let onSave: () async -> Void

    var body: some View {
        VStack(spacing: AGSpace.related) {
            if let errorMessage {
                Text(errorMessage)
                    .font(.footnote)
                    .foregroundStyle(AGColor.warning)
            }
            HStack {
                Button("返回", action: onBack)
                    .buttonStyle(.plain)
                Spacer()
                Button(isSaving ? "正在保存…" : "保存") {
                    Task { await onSave() }
                }
                .buttonStyle(GlassCapsuleButtonStyle())
                .disabled(saveDisabled || isSaving)
            }
        }
    }
}

@MainActor
private func field<Content: View>(
    _ title: String,
    @ViewBuilder content: () -> Content
) -> some View {
    VStack(alignment: .leading, spacing: AGSpace.compact) {
        Text(title)
            .font(.subheadline.weight(.semibold))
        content()
    }
}

@MainActor
private func formSurface<Content: View>(
    title: String,
    @ViewBuilder content: () -> Content
) -> some View {
    ScrollView {
        GlassSurface(cornerRadius: AGLayout.dashboardCornerRadius) {
            VStack(alignment: .leading, spacing: AGSpace.section) {
                Text(title)
                    .font(.largeTitle.weight(.semibold))
                content()
            }
            .padding(AGSpace.major)
        }
    }
}

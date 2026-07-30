import Testing
import Foundation
import AttentionGuardianDomain
@testable import AttentionGuardianPresentation

@Suite("Adaptive SwiftUI design tokens")
struct AdaptiveLayoutTests {
    @Test("compact and accessibility layouts never gain wider insets")
    func adaptiveInsets() {
        let regular = AGLayout.horizontalInset(
            compact: false,
            accessibilityText: false)
        let compact = AGLayout.horizontalInset(
            compact: true,
            accessibilityText: false)
        let accessibility = AGLayout.horizontalInset(
            compact: false,
            accessibilityText: true)

        #expect(compact < regular)
        #expect(accessibility <= compact)
    }

    @Test("touch targets follow Apple minimum")
    func minimumTouchTarget() {
        #expect(AGLayout.minimumTouchTarget >= 44)
    }

    @Test("management display state hides start times by default")
    func managementStartTimeDefaultsHidden() throws {
        let state = ManagementDisplayState()
        let item = ManagementScheduledItem(
            todo: try ScheduledTodo(
                id: #require(UUID(uuidString:
                    "00000000-0000-0000-0000-000000000901")),
                title: "专注",
                start: Date(timeIntervalSince1970: 1_775_400_000),
                end: Date(timeIntervalSince1970: 1_775_401_800)))

        #expect(state.showsStartTimes == false)
        #expect(state.startTimeToggleTitle == "显示开始时间")
        #expect(item.title == "专注")
        #expect(item.isMandatory == false)
    }

    @Test("scheduled edit draft starts from the persisted todo")
    func scheduledEditDraftUsesPersistedValues() throws {
        let start = Date(timeIntervalSince1970: 1_775_400_000)
        let todo = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000902")),
            title: "准备材料",
            start: start,
            end: start.addingTimeInterval(5_400),
            utcOffsetSeconds: 28_800,
            isMandatory: true)

        let draft = ManagementScheduledEditDraft(
            item: ManagementScheduledItem(todo: todo))

        #expect(draft.title == "准备材料")
        #expect(draft.durationHours == 1)
        #expect(draft.durationMinutes == 30)
        #expect(draft.isMandatory)
        #expect(draft.isValid)
    }
}

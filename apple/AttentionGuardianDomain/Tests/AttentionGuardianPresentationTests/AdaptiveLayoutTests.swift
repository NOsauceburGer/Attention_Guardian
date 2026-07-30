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

    @Test("spatial drag follows the legal lifecycle")
    func spatialDragLifecycle() throws {
        let id = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000903"))
        var machine = SpatialDragMachine()

        machine.press(todoId: id)
        machine.lift(originIndex: 1)
        machine.drag()
        machine.magnetize(targetIndex: 2)
        machine.release()
        machine.beginCommit()
        machine.finish()

        #expect(machine.phase == .idle)
    }

    @Test("failed spatial drag returns to its origin")
    func spatialDragFailureReturns() throws {
        let id = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000904"))
        var machine = SpatialDragMachine()
        machine.press(todoId: id)
        machine.lift(originIndex: 1)
        machine.drag()
        machine.fail()

        #expect(machine.phase == .returning(
            todoId: id,
            originIndex: 1))
        machine.finish()
        #expect(machine.phase == .idle)
    }

    @Test("failed spatial commit returns to its true origin")
    func spatialCommitFailureReturns() throws {
        let id = try #require(UUID(uuidString:
            "00000000-0000-0000-0000-000000000905"))
        var machine = SpatialDragMachine()
        machine.press(todoId: id)
        machine.lift(originIndex: 3)
        machine.drag()
        machine.magnetize(targetIndex: 1)
        machine.release()
        machine.beginCommit()
        machine.fail()

        #expect(machine.phase == .returning(
            todoId: id,
            originIndex: 3))
    }

    @Test("target resolver keeps the current target inside hysteresis")
    func targetResolverUsesHysteresis() {
        let frames = [
            0: CGRect(x: 0, y: 0, width: 300, height: 50),
            1: CGRect(x: 0, y: 62, width: 300, height: 50)
        ]
        let previews = [
            ManagementDropPreview(
                requestedIndex: 0,
                actualIndex: 0,
                usedFallbackPosition: false),
            ManagementDropPreview(
                requestedIndex: 1,
                actualIndex: 1,
                usedFallbackPosition: false)
        ]

        let target = SpatialDropResolver.targetIndex(
            pointerY: 57,
            currentTarget: 0,
            rowFrames: frames,
            previews: previews,
            hysteresis: 8)

        #expect(target == 0)
    }

    @Test("target resolver distinguishes items that share one row")
    func targetResolverUsesHorizontalPosition() {
        let frames = [
            0: CGRect(x: 0, y: 0, width: 140, height: 50),
            1: CGRect(x: 152, y: 0, width: 140, height: 50)
        ]
        let previews = [
            ManagementDropPreview(
                requestedIndex: 0,
                actualIndex: 0,
                usedFallbackPosition: false),
            ManagementDropPreview(
                requestedIndex: 1,
                actualIndex: 1,
                usedFallbackPosition: false)
        ]

        let target = SpatialDropResolver.target(
            pointer: CGPoint(x: 230, y: 25),
            currentTarget: nil,
            rowFrames: frames,
            previews: previews,
            hysteresis: 8)

        #expect(target?.actualIndex == 1)
    }

    @Test("only ordinary scheduled items can start spatial drag")
    func ordinaryOnlySpatialDrag() throws {
        let start = Date(timeIntervalSince1970: 1_775_410_000)
        let ordinary = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000905")),
            title: "普通",
            start: start,
            end: start.addingTimeInterval(1_800))
        let mandatory = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000906")),
            title: "不可移动",
            start: ordinary.end,
            end: ordinary.end.addingTimeInterval(1_800),
            isMandatory: true)

        #expect(ManagementScheduledItem(
            todo: ordinary).isSpatiallyDraggable)
        #expect(!ManagementScheduledItem(
            todo: mandatory).isSpatiallyDraggable)
        #expect(ManagementScheduledItem(
            todo: mandatory,
            allowsMandatoryGroupDrag: true).isSpatiallyDraggable)
    }

    @Test("continuous mandatory group shares one layout row")
    func mandatoryGroupLayout() throws {
        let start = Date(timeIntervalSince1970: 1_775_420_000)
        let first = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000907")),
            title: "固定一",
            start: start,
            end: start.addingTimeInterval(1_800),
            isMandatory: true)
        let second = try ScheduledTodo(
            id: #require(UUID(uuidString:
                "00000000-0000-0000-0000-000000000908")),
            title: "固定二",
            start: first.end,
            end: first.end.addingTimeInterval(1_800),
            isMandatory: true)

        let rows = ManagementScheduleLayout.rows(
            items: [
                ManagementScheduledItem(todo: first),
                ManagementScheduledItem(todo: second)
            ],
            mandatoryGroups: [[first.id, second.id]])

        #expect(rows.count == 1)
        #expect(rows[0].items.map(\.id) == [first.id, second.id])
        #expect(rows[0].items.allSatisfy { $0.isSpatiallyDraggable })
    }

    @Test("spatial target scale stays calm but perceptible")
    func spatialTargetScale() {
        #expect(AGMotion.spatialTargetScale >= 1.03)
        #expect(AGMotion.spatialTargetScale <= 1.035)
    }

    @Test("Mac form controls are not draggable window background")
    func macFormControlsCanReceiveInputFocus() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let chromeSource = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/AttentionGuardianMacApp/NativeWindowChrome.swift"),
            encoding: .utf8)

        #expect(chromeSource.contains(
            "window.isMovableByWindowBackground = false"))
    }

    @Test("Mac pointer drag does not require a stationary long press")
    func macPointerDragStartsFromMovement() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let managementSource = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/AttentionGuardianPresentation/ManagementSurface.swift"),
            encoding: .utf8)

        #expect(managementSource.contains(
            "private func macOSSpatialDragGesture"))
        #expect(managementSource.contains(
            "minimumDistance: AGMotion.spatialPointerDragDistance"))
        #expect(managementSource.contains(
            ".highPriorityGesture(macOSSpatialDragGesture("))
    }

    @Test("the full scheduled capsule is a pointer hit target")
    func scheduledCapsuleHasContinuousHitShape() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let managementSource = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/AttentionGuardianPresentation/ManagementSurface.swift"),
            encoding: .utf8)

        #expect(managementSource.contains(
            "bubble.contentShape(RoundedRectangle("))
    }

    @Test("spatial morph contracts toward the pointer press")
    func spatialMorphUsesPointerAnchor() throws {
        let packageRoot = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let managementSource = try String(
            contentsOf: packageRoot.appendingPathComponent(
                "Sources/AttentionGuardianPresentation/ManagementSurface.swift"),
            encoding: .utf8)

        #expect(managementSource.contains(
            "@State private var dragPressLocation: CGPoint?"))
        #expect(managementSource.contains(
            "anchor.x - origin.midX"))
        #expect(managementSource.contains(
            "anchor.y - origin.midY"))
    }
}

import SwiftUI

public enum BreakTimeEntityExpansionPhase: Equatable, Sendable {
    case collapsed
    case morphingOpen
    case expanded
    case hidingContent
}

public struct BreakTimeEntityState: Equatable, Sendable {
    public private(set) var expansionPhase:
        BreakTimeEntityExpansionPhase = .collapsed
    public var durationMinutes = 20
    public private(set) var dragPhase: SpatialDragPhase = .idle
    public private(set) var completedInsertions = 0

    private var dragMachine = SpatialDragMachine()

    public init() {}

    public var accessibilityValue: String {
        "休息，\(durationMinutes) 分钟"
    }

    public var isExpanded: Bool {
        expansionPhase != .collapsed
    }

    public var showsStepper: Bool {
        expansionPhase == .expanded
    }

    public var showsCup: Bool {
        expansionPhase == .collapsed
    }

    public var canBeginDrag: Bool {
        dragPhase == .idle
    }

    public mutating func beginExpansion() {
        guard canBeginDrag, expansionPhase == .collapsed else { return }
        expansionPhase = .morphingOpen
    }

    public mutating func completeExpansion() {
        guard expansionPhase == .morphingOpen else { return }
        expansionPhase = .expanded
    }

    public mutating func beginCollapse() {
        guard canBeginDrag, expansionPhase == .expanded else { return }
        expansionPhase = .hidingContent
    }

    public mutating func completeCollapse() {
        guard expansionPhase == .hidingContent else { return }
        expansionPhase = .collapsed
    }

    public mutating func adjustDuration(by delta: Int) {
        durationMinutes = min(180, max(1, durationMinutes + delta))
    }

    public mutating func beginDrag() {
        guard canBeginDrag else { return }
        dragMachine.press(todoId: Self.dragIdentity)
        dragMachine.lift(originIndex: -1)
        dragMachine.drag()
        dragPhase = dragMachine.phase
    }

    public mutating func magnetize(targetIndex: Int) {
        dragMachine.magnetize(targetIndex: targetIndex)
        dragPhase = dragMachine.phase
    }

    public mutating func clearMagnetization() {
        dragMachine.drag()
        dragPhase = dragMachine.phase
    }

    public mutating func release() {
        dragMachine.release()
        dragPhase = dragMachine.phase
    }

    public mutating func finishInsertion() {
        dragMachine.beginCommit()
        dragMachine.finish()
        dragPhase = dragMachine.phase
        if dragPhase == .idle {
            completedInsertions += 1
        }
    }

    public mutating func cancelDrag() {
        dragMachine.fail()
        dragMachine.finish()
        dragPhase = dragMachine.phase
    }

    private static let dragIdentity = UUID(
        uuidString: "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFE")!
}

struct BreakTimeEntity<SpatialGesture: Gesture>: View {
    @Environment(\.accessibilityReduceMotion)
    private var reduceMotion

    @Binding var state: BreakTimeEntityState
    let isDragging: Bool
    let spatialGesture: SpatialGesture

    @State private var rippleGeneration = 0
    @State private var morphGeneration = 0

    private var width: CGFloat {
        state.isExpanded ? 214 : 64
    }

    var body: some View {
        ZStack {
            glassBody
                .simultaneousGesture(spatialGesture)

            if state.showsCup {
                Image(systemName: "cup.and.saucer.fill")
                    .font(.system(size: 19, weight: .semibold))
                    .foregroundStyle(AGColor.rest)
                    .frame(
                        width: AGLayout.minimumTouchTarget,
                        height: AGLayout.minimumTouchTarget)
                    .allowsHitTesting(false)
                    .transition(.opacity)
            }

            if state.showsStepper {
                HStack(spacing: AGSpace.related) {
                    Button {
                        adjustDuration(by: -1)
                    } label: {
                        Image(systemName: "minus")
                            .frame(
                                width: AGLayout.minimumTouchTarget,
                                height: AGLayout.minimumTouchTarget)
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("减少休息时间")

                    Text("\(state.durationMinutes)")
                        .font(.headline.monospacedDigit())
                        .contentTransition(.numericText())
                        .frame(minWidth: 28)
                        .allowsHitTesting(false)
                        .accessibilityLabel(
                            "\(state.durationMinutes) 分钟")

                    Button {
                        adjustDuration(by: 1)
                    } label: {
                        Image(systemName: "plus")
                            .frame(
                                width: AGLayout.minimumTouchTarget,
                                height: AGLayout.minimumTouchTarget)
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("增加休息时间")
                }
                .transition(.opacity)
                .padding(.horizontal, AGSpace.compact)
            }
        }
        .frame(
            width: width,
            height: AGLayout.managementCapsuleHeight)
        .contentShape(Capsule())
        .opacity(isDragging ? 0.16 : 1)
        .scaleEffect(isDragging ? 0.97 : 1)
        .onTapGesture(count: 2, perform: toggleExpansion)
        .animation(
            reduceMotion ? nil : AGMotion.spatialSettle,
            value: state.isExpanded)
        .animation(
            reduceMotion ? nil : .easeOut(duration: 0.12),
            value: state.showsCup)
        .animation(
            reduceMotion ? nil : .easeOut(duration: 0.14),
            value: state.showsStepper)
        .animation(
            reduceMotion ? nil : AGMotion.spatialLift,
            value: isDragging)
        .accessibilityElement(children: state.showsStepper ? .contain : .ignore)
        .accessibilityLabel("休息")
        .accessibilityValue("\(state.durationMinutes) 分钟")
        .accessibilityHint("双击展开或收起时长设置；拖动可加入时间队列")
        .accessibilityAction(named: state.isExpanded ? "收起" : "展开") {
            toggleExpansion()
        }
    }

    private var glassBody: some View {
        let shape = Capsule(style: .continuous)

        return LiquidGlassSurface(shape: shape)
            .overlay {
                shape.fill(LinearGradient(
                    colors: [
                        AGColor.rest.opacity(0.035),
                        .clear,
                        AGColor.mist.opacity(0.025)
                    ],
                    startPoint: .bottomLeading,
                    endPoint: .topTrailing))
                    .blendMode(.plusLighter)
            }
            .overlay {
                shape.strokeBorder(
                    LinearGradient(
                        colors: [
                            .white.opacity(0.56),
                            .white.opacity(0.08),
                            AGColor.rest.opacity(0.12)
                        ],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing),
                    lineWidth: 0.8)
            }
            .overlay(alignment: .topLeading) {
                Capsule(style: .continuous)
                    .trim(from: 0.03, to: state.isExpanded ? 0.36 : 0.46)
                    .stroke(
                        .white.opacity(0.28),
                        style: StrokeStyle(
                            lineWidth: 1.1,
                            lineCap: .round))
                    .padding(3)
                    .blur(radius: 0.35)
            }
            .overlay {
                if rippleGeneration > 0 && !reduceMotion {
                    Circle()
                        .stroke(
                            LinearGradient(
                                colors: [
                                    .white.opacity(0.22),
                                    AGColor.rest.opacity(0.10),
                                    .clear
                                ],
                                startPoint: .topLeading,
                                endPoint: .bottomTrailing),
                            lineWidth: 0.8)
                        .padding(8)
                        .keyframeAnimator(
                            initialValue: RippleValues(),
                            trigger: rippleGeneration
                        ) { content, value in
                            content
                                .scaleEffect(value.scale)
                                .opacity(value.opacity)
                        } keyframes: { _ in
                            KeyframeTrack(\.scale) {
                                CubicKeyframe(0.72, duration: 0)
                                SpringKeyframe(
                                    1.48,
                                    duration: 0.42,
                                    spring: .smooth)
                            }
                            KeyframeTrack(\.opacity) {
                                CubicKeyframe(0.26, duration: 0)
                                CubicKeyframe(0, duration: 0.42)
                            }
                        }
                        .allowsHitTesting(false)
                }
            }
            .shadow(
                color: AGColor.rest.opacity(0.07),
                radius: 14,
                y: 2)
            .shadow(
                color: AGColor.ambientDeep.opacity(0.16),
                radius: 12,
                y: 7)
    }

    private func toggleExpansion() {
        rippleGeneration += 1
        morphGeneration += 1
        let generation = morphGeneration

        if state.expansionPhase == .collapsed {
            withAnimation(reduceMotion ? nil : AGMotion.spatialSettle) {
                state.beginExpansion()
            }
            Task {
                if !reduceMotion {
                    try? await Task.sleep(for: .milliseconds(230))
                }
                guard generation == morphGeneration else { return }
                withAnimation(reduceMotion ? nil : .easeOut(duration: 0.14)) {
                    state.completeExpansion()
                }
            }
        } else if state.expansionPhase == .expanded {
            withAnimation(reduceMotion ? nil : .easeOut(duration: 0.11)) {
                state.beginCollapse()
            }
            Task {
                if !reduceMotion {
                    try? await Task.sleep(for: .milliseconds(120))
                }
                guard generation == morphGeneration else { return }
                withAnimation(reduceMotion ? nil : AGMotion.spatialSettle) {
                    state.completeCollapse()
                }
            }
        }
    }

    private func adjustDuration(by delta: Int) {
        withAnimation(reduceMotion ? nil : AGMotion.spatialFollow) {
            state.adjustDuration(by: delta)
        }
    }
}

private struct RippleValues {
    var scale = 0.72
    var opacity = 0.26
}

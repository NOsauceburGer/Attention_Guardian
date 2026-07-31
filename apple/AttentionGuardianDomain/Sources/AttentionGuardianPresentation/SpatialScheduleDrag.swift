import SwiftUI

public struct ManagementDropPreview: Equatable, Sendable {
    public let requestedIndex: Int
    public let actualIndex: Int
    public let usedFallbackPosition: Bool

    public init(
        requestedIndex: Int,
        actualIndex: Int,
        usedFallbackPosition: Bool
    ) {
        self.requestedIndex = requestedIndex
        self.actualIndex = actualIndex
        self.usedFallbackPosition = usedFallbackPosition
    }
}

public struct ManagementReorderOutcome: Equatable, Sendable {
    public let actualIndex: Int
    public let usedFallbackPosition: Bool

    public init(
        actualIndex: Int,
        usedFallbackPosition: Bool
    ) {
        self.actualIndex = actualIndex
        self.usedFallbackPosition = usedFallbackPosition
    }
}

public struct ManagementResolvedDropTarget: Equatable, Sendable {
    public let requestedIndex: Int
    public let actualIndex: Int
    public let usedFallbackPosition: Bool

    public init(
        requestedIndex: Int,
        actualIndex: Int,
        usedFallbackPosition: Bool
    ) {
        self.requestedIndex = requestedIndex
        self.actualIndex = actualIndex
        self.usedFallbackPosition = usedFallbackPosition
    }
}

@MainActor
public enum SpatialDropReleasePlanner {
    public static func resolve(
        finalPointer: CGPoint,
        previousTarget: ManagementResolvedDropTarget?,
        rowFrames: [Int: CGRect],
        cachedPreviews: [ManagementDropPreview],
        loadPreviews: () async throws -> [ManagementDropPreview]
    ) async throws -> ManagementResolvedDropTarget? {
        let previews = cachedPreviews.isEmpty
            ? try await loadPreviews()
            : cachedPreviews
        return SpatialDropResolver.releaseTarget(
            finalPointer: finalPointer,
            previousTarget: previousTarget,
            rowFrames: rowFrames,
            previews: previews)
    }
}

public enum SpatialDragPhase: Equatable, Sendable {
    case idle
    case pressing(todoId: UUID)
    case lifting(todoId: UUID, originIndex: Int)
    case dragging(todoId: UUID, originIndex: Int)
    case magnetized(todoId: UUID, originIndex: Int, targetIndex: Int)
    case settling(todoId: UUID, originIndex: Int, targetIndex: Int)
    case committing(todoId: UUID, originIndex: Int, targetIndex: Int)
    case returning(todoId: UUID, originIndex: Int)
}

public struct SpatialDragSessionTracker: Equatable, Sendable {
    public private(set) var current: UUID?

    public init() {}

    @discardableResult
    public mutating func begin() -> UUID {
        let session = UUID()
        current = session
        return session
    }

    public func isCurrent(_ session: UUID) -> Bool {
        current == session
    }

    public mutating func finish(_ session: UUID) {
        guard isCurrent(session) else { return }
        current = nil
    }
}

public struct SpatialDragMachine: Equatable, Sendable {
    public private(set) var phase: SpatialDragPhase = .idle

    public init() {}

    public mutating func press(todoId: UUID) {
        guard phase == .idle else { return }
        phase = .pressing(todoId: todoId)
    }

    public mutating func lift(originIndex: Int) {
        guard case let .pressing(todoId) = phase else { return }
        phase = .lifting(todoId: todoId, originIndex: originIndex)
    }

    public mutating func drag() {
        switch phase {
        case let .lifting(todoId, originIndex),
             let .magnetized(todoId, originIndex, _):
            phase = .dragging(
                todoId: todoId,
                originIndex: originIndex)
        default:
            return
        }
    }

    public mutating func magnetize(targetIndex: Int) {
        switch phase {
        case let .dragging(todoId, originIndex),
             let .magnetized(todoId, originIndex, _):
            phase = .magnetized(
                todoId: todoId,
                originIndex: originIndex,
                targetIndex: targetIndex)
        default:
            return
        }
    }

    public mutating func release() {
        guard case let .magnetized(
            todoId,
            originIndex,
            targetIndex
        ) = phase else {
            return
        }
        phase = .settling(
            todoId: todoId,
            originIndex: originIndex,
            targetIndex: targetIndex)
    }

    public mutating func beginCommit() {
        guard case let .settling(
            todoId,
            originIndex,
            targetIndex
        ) = phase else {
            return
        }
        phase = .committing(
            todoId: todoId,
            originIndex: originIndex,
            targetIndex: targetIndex)
    }

    public mutating func fail() {
        switch phase {
        case let .lifting(todoId, originIndex),
             let .dragging(todoId, originIndex),
             let .magnetized(todoId, originIndex, _):
            phase = .returning(
                todoId: todoId,
                originIndex: originIndex)
        case let .settling(todoId, originIndex, _),
             let .committing(todoId, originIndex, _):
            phase = .returning(
                todoId: todoId,
                originIndex: originIndex)
        default:
            return
        }
    }

    public mutating func finish() {
        switch phase {
        case .committing, .returning:
            phase = .idle
        default:
            return
        }
    }
}

public enum SpatialDropResolver {
    public static func releaseTarget(
        finalPointer: CGPoint,
        previousTarget: ManagementResolvedDropTarget?,
        rowFrames: [Int: CGRect],
        previews: [ManagementDropPreview]
    ) -> ManagementResolvedDropTarget? {
        target(
            pointer: finalPointer,
            currentTarget: previousTarget,
            rowFrames: rowFrames,
            previews: previews,
            hysteresis: 0)
    }

    public static func target(
        pointer: CGPoint,
        currentTarget: ManagementResolvedDropTarget?,
        rowFrames: [Int: CGRect],
        previews: [ManagementDropPreview],
        hysteresis: CGFloat
    ) -> ManagementResolvedDropTarget? {
        if let currentTarget,
           let currentFrame = rowFrames[currentTarget.actualIndex],
           currentFrame.insetBy(
               dx: -hysteresis,
               dy: -hysteresis).contains(pointer) {
            return currentTarget
        }

        guard let requestedIndex = rowFrames.min(by: {
            distance(from: pointer, to: $0.value)
                < distance(from: pointer, to: $1.value)
        })?.key,
              let preview = previews.first(where: {
                  $0.requestedIndex == requestedIndex
              }) else {
            return nil
        }
        return ManagementResolvedDropTarget(
            requestedIndex: preview.requestedIndex,
            actualIndex: preview.actualIndex,
            usedFallbackPosition: preview.usedFallbackPosition)
    }

    public static func target(
        pointerY: CGFloat,
        currentTarget: ManagementResolvedDropTarget?,
        rowFrames: [Int: CGRect],
        previews: [ManagementDropPreview],
        hysteresis: CGFloat
    ) -> ManagementResolvedDropTarget? {
        let pointerX = rowFrames.values.first?.midX ?? 0
        return target(
            pointer: CGPoint(x: pointerX, y: pointerY),
            currentTarget: currentTarget,
            rowFrames: rowFrames,
            previews: previews,
            hysteresis: hysteresis)
    }

    public static func targetIndex(
        pointerY: CGFloat,
        currentTarget: Int?,
        rowFrames: [Int: CGRect],
        previews: [ManagementDropPreview],
        hysteresis: CGFloat
    ) -> Int? {
        let current = currentTarget.flatMap { index in
            previews.first(where: { $0.actualIndex == index }).map {
                ManagementResolvedDropTarget(
                    requestedIndex: $0.requestedIndex,
                    actualIndex: $0.actualIndex,
                    usedFallbackPosition: $0.usedFallbackPosition)
            }
        }
        return target(
            pointerY: pointerY,
            currentTarget: current,
            rowFrames: rowFrames,
            previews: previews,
            hysteresis: hysteresis)?
            .actualIndex
    }

    private static func distance(
        from point: CGPoint,
        to frame: CGRect
    ) -> CGFloat {
        let dx = max(frame.minX - point.x, 0, point.x - frame.maxX)
        let dy = max(frame.minY - point.y, 0, point.y - frame.maxY)
        return hypot(dx, dy)
    }
}

struct LensCoreBubble: View {
    let width: CGFloat
    let height: CGFloat
    let morphProgress: CGFloat
    let isMagnetized: Bool

    var body: some View {
        let cornerRadius = min(width, height) / 2
        let shape = RoundedRectangle(
            cornerRadius: cornerRadius,
            style: .continuous)

        LiquidGlassSurface(shape: shape)
            .overlay {
                shape.fill(RadialGradient(
                    colors: [
                        .white.opacity(0.07 * morphProgress),
                        AGColor.mist.opacity(0.025 * morphProgress),
                        .clear
                    ],
                    center: UnitPoint(x: 0.43, y: 0.39),
                    startRadius: 2,
                    endRadius: max(width, height) * 0.62))
            }
            .overlay {
                shape.strokeBorder(
                    LinearGradient(
                        colors: [
                            .white.opacity(isMagnetized ? 0.62 : 0.52),
                            .white.opacity(0.08),
                            .white.opacity(0.30)
                        ],
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing),
                    lineWidth: isMagnetized ? 1.05 : 0.8)
            }
            .overlay(alignment: .topLeading) {
                Ellipse()
                    .trim(from: 0.08, to: 0.46)
                    .stroke(
                        .white.opacity(0.32 * morphProgress),
                        lineWidth: 0.9)
                    .frame(
                        width: width * 0.56,
                        height: height * 0.31)
                    .padding(.leading, width * 0.19)
                    .padding(.top, height * 0.13)
                    .rotationEffect(.degrees(-10))
                    .blur(radius: 0.25)
            }
            .shadow(
                color: AGColor.ambientDeep.opacity(
                    0.10 + 0.03 * morphProgress),
                radius: 7 + 4 * morphProgress,
                y: 2 + 4 * morphProgress)
            .accessibilityHidden(true)
    }
}

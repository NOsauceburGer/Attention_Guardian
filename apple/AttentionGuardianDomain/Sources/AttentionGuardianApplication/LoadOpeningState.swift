import AttentionGuardianDomain

public struct OpeningState: Equatable, Sendable {
    public let currentTodo: ScheduledTodo?
    public let dueFutureTodos: [UnscheduledTodoRecord]
    public let mandatoryConflicts: [MandatoryConflict]
}

public struct LoadOpeningStateUseCase: Sendable {
    private let scheduledRepository: any ScheduledTodoRepository
    private let futureRepository: any FutureTodoRepository
    private let clock: any Clock

    public init(
        scheduledRepository: any ScheduledTodoRepository,
        futureRepository: any FutureTodoRepository,
        clock: any Clock
    ) {
        self.scheduledRepository = scheduledRepository
        self.futureRepository = futureRepository
        self.clock = clock
    }

    public func execute() async throws -> OpeningState {
        let now = clock.now
        let original = try await scheduledRepository.loadAll()
        let completed = TodoLifecycle.completeDue(original, at: now)
        if completed != original {
            try await scheduledRepository.replaceAll(completed)
        }
        let active = completed
            .filter { $0.status == .active }
            .map(\.todo)
        let future = try await futureRepository.loadAllActive()
        return OpeningState(
            currentTodo: ScheduledTodoSelector.current(in: active, at: now),
            dueFutureTodos: TodoLifecycle.dueFutureTodos(
                future,
                onOrBefore: try clock.localDate()),
            mandatoryConflicts: MandatoryConflictDetector.detect(
                in: active,
                endingAfter: now))
    }
}

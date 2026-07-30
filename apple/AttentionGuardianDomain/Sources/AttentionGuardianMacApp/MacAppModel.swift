#if os(macOS)
import Foundation
import SwiftUI
import AttentionGuardianApplication
import AttentionGuardianDomain
import AttentionGuardianInfrastructure
import AttentionGuardianPersistence
import AttentionGuardianPresentation

@MainActor
final class MacAppModel: ObservableObject {
    @Published private(set) var dashboardState: FocusDashboardState = .loading
    @Published var destination: GuardianDestination = .focus
    @Published private(set) var errorMessage: String?
    @Published private(set) var managedScheduledItems: [ManagementScheduledItem] = []
    @Published private(set) var managedFutureItems: [ManagementFutureItem]?
    @Published private(set) var isManagementLoading = false

    private var currentTitle: String?
    private var persistence: ApplePersistenceContainer?

    func loadOpeningState() async {
        guard persistence == nil else { return }
        do {
            let persistence = try ApplePersistenceContainer
                .openInApplicationSupport()
            self.persistence = persistence
            try await refreshOpeningState()
        } catch {
            dashboardState = .empty
            errorMessage = "无法读取本地事项。请重新打开应用后再试。"
        }
    }

    func startFocus() {
        guard let currentTitle else { return }
        dashboardState = .focused(title: currentTitle)
    }

    func saveScheduled(_ draft: ScheduledTodoDraft) async throws {
        guard let persistence else { throw MacAppError.persistenceUnavailable }
        let clock = SystemClock()
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = clock.timeZone
        let components = calendar.dateComponents(
            [.year, .month, .day],
            from: clock.now)
        guard let year = components.year,
              let month = components.month,
              let day = components.day else {
            throw MacAppError.invalidLocalTime
        }
        let local = String(
            format: "%04d-%02d-%02dT%02d:%02d:00",
            year, month, day, draft.hour, draft.minute)
        let resolution = try LocalDateTimeResolver.resolve(
            local,
            timeZoneId: clock.timeZone.identifier)
        guard case let .resolved(start, offset) = resolution else {
            throw MacAppError.invalidLocalTime
        }
        _ = try await AddScheduledTodoUseCase(
            repository: persistence.scheduledTodos,
            clock: clock)
            .execute(AddScheduledTodoRequest(
                id: UUID(),
                title: draft.title,
                start: start,
                duration: TimeInterval(
                    draft.durationHours * 3_600
                    + draft.durationMinutes * 60),
                utcOffsetSeconds: offset,
                isMandatory: draft.isMandatory))
        destination = .focus
        try await refreshOpeningState()
    }

    func saveFuture(_ draft: FutureTodoDraft) async throws {
        guard let persistence else { throw MacAppError.persistenceUnavailable }
        let selection: FutureTodoDateSelection
        switch draft.date {
        case let .relative(days):
            selection = .daysFromToday(days)
        case let .exact(date):
            selection = .exact(date)
        }
        _ = try await AddFutureTodoUseCase(
            repository: persistence.futureTodos,
            clock: SystemClock())
            .execute(AddFutureTodoRequest(
                id: UUID(),
                title: draft.title,
                dateSelection: selection,
                isMandatory: draft.isMandatory))
        destination = .focus
        try await refreshOpeningState()
    }

    func loadManagement() async {
        guard let persistence else { return }
        isManagementLoading = true
        managedFutureItems = nil
        defer { isManagementLoading = false }
        do {
            let todos = try await ScheduleManagementUseCase(
                repository: persistence.scheduledTodos,
                clock: SystemClock())
                .load()
            managedScheduledItems = todos.map(ManagementScheduledItem.init)
            errorMessage = nil
        } catch {
            errorMessage = "无法读取活动事项。请稍后重试。"
        }
    }

    func loadManagedFutureTodos() async {
        guard let persistence, managedFutureItems == nil else { return }
        do {
            let records = try await FutureTodoManagementUseCase(
                repository: persistence.futureTodos)
                .load()
            managedFutureItems = records.map(ManagementFutureItem.init)
        } catch {
            errorMessage = "无法读取未来待办。请稍后重试。"
        }
    }

    func deleteManagedScheduledTodo(_ id: UUID) async throws {
        guard let persistence else { throw MacAppError.persistenceUnavailable }
        let todos = try await ScheduleManagementUseCase(
            repository: persistence.scheduledTodos,
            clock: SystemClock())
            .delete(todoId: id)
        managedScheduledItems = todos.map(ManagementScheduledItem.init)
        try await refreshOpeningState()
    }

    func saveManagedScheduledTodo(
        _ draft: ManagementScheduledEditDraft,
        conflictResolution: StartTimeConflictResolution?
    ) async throws -> ManagementScheduledEditOutcome {
        guard let persistence else { throw MacAppError.persistenceUnavailable }
        guard let original = managedScheduledItems.first(
            where: { $0.id == draft.id })?.todo
        else {
            throw MacAppError.todoUnavailable
        }

        let clock = SystemClock()
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = clock.timeZone
        let components = calendar.dateComponents(
            [.year, .month, .day],
            from: original.start)
        guard let year = components.year,
              let month = components.month,
              let day = components.day else {
            throw MacAppError.invalidLocalTime
        }
        let local = String(
            format: "%04d-%02d-%02dT%02d:%02d:00",
            year, month, day, draft.hour, draft.minute)
        let resolution = try LocalDateTimeResolver.resolve(
            local,
            timeZoneId: clock.timeZone.identifier)
        guard case let .resolved(newStart, _) = resolution else {
            throw MacAppError.invalidLocalTime
        }

        let result = try await ScheduleManagementUseCase(
            repository: persistence.scheduledTodos,
            clock: clock)
            .edit(
                todoId: draft.id,
                title: draft.title,
                duration: TimeInterval(
                    draft.durationHours * 3_600
                    + draft.durationMinutes * 60),
                isMandatory: draft.isMandatory,
                newStart: newStart,
                conflictResolution: conflictResolution)

        let conflictingTitle = result.conflictingTodoId.flatMap { id in
            managedScheduledItems.first { $0.id == id }?.title
        } ?? "另一事项"
        switch result.rejection {
        case .none:
            managedScheduledItems = result.scheduledTodos.map(
                ManagementScheduledItem.init)
            try await refreshOpeningState()
            return .saved
        case .conflictResolutionRequired:
            return .conflictResolutionRequired(
                conflictingTitle: conflictingTitle)
        case .mandatoryTodoOccupiesNewStart:
            return .mandatoryStartRejected(
                conflictingTitle: conflictingTitle)
        }
    }

    func deleteManagedFutureTodo(_ id: UUID) async throws {
        guard let persistence else { throw MacAppError.persistenceUnavailable }
        let records = try await FutureTodoManagementUseCase(
            repository: persistence.futureTodos)
            .delete(todoId: id, confirmed: true)
        managedFutureItems = records.map(ManagementFutureItem.init)
    }

    private func refreshOpeningState() async throws {
        guard let persistence else { throw MacAppError.persistenceUnavailable }
        let opening = try await LoadOpeningStateUseCase(
            scheduledRepository: persistence.scheduledTodos,
            futureRepository: persistence.futureTodos,
            clock: SystemClock())
            .execute()
        currentTitle = opening.currentTodo?.title
        dashboardState = currentTitle.map {
            .ready(title: $0)
        } ?? .empty
        errorMessage = nil
    }
}

private enum MacAppError: Error {
    case persistenceUnavailable
    case invalidLocalTime
    case todoUnavailable
}
#endif

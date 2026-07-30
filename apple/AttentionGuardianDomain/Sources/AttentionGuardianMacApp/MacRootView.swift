#if os(macOS)
import SwiftUI
import AttentionGuardianPresentation

struct MacRootView: View {
    @ObservedObject var model: MacAppModel

    var body: some View {
        Group {
            switch model.destination {
            case .focus:
                FocusDashboard(
                    state: model.dashboardState,
                    destination: $model.destination,
                    onStart: model.startFocus)
            case .add:
                AddEventFlow(
                    onCancel: { model.destination = .focus },
                    onSaveScheduled: model.saveScheduled,
                    onSaveFuture: model.saveFuture)
            case .manage:
                ManagementSurface(
                    scheduledItems: model.managedScheduledItems,
                    futureItems: model.managedFutureItems,
                    isLoading: model.isManagementLoading,
                    onLoadFutureTodos: model.loadManagedFutureTodos,
                    onSaveScheduled: model.saveManagedScheduledTodo,
                    onPreviewReorder: model.previewManagedScheduledReorder,
                    onReorder: model.reorderManagedScheduledTodo,
                    onDeleteScheduled: model.deleteManagedScheduledTodo,
                    onDeleteFuture: model.deleteManagedFutureTodo,
                    onBack: { model.destination = .focus })
                    .task {
                        await model.loadManagement()
                    }
            }
        }
        .background {
            NativeWindowChrome()
        }
        .alert(
            "本地数据暂时不可用",
            isPresented: Binding(
                get: { model.errorMessage != nil },
                set: { _ in })
        ) {
            Button("知道了", role: .cancel) {}
        } message: {
            Text(model.errorMessage ?? "")
        }
    }

}
#endif

#if os(macOS)
import SwiftUI

@main
struct AttentionGuardianMacApp: App {
    @StateObject private var model = MacAppModel()

    var body: some Scene {
        WindowGroup("Attention Guardian") {
            MacRootView(model: model)
                .task {
                    await model.loadOpeningState()
                }
        }
        .windowStyle(.hiddenTitleBar)
    }
}
#endif

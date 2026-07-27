using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed record SavedFocusSession(
    string CurrentTask,
    FixedEvent NextEvent);

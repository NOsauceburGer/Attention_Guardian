using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public enum FocusSessionStatus
{
    Focusing,
    Handoff
}

public sealed record FocusSession(
    string CurrentTask,
    FixedEvent NextEvent,
    DateTimeOffset SafeUntil,
    FocusSessionStatus Status);

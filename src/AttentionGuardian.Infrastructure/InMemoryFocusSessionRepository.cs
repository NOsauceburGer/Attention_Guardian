using AttentionGuardian.Application;

namespace AttentionGuardian.Infrastructure;

public sealed class InMemoryFocusSessionRepository : IFocusSessionRepository
{
    private SavedFocusSession? current;

    public Task SaveAsync(
        SavedFocusSession session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        current = session;
        return Task.CompletedTask;
    }

    public Task<SavedFocusSession?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(current);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        current = null;
        return Task.CompletedTask;
    }
}

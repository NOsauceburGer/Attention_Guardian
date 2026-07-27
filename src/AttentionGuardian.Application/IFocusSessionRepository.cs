namespace AttentionGuardian.Application;

public interface IFocusSessionRepository
{
    Task SaveAsync(
        SavedFocusSession session,
        CancellationToken cancellationToken = default);

    Task<SavedFocusSession?> LoadAsync(
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        CancellationToken cancellationToken = default);
}

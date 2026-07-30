using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public sealed class FocusSessionCoordinator(
    FocusSessionService sessionService,
    IFocusSessionRepository repository)
{
    public FocusSession? Current => sessionService.Current;

    public async Task<FocusSession> StartAsync(
        StartFocusSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = sessionService.Start(request);
        await SaveAsync(session, cancellationToken);
        return session;
    }

    public async Task<FocusSession> StartAsync(
        string currentTask,
        IEnumerable<FixedEvent> fixedEvents,
        CancellationToken cancellationToken = default)
    {
        var session = sessionService.Start(currentTask, fixedEvents);
        await SaveAsync(session, cancellationToken);
        return session;
    }

    public async Task<FocusSession?> RestoreAsync(
        CancellationToken cancellationToken = default)
    {
        var saved = await repository.LoadAsync(cancellationToken);
        return saved is null
            ? null
            : sessionService.Restore(saved.CurrentTask, saved.NextEvent);
    }

    public FocusSession? Refresh() => sessionService.Refresh();

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await repository.ClearAsync(cancellationToken);
        sessionService.Clear();
    }

    private async Task SaveAsync(
        FocusSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.SaveAsync(
                new SavedFocusSession(session.CurrentTask, session.NextEvent),
                cancellationToken);
        }
        catch
        {
            sessionService.Clear();
            throw;
        }
    }
}

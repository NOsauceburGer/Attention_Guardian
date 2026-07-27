using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public interface IUnscheduledTodoRepository
{
    Task<IReadOnlyList<UnscheduledTodo>> LoadAllActiveAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnscheduledTodo>> LoadByDateAsync(
        DateOnly scheduledDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UnscheduledTodo>> LoadDueOnOrBeforeAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<UnscheduledTodo?> LoadActiveByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        UnscheduledTodo todo,
        CancellationToken cancellationToken = default);

    Task UpdateActiveAsync(
        UnscheduledTodo todo,
        CancellationToken cancellationToken = default);

    Task MarkPlannedAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task MarkDeletedAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

using AttentionGuardian.Core;

namespace AttentionGuardian.Application;

public interface IScheduledTodoRepository
{
    Task<IReadOnlyList<ScheduledTodo>> LoadAllAsync(
        CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(
        IReadOnlyList<ScheduledTodo> scheduledTodos,
        CancellationToken cancellationToken = default);

    Task MarkCompletedBeforeAsync(
        DateTimeOffset completedBefore,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

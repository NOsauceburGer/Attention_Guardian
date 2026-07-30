using AttentionGuardian.Core;
using AttentionGuardian.Desktop.ViewModels;

namespace AttentionGuardian.Desktop.Tests;

public sealed class BubbleViewModelTests
{
    [Fact]
    public async Task ScheduledBubble_FirstToggleOnlyExpands()
    {
        var saveCount = 0;
        var bubble = ScheduledBubble(
            _ =>
            {
                saveCount++;
                return Task.FromResult(true);
            });

        await bubble.ToggleExpandedAsync();

        Assert.True(bubble.IsExpanded);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public async Task ScheduledBubble_SecondToggleSavesChangedBufferThenCollapses()
    {
        ScheduledTodoBubbleViewModel? saved = null;
        var bubble = ScheduledBubble(
            candidate =>
            {
                saved = candidate;
                return Task.FromResult(true);
            });
        await bubble.ToggleExpandedAsync();
        bubble.Title = "修改后";
        bubble.DurationMinutes = 45;

        await bubble.ToggleExpandedAsync();

        Assert.Same(bubble, saved);
        Assert.False(bubble.IsExpanded);
        Assert.True(bubble.HasChanges);
    }

    [Fact]
    public async Task ScheduledBubble_SaveFailureKeepsEditorOpen()
    {
        var bubble = ScheduledBubble(_ => Task.FromResult(false));
        await bubble.ToggleExpandedAsync();
        bubble.Title = "无法保存";

        await bubble.ToggleExpandedAsync();

        Assert.True(bubble.IsExpanded);
    }

    [Fact]
    public void ScheduledBubble_DeleteRequestsOnlySelectedBubble()
    {
        ScheduledTodoBubbleViewModel? requested = null;
        var bubble = ScheduledBubble(
            _ => Task.FromResult(true),
            candidate => requested = candidate);

        bubble.DeleteCommand.Execute(null);

        Assert.Same(bubble, requested);
    }

    [Fact]
    public async Task FutureBubble_TracksBufferedNameAndDateUntilSave()
    {
        FutureTodoBubbleViewModel? saved = null;
        var model = new UnscheduledTodo(
            Guid.NewGuid(),
            "未来事项",
            new DateOnly(2026, 7, 27));
        var bubble = new FutureTodoBubbleViewModel(
            model,
            candidate =>
            {
                saved = candidate;
                return Task.FromResult(true);
            },
            _ => { });
        await bubble.ToggleExpandedAsync();
        bubble.Title = "修改后";
        bubble.ScheduledDay = 29;

        await bubble.ToggleExpandedAsync();

        Assert.Same(bubble, saved);
        Assert.False(bubble.IsExpanded);
        Assert.Equal("未来事项", model.Title);
        Assert.Equal(new DateOnly(2026, 7, 27), model.ScheduledDate);
    }

    [Fact]
    public void BreakBubble_IsRecognizedAsNonRenameablePresentation()
    {
        var start = At(9, 0);
        var model = new ScheduledTodo(
            Guid.NewGuid(),
            ScheduleManagement.BreakTitle,
            new TimeRange(start, start.AddMinutes(20)));
        var bubble = new ScheduledTodoBubbleViewModel(
            model,
            _ => Task.FromResult(true),
            _ => { });

        Assert.True(bubble.IsBreak);
        Assert.Equal("休息", bubble.Title);
        Assert.False(bubble.IsMandatory);
    }

    private static ScheduledTodoBubbleViewModel ScheduledBubble(
        Func<ScheduledTodoBubbleViewModel, Task<bool>> save,
        Action<ScheduledTodoBubbleViewModel>? delete = null)
    {
        var start = At(9, 0);
        return new ScheduledTodoBubbleViewModel(
            new ScheduledTodo(
                Guid.NewGuid(),
                "原名称",
                new TimeRange(start, start.AddMinutes(30))),
            save,
            delete ?? (_ => { }));
    }

    private static DateTimeOffset At(int hour, int minute) =>
        new(2026, 7, 26, hour, minute, 0, TimeSpan.FromHours(8));
}

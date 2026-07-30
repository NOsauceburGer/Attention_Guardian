using System.Collections.Generic;
using System.Linq;

namespace AttentionGuardian.Desktop.ViewModels;

public sealed class ScheduledTodoRowViewModel(
    IEnumerable<ScheduledTodoBubbleViewModel> items,
    bool isMandatoryGroup) : ViewModelBase
{
    public IReadOnlyList<ScheduledTodoBubbleViewModel> Items { get; } =
        items.ToArray();

    public bool IsMandatoryGroup { get; } = isMandatoryGroup;
}

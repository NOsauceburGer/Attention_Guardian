using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AttentionGuardian.Application;
using AttentionGuardian.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttentionGuardian.Desktop.ViewModels;

public enum DesktopPage
{
    Focus,
    AddType,
    AddScheduled,
    AddFuture,
    Manage,
    Acceptance
}

public enum ConfirmationKind
{
    None,
    DeleteScheduled,
    DeleteFuture,
    LeaveManagement
}

public partial class MainViewModel(
    TodoPlanningService planningService,
    ScheduleManagementService managementService,
    IScheduledTodoRepository scheduledRepository,
    TimeProvider timeProvider) : ViewModelBase
{
    private Guid? futureTodoBeingPlannedId;
    private readonly HandoffReminderService handoffReminderService =
        new(scheduledRepository, timeProvider);

    public MainViewModel()
        : this(
            new TodoPlanningService(
                new PreviewScheduledRepository(),
                new PreviewUnscheduledRepository(),
                TimeProvider.System),
            new ScheduleManagementService(
                new PreviewScheduledRepository(),
                new PreviewUnscheduledRepository(),
                TimeProvider.System),
            new PreviewScheduledRepository(),
            TimeProvider.System)
    {
    }

    [ObservableProperty] public partial DesktopPage CurrentPage { get; set; } = DesktopPage.Focus;
    [ObservableProperty] public partial bool IsDrawerOpen { get; set; }
    [ObservableProperty] public partial bool IsFocusStarted { get; set; }
    [ObservableProperty] public partial bool IsDueExpanded { get; set; }
    [ObservableProperty] public partial bool IsConflictExpanded { get; set; }
    [ObservableProperty] public partial bool IsFutureExpanded { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string ScheduledTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial int ScheduledHour { get; set; } = DateTimeOffset.Now.Hour;
    [ObservableProperty] public partial int ScheduledMinute { get; set; } = DateTimeOffset.Now.Minute;
    [ObservableProperty] public partial decimal DurationHours { get; set; }
    [ObservableProperty] public partial decimal DurationMinutes { get; set; } = 30;
    [ObservableProperty] public partial bool IsMandatory { get; set; }
    [ObservableProperty] public partial string FutureTitle { get; set; } = string.Empty;
    [ObservableProperty]
    public partial int FutureYear { get; set; } =
        timeProvider.GetLocalNow().AddDays(1).Year;
    [ObservableProperty]
    public partial int FutureMonth { get; set; } =
        timeProvider.GetLocalNow().AddDays(1).Month;
    [ObservableProperty]
    public partial int FutureDay { get; set; } =
        timeProvider.GetLocalNow().AddDays(1).Day;
    [ObservableProperty] public partial int? FutureDaysFromToday { get; set; } = 1;
    [ObservableProperty] public partial ScheduledTodo? CurrentTodo { get; set; }
    [ObservableProperty] public partial bool HasCrossDayNotice { get; set; }
    [ObservableProperty] public partial bool IsShiftToastVisible { get; set; }
    [ObservableProperty] public partial int RestDurationHours { get; set; }
    [ObservableProperty] public partial int RestDurationMinutes { get; set; } = 20;
    [ObservableProperty] public partial ConfirmationKind PendingConfirmation { get; set; }
    [ObservableProperty] public partial string ConfirmationTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string ConfirmationMessage { get; set; } = string.Empty;

    public ObservableCollection<UnscheduledTodo> DueTodos { get; } = [];
    public ObservableCollection<ScheduledTodoBubbleViewModel> ScheduledTodos { get; } = [];
    public ObservableCollection<ScheduledTodoRowViewModel> ScheduledRows { get; } = [];
    public ObservableCollection<FutureTodoBubbleViewModel> FutureTodos { get; } = [];
    public ObservableCollection<ScheduleConflict> Conflicts { get; } = [];
    public ObservableCollection<AcceptanceScenarioResult> AcceptanceResults { get; } = [];
    public IReadOnlyList<int> StartHourOptions { get; } =
        Enumerable.Range(0, 24).ToArray();
    public IReadOnlyList<int> StartMinuteOptions { get; } =
        Enumerable.Range(0, 60).ToArray();
    public IReadOnlyList<int> RestHourOptions { get; } =
        Enumerable.Range(0, 49).ToArray();
    public IReadOnlyList<int> RestMinuteOptions { get; } =
        Enumerable.Range(0, 60).ToArray();
    public IReadOnlyList<int> FutureYearOptions { get; } =
        Enumerable.Range(timeProvider.GetLocalNow().Year, 11).ToArray();
    public IReadOnlyList<int> FutureMonthOptions { get; } =
        Enumerable.Range(1, 12).ToArray();
    public IReadOnlyList<int> FutureDayOptions =>
        Enumerable.Range(1, DateTime.DaysInMonth(FutureYear, FutureMonth)).ToArray();

    public bool IsFocusPage => CurrentPage == DesktopPage.Focus;
    public bool IsAddTypePage => CurrentPage == DesktopPage.AddType;
    public bool IsAddScheduledPage => CurrentPage == DesktopPage.AddScheduled;
    public bool IsAddFuturePage => CurrentPage == DesktopPage.AddFuture;
    public bool IsManagePage => CurrentPage == DesktopPage.Manage;
    public bool IsAcceptancePage => CurrentPage == DesktopPage.Acceptance;
    public bool IsAcceptanceAvailable
    {
        get
        {
#if DEBUG
            return true;
#else
            return Environment.GetEnvironmentVariable(
                "ATTENTION_GUARDIAN_ENABLE_ACCEPTANCE") == "1";
#endif
        }
    }
    public double DrawerWidth => IsAcceptanceAvailable ? 500 : 360;
    public bool HasAcceptanceResults => AcceptanceResults.Count > 0;
    public bool HasCurrentTodo => CurrentTodo is not null;
    public bool CanStartFocus => CurrentTodo is not null && !IsFocusStarted;
    public bool ShowFocusedTodo => CurrentTodo is not null && IsFocusStarted;
    public bool ShowEmptyFocus => CurrentTodo is null;
    public bool HasDueTodos => DueTodos.Count > 0;
    public bool HasConflicts => Conflicts.Count > 0;
    public bool HasError => ErrorMessage.Length > 0;
    public bool HasMandatoryGroups => ScheduledRows.Any(row => row.IsMandatoryGroup);
    public bool IsConfirmationOpen => PendingConfirmation != ConfirmationKind.None;
    public bool CanDiscardConfirmation => PendingConfirmation == ConfirmationKind.LeaveManagement;
    public bool IsFutureOneDaySelected => FutureDaysFromToday == 1;
    public bool IsFutureTwoDaysSelected => FutureDaysFromToday == 2;
    public string CurrentTodoTitle => CurrentTodo?.Title ?? string.Empty;
    public string CurrentTodoKey => CurrentTodo?.Id.ToString() ?? string.Empty;

    partial void OnCurrentPageChanged(DesktopPage value)
    {
        OnPropertyChanged(nameof(IsFocusPage));
        OnPropertyChanged(nameof(IsAddTypePage));
        OnPropertyChanged(nameof(IsAddScheduledPage));
        OnPropertyChanged(nameof(IsAddFuturePage));
        OnPropertyChanged(nameof(IsManagePage));
        OnPropertyChanged(nameof(IsAcceptancePage));
    }

    partial void OnCurrentTodoChanged(ScheduledTodo? value)
    {
        OnPropertyChanged(nameof(HasCurrentTodo));
        OnPropertyChanged(nameof(CanStartFocus));
        OnPropertyChanged(nameof(ShowFocusedTodo));
        OnPropertyChanged(nameof(ShowEmptyFocus));
        OnPropertyChanged(nameof(CurrentTodoTitle));
        OnPropertyChanged(nameof(CurrentTodoKey));
    }

    partial void OnIsFocusStartedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartFocus));
        OnPropertyChanged(nameof(ShowFocusedTodo));
    }

    partial void OnFutureYearChanged(int value)
    {
        RefreshFutureDayOptions();
        FutureDaysFromToday = null;
    }

    partial void OnFutureMonthChanged(int value)
    {
        RefreshFutureDayOptions();
        FutureDaysFromToday = null;
    }

    partial void OnFutureDayChanged(int value) => FutureDaysFromToday = null;

    partial void OnFutureDaysFromTodayChanged(int? value)
    {
        OnPropertyChanged(nameof(IsFutureOneDaySelected));
        OnPropertyChanged(nameof(IsFutureTwoDaysSelected));
    }

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnPendingConfirmationChanged(ConfirmationKind value)
    {
        OnPropertyChanged(nameof(IsConfirmationOpen));
        OnPropertyChanged(nameof(CanDiscardConfirmation));
    }

    public async Task InitializeAsync() => await ReloadOpeningAsync();

    public async Task RefreshCurrentAsync()
    {
        if (CurrentPage != DesktopPage.Focus)
        {
            return;
        }

        var now = timeProvider.GetLocalNow();
        await scheduledRepository.MarkCompletedBeforeAsync(now);
        var schedule = await scheduledRepository.LoadAllAsync();
        CurrentTodo = ScheduledTodoSelector.GetCurrent(schedule, now);
    }

    public Task<PendingHandoffReminder?> GetPendingHandoffReminderAsync() =>
        handoffReminderService.GetPendingAsync();

    [RelayCommand]
    private void StartFocus()
    {
        if (CurrentTodo is not null)
        {
            IsFocusStarted = true;
        }
    }

    [RelayCommand] private void ToggleDue() => IsDueExpanded = !IsDueExpanded;
    [RelayCommand] private void ToggleConflict() => IsConflictExpanded = !IsConflictExpanded;
    [RelayCommand] private void CloseDrawer() => IsDrawerOpen = false;
    [RelayCommand] private void CloseCrossDay() => HasCrossDayNotice = false;

    [RelayCommand]
    private void OpenAdd()
    {
        IsDrawerOpen = false;
        CurrentPage = DesktopPage.AddType;
    }

    [RelayCommand]
    private async Task OpenManageAsync()
    {
        IsDrawerOpen = false;
        await LoadManagementAsync();
        CurrentPage = DesktopPage.Manage;
    }

    [RelayCommand]
    private void OpenAcceptance()
    {
        if (!IsAcceptanceAvailable)
        {
            return;
        }

        IsDrawerOpen = false;
        CurrentPage = DesktopPage.Acceptance;
    }

    [RelayCommand]
    private void RunAcceptance()
    {
        AcceptanceResults.Clear();
        foreach (var result in AcceptanceScenarioRunner.RunAll())
        {
            AcceptanceResults.Add(result);
        }

        OnPropertyChanged(nameof(HasAcceptanceResults));
    }

    [RelayCommand]
    private void ChooseScheduled()
    {
        futureTodoBeingPlannedId = null;
        var now = timeProvider.GetLocalNow();
        ScheduledTitle = string.Empty;
        ScheduledHour = now.Hour;
        ScheduledMinute = now.Minute;
        CurrentPage = DesktopPage.AddScheduled;
    }

    [RelayCommand]
    private void PlanDueTodo(UnscheduledTodo todo)
    {
        ArgumentNullException.ThrowIfNull(todo);

        var now = timeProvider.GetLocalNow();
        futureTodoBeingPlannedId = todo.Id;
        ScheduledTitle = todo.Title;
        ScheduledHour = now.Hour;
        ScheduledMinute = now.Minute;
        IsMandatory = todo.IsMandatory;
        ErrorMessage = string.Empty;
        CurrentPage = DesktopPage.AddScheduled;
    }

    [RelayCommand]
    private void ChooseFuture()
    {
        FutureTitle = string.Empty;
        SelectFutureRelativeDate(1);
        CurrentPage = DesktopPage.AddFuture;
    }

    [RelayCommand]
    private void SelectFutureOneDay() => SelectFutureRelativeDate(1);

    [RelayCommand]
    private void SelectFutureTwoDays() => SelectFutureRelativeDate(2);

    [RelayCommand]
    private async Task BackAsync()
    {
        if (CurrentPage == DesktopPage.Manage && HasUnsavedManagementChanges())
        {
            PendingConfirmation = ConfirmationKind.LeaveManagement;
            ConfirmationTitle = "保存管理页修改？";
            ConfirmationMessage = "有尚未收起保存的事件。保存后离开，或放弃这些修改。";
            return;
        }

        ErrorMessage = string.Empty;
        futureTodoBeingPlannedId = null;
        CurrentPage = DesktopPage.Focus;
        await ReloadOpeningAsync();
    }

    [RelayCommand]
    private async Task SaveScheduledAsync()
    {
        await RunAsync(async () =>
        {
            var date = timeProvider.GetLocalNow();
            var time = new TimeSpan(ScheduledHour, ScheduledMinute, 0);
            var local = date.Date + time;
            var start = LocalDateTimeResolver.Resolve(local, TimeZoneInfo.Local);
            var duration = TimeSpan.FromHours((double)DurationHours)
                + TimeSpan.FromMinutes((double)DurationMinutes);
            var result = futureTodoBeingPlannedId is { } sourceId
                ? await planningService.PlanUnscheduledTodoAsync(
                    new PlanUnscheduledTodoRequest(sourceId, duration, start))
                : await planningService.AddScheduledTodoAsync(
                    new AddScheduledTodoRequest(
                        Guid.NewGuid(),
                        ScheduledTitle,
                        duration,
                        start,
                        IsMandatory));
            futureTodoBeingPlannedId = null;
            ReplaceConflicts(result.Conflicts);

            HasCrossDayNotice |= result.HasRolloverToNextDay;
            IsShiftToastVisible = result.ScheduledTodos.Count > 1;
            CurrentPage = DesktopPage.Focus;
            await ReloadOpeningAsync();
        });
    }

    [RelayCommand]
    private async Task SaveFutureAsync()
    {
        await RunAsync(async () =>
        {
            var request = FutureDaysFromToday is { } daysFromToday
                ? new AddUnscheduledTodoRequest(
                    Guid.NewGuid(),
                    FutureTitle,
                    DaysFromToday: daysFromToday)
                : new AddUnscheduledTodoRequest(
                    Guid.NewGuid(),
                    FutureTitle,
                    ScheduledDate: new DateOnly(FutureYear, FutureMonth, FutureDay));
            await planningService.AddUnscheduledTodoAsync(
                request);
            CurrentPage = DesktopPage.Focus;
            await ReloadOpeningAsync();
        });
    }

    private void SelectFutureRelativeDate(int daysFromToday)
    {
        var date = timeProvider.GetLocalNow().AddDays(daysFromToday);
        FutureYear = date.Year;
        FutureMonth = date.Month;
        FutureDay = date.Day;
        FutureDaysFromToday = daysFromToday;
    }

    private void RefreshFutureDayOptions()
    {
        var lastDay = DateTime.DaysInMonth(FutureYear, FutureMonth);
        if (FutureDay > lastDay)
        {
            FutureDay = lastDay;
        }

        OnPropertyChanged(nameof(FutureDayOptions));
    }

    [RelayCommand]
    private async Task ToggleFutureAsync()
    {
        IsFutureExpanded = !IsFutureExpanded;
        if (!IsFutureExpanded)
        {
            return;
        }

        FutureTodos.Clear();
        foreach (var todo in await managementService.LoadFutureTodosAsync())
        {
            FutureTodos.Add(CreateFutureBubble(todo));
        }
    }

    [RelayCommand]
    private async Task AddRestAsync()
    {
        await AddRestAtIndexAsync(ScheduledTodos.Count);
    }

    public async Task<bool> AddRestAtIndexAsync(int requestedIndex)
    {
        ErrorMessage = string.Empty;
        var duration = TimeSpan.FromHours(RestDurationHours)
            + TimeSpan.FromMinutes(RestDurationMinutes);
        if (duration <= TimeSpan.Zero)
        {
            ErrorMessage = "休息时长必须大于零";
            return false;
        }

        if (requestedIndex < 0 || requestedIndex > ScheduledTodos.Count)
        {
            ErrorMessage = "休息的目标位置无效";
            return false;
        }

        var start = requestedIndex < ScheduledTodos.Count
            ? ScheduledTodos[requestedIndex].Model.TimeRange.Start
            : ScheduledTodos.LastOrDefault()?.Model.TimeRange.End
                ?? timeProvider.GetLocalNow();
        var id = Guid.NewGuid();

        try
        {
            var result = await managementService.AddBreakAsync(
                id,
                start,
                duration);
            ReplaceScheduledBubbles(result.ScheduledTodos);
            var actualIndex = ScheduledTodos
                .Select((bubble, index) => (bubble, index))
                .Single(item => item.bubble.Id == id)
                .index;
            if (actualIndex != requestedIndex)
            {
                ErrorMessage = "这个事件没办法放在这里";
            }

            HasCrossDayNotice |= result.HasRolloverToNextDay;
            IsShiftToastVisible = result.ScheduledTodos.Count > 1;
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
    }

    private async Task ReloadOpeningAsync()
    {
        var state = await planningService.LoadOpeningStateAsync();
        CurrentTodo = state.CurrentTodo;
        DueTodos.Clear();
        foreach (var todo in state.DueUnscheduledTodos)
        {
            DueTodos.Add(todo);
        }

        ReplaceConflicts(state.MandatoryConflicts);
        OnPropertyChanged(nameof(HasDueTodos));
    }

    private void ReplaceConflicts(IEnumerable<ScheduleConflict> conflicts)
    {
        Conflicts.Clear();
        foreach (var conflict in conflicts)
        {
            Conflicts.Add(conflict);
        }

        OnPropertyChanged(nameof(HasConflicts));
        if (!HasConflicts)
        {
            IsConflictExpanded = false;
        }
    }

    private async Task LoadManagementAsync()
    {
        var state = await managementService.LoadAsync();
        ReplaceScheduledBubbles(state.ScheduledTodos);
    }

    public async Task<bool> ReorderScheduledAsync(Guid todoId, int requestedIndex)
    {
        ErrorMessage = string.Empty;
        try
        {
            var result = await managementService.ReorderAsync(todoId, requestedIndex);
            ReplaceScheduledBubbles(result.ScheduledTodos);
            if (result.UsedFallbackPosition)
            {
                ErrorMessage = "这个事件没办法放在这里";
            }

            HasCrossDayNotice |= result.HasRolloverToNextDay;
            return true;
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = "这个事件没办法放在这里";
            return false;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
    }

    public Task<bool> MoveScheduledByOffsetAsync(
        ScheduledTodoBubbleViewModel bubble,
        int offset)
    {
        var currentIndex = ScheduledTodos.IndexOf(bubble);
        if (currentIndex < 0)
        {
            return Task.FromResult(false);
        }

        var targetIndex = Math.Clamp(
            currentIndex + offset,
            0,
            ScheduledTodos.Count - 1);
        if (targetIndex == currentIndex)
        {
            return Task.FromResult(true);
        }

        return ReorderScheduledAsync(bubble.Id, targetIndex);
    }

    private ScheduledTodoBubbleViewModel CreateScheduledBubble(ScheduledTodo todo) =>
        new(todo, SaveScheduledBubbleAsync, RequestDeleteScheduled);

    private FutureTodoBubbleViewModel CreateFutureBubble(UnscheduledTodo todo) =>
        new(todo, SaveFutureBubbleAsync, RequestDeleteFuture);

    private async Task<bool> SaveScheduledBubbleAsync(ScheduledTodoBubbleViewModel bubble)
    {
        ErrorMessage = string.Empty;
        try
        {
            var schedule = await managementService.EditAsync(
                new EditScheduledTodoRequest(
                    bubble.Id,
                    bubble.IsBreak ? ScheduleManagement.BreakTitle : bubble.Title,
                    bubble.Duration,
                    bubble.IsMandatory));
            ReplaceScheduledBubbles(schedule);
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
    }

    private async Task<bool> SaveFutureBubbleAsync(FutureTodoBubbleViewModel bubble)
    {
        ErrorMessage = string.Empty;
        try
        {
            var updated = await managementService.UpdateFutureTodoAsync(
                bubble.Id,
                bubble.Title,
                bubble.SelectedDate);
            bubble.AcceptSavedModel(updated);
            return true;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return false;
        }
    }

    private void RequestDeleteScheduled(ScheduledTodoBubbleViewModel bubble)
    {
        pendingScheduledDelete = bubble;
        ConfirmationTitle = "删除事件？";
        ConfirmationMessage = $"将删除“{bubble.Model.Title}”，并让后续事件向前补位。";
        PendingConfirmation = ConfirmationKind.DeleteScheduled;
    }

    private void RequestDeleteFuture(FutureTodoBubbleViewModel bubble)
    {
        pendingFutureDelete = bubble;
        ConfirmationTitle = "删除未来待办？";
        ConfirmationMessage = $"将删除“{bubble.Model.Title}”。";
        PendingConfirmation = ConfirmationKind.DeleteFuture;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        var confirmation = PendingConfirmation;
        CloseConfirmation();
        await RunAsync(async () =>
        {
            switch (confirmation)
            {
                case ConfirmationKind.DeleteScheduled when pendingScheduledDelete is not null:
                    ReplaceScheduledBubbles(
                        await managementService.DeleteAsync(
                            pendingScheduledDelete.Id,
                            isConfirmed: true));
                    break;
                case ConfirmationKind.DeleteFuture when pendingFutureDelete is not null:
                    await managementService.DeleteFutureTodoAsync(
                        pendingFutureDelete.Id,
                        isConfirmed: true);
                    FutureTodos.Remove(pendingFutureDelete);
                    break;
                case ConfirmationKind.LeaveManagement:
                    if (await SaveAllManagementChangesAsync())
                    {
                        await LeaveManagementAsync();
                    }

                    break;
            }
        });
        pendingScheduledDelete = null;
        pendingFutureDelete = null;
    }

    [RelayCommand]
    private async Task DiscardAndLeaveAsync()
    {
        if (PendingConfirmation != ConfirmationKind.LeaveManagement)
        {
            return;
        }

        CloseConfirmation();
        await LeaveManagementAsync();
    }

    [RelayCommand]
    private void CancelConfirmation()
    {
        CloseConfirmation();
        pendingScheduledDelete = null;
        pendingFutureDelete = null;
    }

    private async Task<bool> SaveAllManagementChangesAsync()
    {
        foreach (var bubble in ScheduledTodos.Where(item => item.IsExpanded).ToArray())
        {
            if (!await bubble.SaveIfChangedAsync())
            {
                return false;
            }
        }

        foreach (var bubble in FutureTodos.Where(item => item.IsExpanded).ToArray())
        {
            if (!await bubble.SaveIfChangedAsync())
            {
                return false;
            }
        }

        return true;
    }

    private bool HasUnsavedManagementChanges() =>
        ScheduledTodos.Any(todo => todo.IsExpanded && todo.HasChanges)
        || FutureTodos.Any(todo => todo.IsExpanded && todo.HasChanges);

    private async Task LeaveManagementAsync()
    {
        ErrorMessage = string.Empty;
        CurrentPage = DesktopPage.Focus;
        await ReloadOpeningAsync();
    }

    private void CloseConfirmation()
    {
        PendingConfirmation = ConfirmationKind.None;
        ConfirmationTitle = string.Empty;
        ConfirmationMessage = string.Empty;
    }

    private void ReplaceScheduledBubbles(IReadOnlyList<ScheduledTodo> schedule)
    {
        ScheduledTodos.Clear();
        foreach (var todo in schedule
                     .OrderBy(todo => todo.TimeRange.Start)
                     .ThenBy(todo => todo.TimeRange.End)
                     .ThenBy(todo => todo.Id))
        {
            ScheduledTodos.Add(CreateScheduledBubble(todo));
        }

        RebuildScheduledRows();
    }

    private void RebuildScheduledRows()
    {
        ScheduledRows.Clear();
        var groups = ScheduleManagement.FindMandatoryGroups(
            ScheduledTodos.Select(bubble => bubble.Model));
        var groupByTodoId = groups
            .SelectMany(group => group.Todos.Select(todo => (todo.Id, Group: group)))
            .ToDictionary(item => item.Id, item => item.Group);
        var addedGroups = new HashSet<MandatoryTodoGroup>();

        foreach (var bubble in ScheduledTodos)
        {
            if (!groupByTodoId.TryGetValue(bubble.Id, out var group))
            {
                ScheduledRows.Add(new([bubble], isMandatoryGroup: false));
                continue;
            }

            if (!addedGroups.Add(group))
            {
                continue;
            }

            var groupIds = group.Todos.Select(todo => todo.Id).ToHashSet();
            ScheduledRows.Add(
                new(
                    ScheduledTodos.Where(item => groupIds.Contains(item.Id)),
                    isMandatoryGroup: true));
        }

        OnPropertyChanged(nameof(HasMandatoryGroups));
    }

    private async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ScheduledTodoBubbleViewModel? pendingScheduledDelete;
    private FutureTodoBubbleViewModel? pendingFutureDelete;

    private sealed class PreviewScheduledRepository : IScheduledTodoRepository
    {
        public Task<IReadOnlyList<ScheduledTodo>> LoadAllAsync(System.Threading.CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScheduledTodo>>([]);
        public Task ReplaceAllAsync(IReadOnlyList<ScheduledTodo> scheduledTodos, System.Threading.CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class PreviewUnscheduledRepository : IUnscheduledTodoRepository
    {
        public Task<IReadOnlyList<UnscheduledTodo>> LoadAllActiveAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UnscheduledTodo>>([]);
        public Task<IReadOnlyList<UnscheduledTodo>> LoadByDateAsync(DateOnly scheduledDate, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UnscheduledTodo>>([]);
        public Task<IReadOnlyList<UnscheduledTodo>> LoadDueOnOrBeforeAsync(DateOnly date, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UnscheduledTodo>>([]);
        public Task<UnscheduledTodo?> LoadActiveByIdAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<UnscheduledTodo?>(null);
        public Task SaveAsync(UnscheduledTodo todo, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateActiveAsync(UnscheduledTodo todo, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkPlannedAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkDeletedAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

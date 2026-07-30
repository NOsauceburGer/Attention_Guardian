using System;
using System.Threading.Tasks;
using AttentionGuardian.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttentionGuardian.Desktop.ViewModels;

public partial class ScheduledTodoBubbleViewModel : ViewModelBase
{
    private readonly Func<ScheduledTodoBubbleViewModel, Task<bool>> save;
    private readonly Action<ScheduledTodoBubbleViewModel> requestDelete;

    public ScheduledTodoBubbleViewModel(
        ScheduledTodo todo,
        Func<ScheduledTodoBubbleViewModel, Task<bool>> save,
        Action<ScheduledTodoBubbleViewModel> requestDelete)
    {
        Model = todo;
        this.save = save;
        this.requestDelete = requestDelete;
        Title = todo.Title;
        DurationHours = (decimal)Math.Floor(todo.TimeRange.Duration.TotalHours);
        DurationMinutes = todo.TimeRange.Duration.Minutes;
        IsMandatory = todo.IsMandatory;
        ToggleExpandedCommand = new AsyncRelayCommand(ToggleExpandedAsync);
        DeleteCommand = new RelayCommand(() => requestDelete(this));
    }

    public ScheduledTodo Model { get; private set; }

    public Guid Id => Model.Id;

    public bool IsBreak => Model.Title == ScheduleManagement.BreakTitle;

    public bool HasChanges =>
        Title != Model.Title
        || Duration != Model.TimeRange.Duration
        || IsMandatory != Model.IsMandatory;

    public TimeSpan Duration =>
        TimeSpan.FromHours((double)DurationHours)
        + TimeSpan.FromMinutes((double)DurationMinutes);

    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial string Title { get; set; }
    [ObservableProperty] public partial decimal DurationHours { get; set; }
    [ObservableProperty] public partial decimal DurationMinutes { get; set; }
    [ObservableProperty] public partial bool IsMandatory { get; set; }

    public IAsyncRelayCommand ToggleExpandedCommand { get; }

    public IRelayCommand DeleteCommand { get; }

    public async Task ToggleExpandedAsync()
    {
        if (!IsExpanded)
        {
            IsExpanded = true;
            return;
        }

        if (!HasChanges || await save(this))
        {
            IsExpanded = false;
        }
    }

    public async Task<bool> SaveIfChangedAsync()
    {
        if (!HasChanges)
        {
            return true;
        }

        return await save(this);
    }

    public void AcceptSavedModel(ScheduledTodo todo)
    {
        Model = todo;
        Title = todo.Title;
        DurationHours = (decimal)Math.Floor(todo.TimeRange.Duration.TotalHours);
        DurationMinutes = todo.TimeRange.Duration.Minutes;
        IsMandatory = todo.IsMandatory;
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(IsBreak));
        OnPropertyChanged(nameof(HasChanges));
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(HasChanges));
    partial void OnDurationHoursChanged(decimal value)
    {
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(HasChanges));
    }

    partial void OnDurationMinutesChanged(decimal value)
    {
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(HasChanges));
    }

    partial void OnIsMandatoryChanged(bool value) => OnPropertyChanged(nameof(HasChanges));
}

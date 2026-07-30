using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AttentionGuardian.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttentionGuardian.Desktop.ViewModels;

public partial class FutureTodoBubbleViewModel : ViewModelBase
{
    private readonly Func<FutureTodoBubbleViewModel, Task<bool>> save;
    private readonly Action<FutureTodoBubbleViewModel> requestDelete;
    private bool isUpdatingDate;

    public FutureTodoBubbleViewModel(
        UnscheduledTodo todo,
        Func<FutureTodoBubbleViewModel, Task<bool>> save,
        Action<FutureTodoBubbleViewModel> requestDelete)
    {
        Model = todo;
        this.save = save;
        this.requestDelete = requestDelete;
        Title = todo.Title;
        isUpdatingDate = true;
        ScheduledYear = todo.ScheduledDate.Year;
        ScheduledMonth = todo.ScheduledDate.Month;
        ScheduledDay = todo.ScheduledDate.Day;
        isUpdatingDate = false;
        ToggleExpandedCommand = new AsyncRelayCommand(ToggleExpandedAsync);
        DeleteCommand = new RelayCommand(() => requestDelete(this));
    }

    public UnscheduledTodo Model { get; private set; }

    public Guid Id => Model.Id;

    public bool HasChanges =>
        Title != Model.Title
        || SelectedDate != Model.ScheduledDate;

    [ObservableProperty] public partial bool IsExpanded { get; set; }
    [ObservableProperty] public partial string Title { get; set; }
    [ObservableProperty] public partial int ScheduledYear { get; set; }
    [ObservableProperty] public partial int ScheduledMonth { get; set; }
    [ObservableProperty] public partial int ScheduledDay { get; set; }

    public IReadOnlyList<int> YearOptions { get; } =
        Enumerable.Range(DateTime.Today.Year - 1, 12).ToArray();
    public IReadOnlyList<int> MonthOptions { get; } =
        Enumerable.Range(1, 12).ToArray();
    public IReadOnlyList<int> DayOptions =>
        Enumerable.Range(1, DateTime.DaysInMonth(ScheduledYear, ScheduledMonth)).ToArray();
    public DateOnly SelectedDate => new(ScheduledYear, ScheduledMonth, ScheduledDay);

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

    public void AcceptSavedModel(UnscheduledTodo todo)
    {
        Model = todo;
        Title = todo.Title;
        isUpdatingDate = true;
        ScheduledYear = todo.ScheduledDate.Year;
        ScheduledMonth = todo.ScheduledDate.Month;
        ScheduledDay = todo.ScheduledDate.Day;
        isUpdatingDate = false;
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(DayOptions));
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(HasChanges));
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(HasChanges));
    partial void OnScheduledYearChanged(int value) => RefreshDate();
    partial void OnScheduledMonthChanged(int value) => RefreshDate();
    partial void OnScheduledDayChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(HasChanges));
    }

    private void RefreshDate()
    {
        if (isUpdatingDate)
        {
            return;
        }

        var lastDay = DateTime.DaysInMonth(ScheduledYear, ScheduledMonth);
        if (ScheduledDay > lastDay)
        {
            ScheduledDay = lastDay;
        }

        OnPropertyChanged(nameof(DayOptions));
        OnPropertyChanged(nameof(SelectedDate));
        OnPropertyChanged(nameof(HasChanges));
    }
}

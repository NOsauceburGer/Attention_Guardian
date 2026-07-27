using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AttentionGuardian.Core;
using AttentionGuardian.Application;
using AttentionGuardian.Desktop.Notifications;
using AttentionGuardian.Desktop.ViewModels;

namespace AttentionGuardian.Desktop.Views;

public partial class MainWindow : Window
{
    private static readonly DataFormat<string> ScheduledTodoDragFormat =
        DataFormat.CreateInProcessFormat<string>("attention-guardian-scheduled-todo");
    private static readonly DataFormat<string> RestTemplateDragFormat =
        DataFormat.CreateInProcessFormat<string>("attention-guardian-rest-template");

    private readonly DispatcherTimer refreshTimer;
    private readonly DispatcherTimer reminderTimer;
    private readonly DispatcherTimer toastTimer;
    private readonly DispatcherTimer drawerCloseTimer;
    private readonly DispatcherTimer pageRevealTimer;
    private readonly Stopwatch pageRevealStopwatch = new();
    private readonly System.Collections.Generic.List<PageRevealItem> pageRevealItems = [];
    private Point? drawerPointerStart;
    private PendingDragGesture? pendingDragGesture;

    public IHandoffNotificationSender? NotificationSender { get; init; }

    public MainWindow()
    {
        InitializeComponent();
        refreshTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(5),
            DispatcherPriority.Background,
            RefreshCurrent);
        reminderTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(5),
            DispatcherPriority.Background,
            CheckHandoffReminder);
        toastTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(3),
            DispatcherPriority.Background,
            HideShiftToast);
        drawerCloseTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(310),
            DispatcherPriority.Background,
            HideClosedDrawer);
        pageRevealTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            AnimatePageReveal);
        LaunchMotion.Completed += OnLaunchMotionCompleted;
        Activated += BeginLaunchMotion;
        Opened += InitializeAsync;
        DataContextChanged += OnDataContextChanged;
        refreshTimer.Start();
        reminderTimer.Start();
    }

    private async void InitializeAsync(object? sender, EventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.InitializeAsync();
            if (viewModel.IsAcceptanceAvailable
                && Environment.GetEnvironmentVariable(
                    "ATTENTION_GUARDIAN_ENABLE_ACCEPTANCE") == "1")
            {
                viewModel.OpenAcceptanceCommand.Execute(null);
            }
        }
    }

    private void BeginLaunchMotion(object? sender, EventArgs eventArgs)
    {
        Activated -= BeginLaunchMotion;
        LaunchMotion.Start();
    }

    private void OnLaunchMotionCompleted(object? sender, EventArgs eventArgs)
    {
        LaunchMotion.Completed -= OnLaunchMotionCompleted;
        if (MotionPreferences.IsReducedMotionEnabled)
        {
            WindowControlsPanel.Transitions = null;
        }

        WindowControlsPanel.IsHitTestVisible = true;
        WindowControlsPanel.Opacity = 1;
    }

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (DataContext is INotifyPropertyChanged observable)
        {
            observable.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateDrawerState(DataContext is MainViewModel { IsDrawerOpen: true });
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainViewModel.IsShiftToastVisible)
            && DataContext is MainViewModel { IsShiftToastVisible: true })
        {
            toastTimer.Stop();
            toastTimer.Start();
        }

        if (eventArgs.PropertyName == nameof(MainViewModel.IsDrawerOpen)
            && DataContext is MainViewModel viewModel)
        {
            UpdateDrawerState(viewModel.IsDrawerOpen);
        }

        if (eventArgs.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            BeginPageTransition();
        }

        if (eventArgs.PropertyName == nameof(MainViewModel.IsFocusStarted)
            && DataContext is MainViewModel { IsFocusStarted: true })
        {
            CompletePageReveal();
            FocusedContent.Opacity = 0;
            FocusedContent.RenderTransform = TransformOperations.Parse("translateY(14px)");
            Dispatcher.UIThread.Post(
                () =>
                {
                    FocusedContent.Opacity = 1;
                    FocusedContent.RenderTransform = TransformOperations.Parse("translateY(0px)");
                },
                DispatcherPriority.Loaded);
        }

        if (eventArgs.PropertyName == nameof(MainViewModel.CurrentTodo)
            && DataContext is MainViewModel { IsFocusStarted: true })
        {
            AnimateFocusedTaskChange();
        }

        if (eventArgs.PropertyName == nameof(MainViewModel.IsManagePage)
            && DataContext is MainViewModel
            {
                IsManagePage: true,
                HasMandatoryGroups: true
            })
        {
            Dispatcher.UIThread.Post(
                ScrollToFirstMandatoryGroup,
                DispatcherPriority.Loaded);
        }
    }

    private async void RefreshCurrent(object? sender, EventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.RefreshCurrentAsync();
        }
    }

    private async void CheckHandoffReminder(object? sender, EventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel || NotificationSender is null)
        {
            return;
        }

        var reminder = await viewModel.GetPendingHandoffReminderAsync();
        if (reminder is not null)
        {
            NotificationSender.Send(reminder);
        }
    }

    private void AnimateFocusedTaskChange()
    {
        if (MotionPreferences.IsReducedMotionEnabled)
        {
            FocusedContent.Opacity = 1;
            FocusedContent.RenderTransform = TransformOperations.Parse("translateY(0px)");
            return;
        }

        FocusedContent.Opacity = 0;
        FocusedContent.RenderTransform = TransformOperations.Parse("translateY(-14px)");
        Dispatcher.UIThread.Post(
            () =>
            {
                FocusedContent.Opacity = 1;
                FocusedContent.RenderTransform = TransformOperations.Parse("translateY(0px)");
            },
            DispatcherPriority.Loaded);
    }

    private void SendTestHandoffNotification(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        var sent = NotificationSender?.Send(
            new PendingHandoffReminder(
                Guid.NewGuid(),
                "当前事项（测试）",
                Guid.NewGuid(),
                "下一事项（测试）",
                TimeProvider.System.GetLocalNow()));
        NotificationTestStatus.Text = sent == true
            ? "已交给 Windows 通知中心"
            : string.IsNullOrWhiteSpace(NotificationSender?.LastErrorMessage)
                ? "Windows 通知不可用，请检查系统通知设置"
                : $"Windows 通知不可用：{NotificationSender.LastErrorMessage}";
    }

    private void WindowPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        var point = eventArgs.GetPosition(this);
        if (!eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (WindowState == WindowState.Normal && TryBeginResize(point, eventArgs))
        {
            return;
        }

        var isDrawerGestureZone = point.Y <= 44
            && Math.Abs(point.X - (Bounds.Width / 2)) <= 90;
        if (isDrawerGestureZone)
        {
            drawerPointerStart = point;
            eventArgs.Pointer.Capture(this);
            return;
        }

        if (point.Y <= 44 && point.X < Bounds.Width - 160)
        {
            BeginMoveDrag(eventArgs);
            return;
        }

        if (DataContext is MainViewModel { IsDrawerOpen: true } viewModel
            && !Drawer.Bounds.Contains(eventArgs.GetPosition(Drawer)))
        {
            viewModel.IsDrawerOpen = false;
        }
    }

    private void WindowPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        if (drawerPointerStart is not { } start
            || DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var current = eventArgs.GetPosition(this);
        if (current.Y - start.Y >= 42)
        {
            viewModel.IsDrawerOpen = true;
            drawerPointerStart = null;
            eventArgs.Pointer.Capture(null);
        }
    }

    private void WindowPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        drawerPointerStart = null;
        eventArgs.Pointer.Capture(null);
    }

    private void HideShiftToast(object? sender, EventArgs eventArgs)
    {
        toastTimer.Stop();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.IsShiftToastVisible = false;
        }
    }

    private void UpdateDrawerState(bool isOpen)
    {
        drawerCloseTimer.Stop();
        Drawer.IsHitTestVisible = isOpen;
        if (isOpen)
        {
            Drawer.IsVisible = true;
            Dispatcher.UIThread.Post(
                () =>
                {
                    Drawer.Opacity = 1;
                    Drawer.RenderTransform = TransformOperations.Parse("translateY(0px)");
                },
                DispatcherPriority.Loaded);
            return;
        }

        Drawer.Opacity = 0;
        Drawer.RenderTransform = TransformOperations.Parse("translateY(-24px)");
        drawerCloseTimer.Start();
    }

    private void HideClosedDrawer(object? sender, EventArgs eventArgs)
    {
        drawerCloseTimer.Stop();
        if (DataContext is not MainViewModel { IsDrawerOpen: true })
        {
            Drawer.IsVisible = false;
        }
    }

    private void BeginPageTransition()
    {
        CompletePageReveal();
        PageContent.Opacity = 0;
        Dispatcher.UIThread.Post(
            StartPageReveal,
            DispatcherPriority.Loaded);
    }

    private void StartPageReveal()
    {
        var activePage = PageContent.Children
            .OfType<Control>()
            .FirstOrDefault(control => control.IsVisible);
        if (activePage is null)
        {
            PageContent.Opacity = 1;
            return;
        }

        if (MotionPreferences.IsReducedMotionEnabled)
        {
            PageContent.Opacity = 1;
            return;
        }

        foreach (var control in GetRevealChildren(activePage))
        {
            var translation = new TranslateTransform(0, 18);
            var blur = new BlurEffect { Radius = 4 };
            pageRevealItems.Add(new PageRevealItem(
                control,
                control.Opacity,
                control.RenderTransform,
                control.Effect,
                translation,
                blur));
            control.Opacity = 0;
            control.RenderTransform = translation;
            control.Effect = blur;
        }

        PageContent.Opacity = 1;
        if (pageRevealItems.Count == 0)
        {
            return;
        }

        pageRevealStopwatch.Restart();
        pageRevealTimer.Start();
    }

    private static System.Collections.Generic.IEnumerable<Control> GetRevealChildren(
        Control activePage)
    {
        if (activePage is Border { Child: Panel borderPanel })
        {
            return new[] { activePage }.Concat(
                borderPanel.Children
                    .OfType<Control>()
                    .Where(control => control.IsVisible));
        }

        if (activePage is Panel panel)
        {
            return panel.Children
                .OfType<Control>()
                .Where(control => control.IsVisible);
        }

        return [activePage];
    }

    private void AnimatePageReveal(object? sender, EventArgs eventArgs)
    {
        const double itemDuration = 360;
        const double stagger = 58;
        var elapsed = pageRevealStopwatch.Elapsed.TotalMilliseconds;
        var allComplete = true;

        for (var index = 0; index < pageRevealItems.Count; index++)
        {
            var item = pageRevealItems[index];
            var delay = index * stagger;
            var progress = Math.Clamp((elapsed - delay) / itemDuration, 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 4);

            item.Control.Opacity = item.OriginalOpacity * eased;
            item.Translation.Y = 18 * (1 - eased);
            item.Blur.Radius = 4 * (1 - eased);
            allComplete &= progress >= 1;
        }

        if (!allComplete)
        {
            return;
        }

        CompletePageReveal();
    }

    private void CompletePageReveal()
    {
        pageRevealTimer.Stop();
        pageRevealStopwatch.Stop();
        foreach (var item in pageRevealItems)
        {
            item.Control.Opacity = item.OriginalOpacity;
            item.Control.RenderTransform = item.OriginalTransform;
            item.Control.Effect = item.OriginalEffect;
        }

        pageRevealItems.Clear();
        PageContent.Opacity = 1;
    }

    private sealed record PageRevealItem(
        Control Control,
        double OriginalOpacity,
        ITransform? OriginalTransform,
        IEffect? OriginalEffect,
        TranslateTransform Translation,
        BlurEffect Blur);

    private bool TryBeginResize(Point point, PointerPressedEventArgs eventArgs)
    {
        const double edgeSize = 6;
        var left = point.X <= edgeSize;
        var right = point.X >= Bounds.Width - edgeSize;
        var top = point.Y <= edgeSize;
        var bottom = point.Y >= Bounds.Height - edgeSize;

        var edge = (left, right, top, bottom) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (_, true, true, _) => WindowEdge.NorthEast,
            (true, _, _, true) => WindowEdge.SouthWest,
            (_, true, _, true) => WindowEdge.SouthEast,
            (_, _, true, _) => WindowEdge.North,
            (_, _, _, true) => WindowEdge.South,
            (true, _, _, _) => WindowEdge.West,
            (_, true, _, _) => WindowEdge.East,
            _ => (WindowEdge?)null
        };

        if (edge is not { } resizeEdge)
        {
            return false;
        }

        BeginResizeDrag(resizeEdge, eventArgs);
        return true;
    }

    private void MinimizeWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximizeWindow(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        WindowShell.CornerRadius = WindowState == WindowState.Maximized
            ? new CornerRadius(0)
            : new CornerRadius(14);
    }

    private void CloseWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        Close();
    }

    private async void ScheduledBubbleDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (sender is Control
            {
                DataContext: ScheduledTodoBubbleViewModel bubble
            } bubbleControl)
        {
            eventArgs.Handled = true;
            await ToggleBubbleEditorAsync(
                bubbleControl,
                bubble.ToggleExpandedAsync,
                () => bubble.IsExpanded);
        }
    }

    private async void FutureBubbleDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (sender is Control
            {
                DataContext: FutureTodoBubbleViewModel bubble
            } bubbleControl)
        {
            eventArgs.Handled = true;
            await ToggleBubbleEditorAsync(
                bubbleControl,
                bubble.ToggleExpandedAsync,
                () => bubble.IsExpanded);
        }
    }

    private void DueReminderDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            eventArgs.Handled = true;
            viewModel.ToggleDueCommand.Execute(null);
        }
    }

    private void ConflictReminderDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            eventArgs.Handled = true;
            viewModel.ToggleConflictCommand.Execute(null);
        }
    }

    private static async Task ToggleBubbleEditorAsync(
        Control bubbleControl,
        Func<Task> toggle,
        Func<bool> isExpanded)
    {
        var editor = bubbleControl
            .GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(control => control.Classes.Contains("BubbleEditor"));
        if (editor is null || MotionPreferences.IsReducedMotionEnabled)
        {
            await toggle();
            return;
        }

        bubbleControl.IsHitTestVisible = false;
        try
        {
            if (!isExpanded())
            {
                editor.MaxHeight = 0;
                editor.Opacity = 0;
                await toggle();
                if (!isExpanded())
                {
                    ResetBubbleEditor(editor);
                    return;
                }

                var targetHeight = MeasureBubbleEditor(editor, bubbleControl);
                editor.MaxHeight = 0;
                await AnimateBubbleEditorAsync(
                    editor,
                    startHeight: 0,
                    endHeight: targetHeight,
                    startOpacity: 0,
                    endOpacity: 1);
                ResetBubbleEditor(editor);
                return;
            }

            var currentHeight = Math.Max(
                editor.Bounds.Height,
                MeasureBubbleEditor(editor, bubbleControl));
            await AnimateBubbleEditorAsync(
                editor,
                startHeight: currentHeight,
                endHeight: 0,
                startOpacity: 1,
                endOpacity: 0);
            await toggle();
            if (isExpanded())
            {
                var targetHeight = MeasureBubbleEditor(editor, bubbleControl);
                editor.MaxHeight = 0;
                await AnimateBubbleEditorAsync(
                    editor,
                    startHeight: 0,
                    endHeight: targetHeight,
                    startOpacity: 0,
                    endOpacity: 1);
            }

            ResetBubbleEditor(editor);
        }
        finally
        {
            bubbleControl.IsHitTestVisible = true;
        }
    }

    private static double MeasureBubbleEditor(
        Border editor,
        Control bubbleControl)
    {
        editor.MaxHeight = double.PositiveInfinity;
        var availableWidth = Math.Max(1, bubbleControl.Bounds.Width - 40);
        editor.Measure(new Size(availableWidth, double.PositiveInfinity));
        return Math.Max(1, editor.DesiredSize.Height);
    }

    private static void ResetBubbleEditor(Border editor)
    {
        editor.MaxHeight = double.PositiveInfinity;
        editor.Opacity = 1;
    }

    private static Task AnimateBubbleEditorAsync(
        Border editor,
        double startHeight,
        double endHeight,
        double startOpacity,
        double endOpacity)
    {
        const double durationMilliseconds = 240;
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            (_, _) =>
            {
                var progress = Math.Clamp(
                    stopwatch.Elapsed.TotalMilliseconds / durationMilliseconds,
                    0,
                    1);
                var eased = progress * progress * (3 - (2 * progress));
                editor.MaxHeight =
                    startHeight + ((endHeight - startHeight) * eased);
                editor.Opacity =
                    startOpacity + ((endOpacity - startOpacity) * eased);
                if (progress < 1)
                {
                    return;
                }

                timer?.Stop();
                stopwatch.Stop();
                completion.TrySetResult();
            });
        timer.Start();
        return completion.Task;
    }

    private void ScheduledDragHandlePressed(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control
            {
                DataContext: ScheduledTodoBubbleViewModel bubble
            }
            || !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (sender is Control control)
        {
            var liftTarget = control
                .GetVisualAncestors()
                .OfType<Border>()
                .FirstOrDefault(border => border.Classes.Contains("ScheduledBubble"))
                ?? control;
            BeginPendingDrag(
                control,
                liftTarget,
                eventArgs,
                bubble.Id.ToString("D"),
                ScheduledTodoDragFormat,
                DragDropEffects.Move);
        }
    }

    private async void DragHandlePointerMoved(
        object? sender,
        PointerEventArgs eventArgs)
    {
        if (pendingDragGesture is not { } pending
            || sender is not Control control
            || !ReferenceEquals(control, pending.Source)
            || !eventArgs.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var current = eventArgs.GetPosition(control);
        var delta = current - pending.Start;
        if ((delta.X * delta.X) + (delta.Y * delta.Y) < 36)
        {
            return;
        }

        pendingDragGesture = null;
        eventArgs.Pointer.Capture(null);
        eventArgs.Handled = true;
        await StartDragAsync(pending);
    }

    private void DragHandlePointerReleased(
        object? sender,
        PointerReleasedEventArgs eventArgs) =>
        CancelPendingDrag(sender);

    private void DragHandlePointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs eventArgs) =>
        CancelPendingDrag(sender);

    private void ScheduledBubbleDragOver(object? sender, DragEventArgs eventArgs)
    {
        var isScheduledTodo =
            eventArgs.DataTransfer.TryGetValue(ScheduledTodoDragFormat) is not null;
        var isRestTemplate =
            eventArgs.DataTransfer.TryGetValue(RestTemplateDragFormat) is not null;
        if (!isScheduledTodo && !isRestTemplate)
        {
            eventArgs.DragEffects = DragDropEffects.None;
            eventArgs.Handled = true;
            return;
        }

        eventArgs.DragEffects = isRestTemplate
            ? DragDropEffects.Copy
            : DragDropEffects.Move;
        if (sender is Border border)
        {
            border.Classes.Add("DropTarget");
        }

        eventArgs.Handled = true;
    }

    private void ScheduledBubbleDragLeave(object? sender, DragEventArgs eventArgs) =>
        ClearDropTarget(sender);

    private async void ScheduledBubbleDrop(object? sender, DragEventArgs eventArgs)
    {
        try
        {
            var draggedIdText = eventArgs.DataTransfer.TryGetValue(ScheduledTodoDragFormat);
            var isRestTemplate =
                eventArgs.DataTransfer.TryGetValue(RestTemplateDragFormat) is not null;
            if (sender is not Border
                {
                    DataContext: ScheduledTodoBubbleViewModel target
                }
                || DataContext is not MainViewModel viewModel)
            {
                return;
            }

            var targetIndex = viewModel.ScheduledTodos.IndexOf(target);
            if (targetIndex >= 0)
            {
                if (isRestTemplate)
                {
                    var succeeded = await viewModel.AddRestAtIndexAsync(targetIndex);
                    eventArgs.DragEffects = succeeded
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;
                }
                else if (Guid.TryParse(draggedIdText, out var draggedId))
                {
                    var succeeded = await viewModel.ReorderScheduledAsync(
                        draggedId,
                        targetIndex);
                    eventArgs.DragEffects = succeeded
                        ? DragDropEffects.Move
                        : DragDropEffects.None;
                }
            }

            eventArgs.Handled = true;
        }
        finally
        {
            ClearDropTarget(sender);
        }
    }

    private async void ScheduledBubbleKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (sender is not Border
            {
                DataContext: ScheduledTodoBubbleViewModel bubble
            }
            || DataContext is not MainViewModel viewModel
            || !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        var offset = eventArgs.Key switch
        {
            Key.Up => -1,
            Key.Down => 1,
            _ => 0,
        };
        if (offset == 0)
        {
            return;
        }

        eventArgs.Handled = true;
        await viewModel.MoveScheduledByOffsetAsync(bubble, offset);
    }

    private void ClearDropTarget(object? sender)
    {
        if (sender is Border border)
        {
            border.Classes.Remove("DropTarget");
        }
    }

    private void RestTemplatePointerPressed(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control control
            || !eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginPendingDrag(
            control,
            control,
            eventArgs,
            ScheduleManagement.BreakTitle,
            RestTemplateDragFormat,
            DragDropEffects.Copy);
    }

    private void QueueEndDropZoneDragOver(object? sender, DragEventArgs eventArgs)
    {
        var isScheduledTodo =
            eventArgs.DataTransfer.TryGetValue(ScheduledTodoDragFormat) is not null;
        var isRestTemplate =
            eventArgs.DataTransfer.TryGetValue(RestTemplateDragFormat) is not null;
        if (!isScheduledTodo && !isRestTemplate)
        {
            eventArgs.DragEffects = DragDropEffects.None;
            eventArgs.Handled = true;
            return;
        }

        eventArgs.DragEffects = isRestTemplate
            ? DragDropEffects.Copy
            : DragDropEffects.Move;
        if (sender is Border border)
        {
            border.Classes.Add("DropTarget");
        }

        eventArgs.Handled = true;
    }

    private async void QueueEndDropZoneDrop(object? sender, DragEventArgs eventArgs)
    {
        try
        {
            if (eventArgs.DataTransfer.TryGetValue(RestTemplateDragFormat) is not null
                && DataContext is MainViewModel viewModel)
            {
                var succeeded = await viewModel.AddRestAtIndexAsync(
                    viewModel.ScheduledTodos.Count);
                eventArgs.DragEffects = succeeded
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            }
            else if (Guid.TryParse(
                         eventArgs.DataTransfer.TryGetValue(ScheduledTodoDragFormat),
                         out var draggedId)
                     && DataContext is MainViewModel scheduledViewModel
                     && scheduledViewModel.ScheduledTodos.Count > 0)
            {
                var succeeded = await scheduledViewModel.ReorderScheduledAsync(
                    draggedId,
                    scheduledViewModel.ScheduledTodos.Count - 1);
                eventArgs.DragEffects = succeeded
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
            }

            eventArgs.Handled = true;
        }
        finally
        {
            ClearDropTarget(sender);
        }
    }

    private void BeginPendingDrag(
        Control source,
        Control liftTarget,
        PointerPressedEventArgs eventArgs,
        string value,
        DataFormat<string> format,
        DragDropEffects effect)
    {
        pendingDragGesture = new(
            source,
            liftTarget,
            eventArgs.GetPosition(source),
            eventArgs,
            value,
            format,
            effect);
        eventArgs.Pointer.Capture(source);
        eventArgs.Handled = true;
    }

    private void CancelPendingDrag(object? sender)
    {
        if (pendingDragGesture is not { } pending
            || sender is not Control control
            || !ReferenceEquals(control, pending.Source))
        {
            return;
        }

        pendingDragGesture = null;
    }

    private static async Task StartDragAsync(PendingDragGesture pending)
    {
        var item = new DataTransferItem();
        item.Set(pending.Format, pending.Value);
        var transfer = new DataTransfer();
        transfer.Add(item);
        var originalOpacity = pending.LiftTarget.Opacity;
        var originalTransform = pending.LiftTarget.RenderTransform;
        pending.LiftTarget.Opacity = 0.72;
        pending.LiftTarget.RenderTransform = new ScaleTransform(1.025, 1.025);
        try
        {
            await DragDrop.DoDragDropAsync(
                pending.TriggerEvent,
                transfer,
                pending.Effect);
        }
        finally
        {
            pending.LiftTarget.Opacity = originalOpacity;
            pending.LiftTarget.RenderTransform = originalTransform;
        }
    }

    private void ScrollToFirstMandatoryGroup()
    {
        var firstGroup = ManagementScrollViewer
            .GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(
                control => control.DataContext is ScheduledTodoRowViewModel
                {
                    IsMandatoryGroup: true
                });
        firstGroup?.BringIntoView();
    }

    private sealed record PendingDragGesture(
        Control Source,
        Control LiftTarget,
        Point Start,
        PointerPressedEventArgs TriggerEvent,
        string Value,
        DataFormat<string> Format,
        DragDropEffects Effect);
}

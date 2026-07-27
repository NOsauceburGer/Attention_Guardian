using System;
using AttentionGuardian.Application;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace AttentionGuardian.Desktop.Notifications;

public sealed class WindowsHandoffNotificationSender : IHandoffNotificationSender, IDisposable
{
    private bool isRegistered;

    public string? LastErrorMessage { get; private set; }

    public bool TryRegister()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            AppNotificationManager.Default.Register();
            isRegistered = true;
            LastErrorMessage = null;
            return true;
        }
        catch (Exception exception)
        {
            LastErrorMessage = FormatError(exception);
            Console.Error.WriteLine($"Windows notification registration failed: {LastErrorMessage}");
            return false;
        }
    }

    public bool Send(PendingHandoffReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        if (!isRegistered)
        {
            return false;
        }

        try
        {
            var notification = new AppNotificationBuilder()
                .AddText("当前事项即将结束")
                .AddText($"{reminder.CurrentTodoTitle} → {reminder.NextTodoTitle}")
                .BuildNotification();
            AppNotificationManager.Default.Show(notification);
            LastErrorMessage = null;
            return true;
        }
        catch (Exception exception)
        {
            LastErrorMessage = FormatError(exception);
            Console.Error.WriteLine($"Windows notification send failed: {LastErrorMessage}");
            return false;
        }
    }

    public void Dispose()
    {
        if (!isRegistered)
        {
            return;
        }

        AppNotificationManager.Default.Unregister();
        isRegistered = false;
    }

    private static string FormatError(Exception exception)
    {
        return $"{exception.GetType().Name} (0x{exception.HResult:X8}): {exception.Message}";
    }
}

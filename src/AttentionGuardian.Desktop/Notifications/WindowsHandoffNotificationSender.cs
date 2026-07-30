using System;
using AttentionGuardian.Application;

namespace AttentionGuardian.Desktop.Notifications;

/// <summary>
/// A safe desktop fallback until a native notification sender is supplied for a platform.
/// The scheduling decision stays in Application; this adapter deliberately does not use a
/// Windows API so the shared Avalonia application can run on macOS.
/// </summary>
public sealed class UnavailableHandoffNotificationSender : IHandoffNotificationSender, IDisposable
{
    private bool isRegistered;

    public string? LastErrorMessage { get; private set; }

    public bool TryRegister()
    {
        isRegistered = false;
        LastErrorMessage = "System notifications are not configured for this platform.";
        return false;
    }

    public bool Send(PendingHandoffReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        if (!isRegistered)
        {
            return false;
        }

        LastErrorMessage = "System notifications are not configured for this platform.";
        return false;
    }

    public void Dispose()
    {
        if (!isRegistered)
        {
            return;
        }

        isRegistered = false;
    }
}

using AttentionGuardian.Application;

namespace AttentionGuardian.Desktop.Notifications;

public interface IHandoffNotificationSender
{
    string? LastErrorMessage => null;

    bool Send(PendingHandoffReminder reminder);
}

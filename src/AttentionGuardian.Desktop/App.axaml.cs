using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AttentionGuardian.Application;
using AttentionGuardian.Desktop.ViewModels;
using AttentionGuardian.Desktop.Views;
using AttentionGuardian.Desktop.Notifications;
using AttentionGuardian.Infrastructure;

namespace AttentionGuardian.Desktop;

public partial class App : Avalonia.Application
{
    private WindowsHandoffNotificationSender? notificationSender;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var databaseDirectory =
                Environment.GetEnvironmentVariable("ATTENTION_GUARDIAN_DATA_DIRECTORY");
            if (string.IsNullOrWhiteSpace(databaseDirectory))
            {
                databaseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AttentionGuardian");
            }
            var scheduledRepository = new SqliteScheduledTodoRepository(
                Path.Combine(databaseDirectory, SqliteScheduledTodoRepository.DefaultDatabaseFileName));
            var futureRepository = new SqliteUnscheduledTodoRepository(
                Path.Combine(databaseDirectory, SqliteUnscheduledTodoRepository.DefaultDatabaseFileName));
            var planningService = new TodoPlanningService(
                scheduledRepository,
                futureRepository,
                TimeProvider.System);
            var managementService = new ScheduleManagementService(
                scheduledRepository,
                futureRepository,
                TimeProvider.System);

            notificationSender = new WindowsHandoffNotificationSender();
            notificationSender.TryRegister();
            desktop.Exit += (_, _) => notificationSender.Dispose();

            desktop.MainWindow = new MainWindow
            {
                NotificationSender = notificationSender,
                DataContext = new MainViewModel(
                    planningService,
                    managementService,
                    scheduledRepository,
                    TimeProvider.System),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

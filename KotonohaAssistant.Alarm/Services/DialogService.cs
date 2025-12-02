using KotonohaAssistant.Alarm.Controls;
using KotonohaAssistant.Alarm.Models;
using System.Windows;

namespace KotonohaAssistant.Alarm.Services;

public interface IDialogService
{
    Task<AlarmSetting?> ShowAddAlarmDialogAsync();
}

public class DialogService : IDialogService
{
    private readonly MainWindow _mainWindow;
    public DialogService(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public Task<AlarmSetting?> ShowAddAlarmDialogAsync()
    {
        var tcs = new TaskCompletionSource<AlarmSetting?>();

        Application.Current.Dispatcher.Invoke(() =>
        {
            var dialog = new AddAlarmDialog
            {
                Owner = _mainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            dialog.Closed += (_, __) =>
            {
                if (dialog.DialogResult == true)
                {
                    tcs.TrySetResult(dialog.AlarmSetting);
                }
                else
                {
                    tcs.TrySetResult(null);
                }
            };

            dialog.ShowDialog();
        });

        return tcs.Task;
    }
}

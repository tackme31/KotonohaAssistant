using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KotonohaAssistant.Alarm.Services;

namespace KotonohaAssistant.Alarm.ViewModels;

public partial class TimerViewModel : ObservableObject
{
    private readonly ITimerService _timerService;

    [ObservableProperty]
    private int _inputSeconds = 60;

    [ObservableProperty]
    private int _remainingSeconds;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    public TimerViewModel(ITimerService timerService)
    {
        _timerService = timerService;

        _timerService.Tick += s =>
        {
            RemainingSeconds = s;
        };
        _timerService.Finished += () =>
        {
            RemainingSeconds = 0;
        };
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (InputSeconds <= 0)
        {
            return;
        }

        RemainingSeconds = InputSeconds;
        _timerService.SetTime(InputSeconds);

        IsRunning = true;
        _timerService.Start();
    }

    public void Start(TimeSpan time)
    {
        InputSeconds = (int)time.TotalSeconds;
        Start();
    }

    private bool CanStart() => !IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    public void Stop()
    {
        _timerService.Stop();
        IsRunning = false;
        RemainingSeconds = InputSeconds;
    }

    private bool CanStop() => IsRunning;
}

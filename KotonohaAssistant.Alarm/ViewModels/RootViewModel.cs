using CommunityToolkit.Mvvm.ComponentModel;

namespace KotonohaAssistant.Alarm.ViewModels;

public partial class RootViewModel : ObservableObject
{
    public AlarmListViewModel AlarmList { get; }
    public TimerViewModel Timer { get; set; }

    public RootViewModel(AlarmListViewModel alarmListViewModel, TimerViewModel timerViewModel)
    {
        AlarmList = alarmListViewModel;
        Timer = timerViewModel;
    }
}

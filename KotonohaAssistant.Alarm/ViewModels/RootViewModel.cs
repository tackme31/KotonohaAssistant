using CommunityToolkit.Mvvm.ComponentModel;

namespace KotonohaAssistant.Alarm.ViewModels;

public partial class RootViewModel : ObservableObject
{
    public AlarmListViewModel AlarmList { get; }

    public RootViewModel(AlarmListViewModel alarmListViewModel)
    {
        AlarmList = alarmListViewModel;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using KotonohaAssistant.Alarm.Models;
using System.Collections.ObjectModel;

namespace KotonohaAssistant.Alarm.ViewModels;

public partial class AlarmListViewModel : ObservableObject
{
    internal void OnApplicationLoaded()
    {
    }

    [ObservableProperty]
    private ObservableCollection<AlarmSetting> _alarmSettings = new([
        new AlarmSetting
        {
            Id = 1,
            TimeInSeconds = TimeSpan.Parse("12:00").TotalSeconds,
            VoicePath = @"path/to/voice.mp3",
            IsEnabled = true
        },
        new AlarmSetting
        {
            Id = 2,
            TimeInSeconds = TimeSpan.Parse("9:30").TotalSeconds,
            VoicePath = @"path/to/voice.mp3",
            IsEnabled = false
        }]);
}

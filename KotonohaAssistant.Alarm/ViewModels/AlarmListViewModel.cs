using CommunityToolkit.Mvvm.ComponentModel;
using KotonohaAssistant.Alarm.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            TimeInSeconds = TimeSpan.Parse("12:23").TotalSeconds,
            VoicePath = @"path/to/voice.mp3",
            IsEnabled = true
        }]);

    [ObservableProperty]
    private string _fooBar = "200";
}

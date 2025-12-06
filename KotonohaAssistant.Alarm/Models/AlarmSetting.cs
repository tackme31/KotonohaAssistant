using CommunityToolkit.Mvvm.ComponentModel;

namespace KotonohaAssistant.Alarm.Models;

public partial class AlarmSetting : ObservableObject
{
    [ObservableProperty]
    private long _id;

    [ObservableProperty]
    private long _timeInSeconds;

    [ObservableProperty]
    private string? _voicePath;

    [ObservableProperty]

    private bool _isEnabled;

    [ObservableProperty]
    private bool _isRepeated;
}

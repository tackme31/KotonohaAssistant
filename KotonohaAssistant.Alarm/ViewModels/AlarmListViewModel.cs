using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KotonohaAssistant.Alarm.Controls;
using KotonohaAssistant.Alarm.Models;
using KotonohaAssistant.Alarm.Repositories;
using KotonohaAssistant.Alarm.Services;
using System.Collections.ObjectModel;

namespace KotonohaAssistant.Alarm.ViewModels;

public partial class AlarmListViewModel : ObservableObject
{
    private readonly IAlarmRepository _alarmRepository;
    private readonly IDialogService _dialogService;

    public AlarmListViewModel(IAlarmRepository alarmRepository, IDialogService dialogService)
    {
        _alarmRepository = alarmRepository;
        _dialogService = dialogService;
    }

    internal void OnApplicationLoaded()
    {
    }

    public async Task InitializeAsync()
    {
        var settings = await _alarmRepository.GetAlarmSettingsAsync(TimeSpan.MinValue, TimeSpan.MaxValue);
        var sorted = new ObservableCollection<AlarmSetting>(settings.OrderBy(s => s.TimeInSeconds));

        AlarmSettings = sorted;
    }

    [ObservableProperty]
    private ObservableCollection<AlarmSetting> _alarmSettings = [];

    [ObservableProperty]
    private int _selectedAlarmIndex = 0;

    public IRelayCommand DeleteCommand => new RelayCommand(DeleteSelectedAlarm);

    private async void DeleteSelectedAlarm()
    {
        if (AlarmSettings.Count <= SelectedAlarmIndex)
        {
            return;
        }

        var toDelete = AlarmSettings[SelectedAlarmIndex];
        AlarmSettings.Remove(toDelete);

        try
        {
            await _alarmRepository.DeleteAlarmSettingsAsync([toDelete.Id]);
        }
        catch (Exception ex)
        {
            // log
        }
    }

    public IRelayCommand AddCommand => new RelayCommand(AddAlarm);

    private async void AddAlarm()
    {
        var setting = await _dialogService.ShowAddAlarmDialogAsync();
        if (setting is null)
        {
            return;
        }

        var id = await _alarmRepository.InsertAlarmSetting(setting);
        setting.Id = id;

        int index = 0;
        while (index < AlarmSettings.Count && AlarmSettings[index].TimeInSeconds <= setting.TimeInSeconds)
        {
            index++;
        }
        AlarmSettings.Insert(index, setting);
    }
}

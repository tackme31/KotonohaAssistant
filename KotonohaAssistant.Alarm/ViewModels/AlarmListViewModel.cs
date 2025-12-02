using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KotonohaAssistant.Alarm.Models;
using KotonohaAssistant.Alarm.Repositories;
using KotonohaAssistant.Alarm.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;

namespace KotonohaAssistant.Alarm.ViewModels;

public partial class AlarmListViewModel : ObservableObject
{
    private readonly IAlarmRepository _alarmRepository;
    private readonly IAlarmService _alarmService;
    private readonly IDialogService _dialogService;

    public AlarmListViewModel(IAlarmRepository alarmRepository, IAlarmService alarmService, IDialogService dialogService)
    {
        _alarmRepository = alarmRepository;
        _alarmService = alarmService;
        _dialogService = dialogService;
    }

    internal void OnApplicationLoaded()
    {
    }

    public async Task InitializeAsync()
    {
        var settings = await _alarmRepository.GetAlarmSettingsAsync(TimeSpan.MinValue, TimeSpan.MaxValue);
        var sorted = new ObservableCollection<AlarmSetting>(settings.OrderBy(s => s.TimeInSeconds));

        foreach (var s in sorted)
        {
            s.PropertyChanged += AlarmSetting_PropertyChanged;
        }

        AlarmSettings = sorted;
        AlarmSettings.CollectionChanged += AlarmSettings_CollectionChanged;
    }

    [ObservableProperty]
    private ObservableCollection<AlarmSetting> _alarmSettings = [];

    [ObservableProperty]
    private int _selectedAlarmIndex = 0;

    public IRelayCommand DeleteCommand => new RelayCommand(DeleteSelectedAlarm);

    private async void DeleteSelectedAlarm()
    {
        if (SelectedAlarmIndex < 0 ||
            AlarmSettings.Count <= SelectedAlarmIndex)
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

        var id = await _alarmRepository.InsertAlarmSettingAsync(setting);
        setting.Id = id;

        int index = 0;
        while (index < AlarmSettings.Count && AlarmSettings[index].TimeInSeconds <= setting.TimeInSeconds)
        {
            index++;
        }

        AlarmSettings.Insert(index, setting);
    }

    public void AddAlarm(AlarmSetting setting)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            int index = 0;
            while (index < AlarmSettings.Count && AlarmSettings[index].TimeInSeconds <= setting.TimeInSeconds)
            {
                index++;
            }

            AlarmSettings.Insert(index, setting);
        });
    }

    public IRelayCommand StopCommand => new RelayCommand(StopAlarm);

    public void StopAlarm()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var id = _alarmService.GetCurrentAlarmId();
            if (id is null)
            {
                return;
            }

            var setting = AlarmSettings.FirstOrDefault(s => s.Id == id);
            if (setting is null)
            {
                return;
            }

            _alarmService.StopAlarm();
            setting.IsEnabled = false;
        });
    }

    private async void AlarmSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not AlarmSetting alarm)
            return;

        if (e.PropertyName == nameof(AlarmSetting.IsEnabled))
        {
            try
            {
                await _alarmRepository.UpdateIsEnabledAsync(alarm.Id, alarm.IsEnabled);
            }
            catch (Exception ex)
            {
                // 必要に応じてエラーハンドリング
            }
        }
    }

    private void AlarmSettings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (AlarmSetting newItem in e.NewItems)
            {
                newItem.PropertyChanged += AlarmSetting_PropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (AlarmSetting oldItem in e.OldItems)
            {
                oldItem.PropertyChanged -= AlarmSetting_PropertyChanged;
            }
        }
    }
}

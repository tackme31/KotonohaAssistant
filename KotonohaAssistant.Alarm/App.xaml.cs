using KotonohaAssistant.Alarm.Pages;
using KotonohaAssistant.Alarm.Repositories;
using KotonohaAssistant.Alarm.Services;
using KotonohaAssistant.Alarm.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions;

namespace KotonohaAssistant.Alarm;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            DotNetEnv.Env.TraversePath().Load();

            _ = c.SetBasePath(AppContext.BaseDirectory);
            _ = c.AddEnvironmentVariables();
        })
        .ConfigureServices((hostContext, services) =>
        {
            var config = hostContext.Configuration;
            var alarmSoundFile = config["ALARM_SOUND_FILE"] ?? throw new Exception();

            // App Host
            _ = services.AddHostedService<ApplicationHostService>();

            // Main Window
            _ = services.AddSingleton<MainWindow>();
            _ = services.AddSingleton<RootViewModel>();

            // Pages
            _ = services.AddSingleton<AlarmListPage>();
            _ = services.AddSingleton<AlarmListViewModel>();
            _ = services.AddSingleton<TimerPage>();

            // Repositories
            _ = services.AddSingleton<IAlarmRepository>(_ =>
            {
                var appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotonoha Assistant");
                var alarmDBPath = Path.Combine(appDirectory, "alarm.db");
                return new AlarmRepository(alarmDBPath);
            });
        })
        .Build();

    protected override void OnStartup(StartupEventArgs e)
    {
        DotNetEnv.Env.TraversePath().Load();

        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync().Wait();
        _host.Dispose();
    }
}

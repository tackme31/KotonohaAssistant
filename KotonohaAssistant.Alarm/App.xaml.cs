using KotonohaAssistant.Alarm.Services;
using KotonohaAssistant.Alarm.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace KotonohaAssistant.Alarm;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(AppContext.BaseDirectory);
        })
        .ConfigureServices((_1, services) =>
        {
            _ = services.AddHostedService<ApplicationHostService>();
            _ = services.AddSingleton<MainWindow>();
            _ = services.AddSingleton<RootViewModel>();
        })
        .Build();

    protected override void OnStartup(StartupEventArgs e)
    {
        _host.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.StopAsync().Wait();
        _host.Dispose();
    }
}

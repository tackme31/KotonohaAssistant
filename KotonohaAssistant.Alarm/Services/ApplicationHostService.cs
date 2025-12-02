using KotonohaAssistant.Alarm.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace KotonohaAssistant.Alarm.Services;

internal class ApplicationHostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public ApplicationHostService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var alarmService = _serviceProvider.GetRequiredService<IAlarmService>();
        alarmService.Start();

        var window = _serviceProvider.GetRequiredService<MainWindow>();
        var viewModel = _serviceProvider.GetRequiredService<RootViewModel>();

        window.DataContext = viewModel;
        window.Loaded += (sender, arg) => viewModel.OnApplicationLoaded();
        window.Show();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        var alarmService = _serviceProvider.GetRequiredService<IAlarmService>();
        alarmService.Stop();

        return Task.CompletedTask;
    }
}

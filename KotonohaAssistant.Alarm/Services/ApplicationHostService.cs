using KotonohaAssistant.Alarm.Models;
using KotonohaAssistant.Alarm.Repositories;
using KotonohaAssistant.Alarm.ViewModels;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.IO;
using System.IO.Pipes;
using System.Text.RegularExpressions;
using System.Windows;

namespace KotonohaAssistant.Alarm.Services;

internal class ApplicationHostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _cts;
    private Task? _pipeServerTask;

    public ApplicationHostService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Start named pipe
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pipeServerTask = RunNamedPipeServer(_cts.Token);

        // Start alarm
        var alarmService = _serviceProvider.GetRequiredService<IAlarmService>();
        alarmService.Start();

        // Setup window
        var window = _serviceProvider.GetRequiredService<MainWindow>();
        var viewModel = _serviceProvider.GetRequiredService<RootViewModel>();

        window.DataContext = viewModel;
        window.Show();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop named pipe
        _cts?.Cancel();

        // Stop alarm
        var alarmService = _serviceProvider.GetRequiredService<IAlarmService>();
        alarmService.Stop();

        return Task.CompletedTask;
    }

    private async Task RunNamedPipeServer(CancellationToken token)
    {
        var vm = _serviceProvider.GetRequiredService<AlarmListViewModel>();
        var repository = _serviceProvider.GetRequiredService<IAlarmRepository>();
        var tasks = new List<Task>();
        while (!token.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(Const.AlarmAppPipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Message, PipeOptions.Asynchronous);

            try
            {
                await pipe.WaitForConnectionAsync();
                Console.WriteLine("Client connected.");

                var clientTask = HandleClientAsync(pipe);
                tasks.Add(clientTask);
            }
            catch (IOException)
            {
                // 接続エラー処理
                // reader/writerのdisposeの関係で毎回発生するため一旦無視する
            }
            catch (Exception)
            {
                // log
            }
        }

        async Task HandleClientAsync(NamedPipeServerStream ps)
        {
            using (var reader = new StreamReader(ps))
            using (var writer = new StreamWriter(ps) { AutoFlush = true })
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    Console.WriteLine($"Data received: {line}");

                    var (command, payload) = ParseRequest(line);
                    try
                    {
                        await RunCommand(command, payload);
                        await writer.WriteLineAsync("OK");
                    }
                    catch (Exception ex)
                    {
                        await writer.WriteLineAsync("ERROR: " + ex.Message);
                    }
                }
            }

            // クライアントが切断された後に行う処理
            Console.WriteLine("Client disconnected.");
            ps.Disconnect(); // 接続を切断
        }

        async Task RunCommand(string command, string payload)
        {
            switch (command)
            {
                case "ADD_ALARM":
                    var request = JsonConvert.DeserializeObject<AddAlarmRequest>(payload);
                    var setting = new AlarmSetting
                    {
                        TimeInSeconds = request!.TimeInSeconds,
                        VoicePath = request!.VoicePath,
                        IsRepeated = request!.IsRepeated,
                        IsEnabled = true,
                    };
                    var id = await repository.InsertAlarmSettingAsync(setting);
                    setting.Id = id;
                    vm.AddAlarm(setting);
                    break;
                case "STOP_ALARM":
                    vm.StopAlarm();
                    break;
            }
        }

        (string command, string payload) ParseRequest(string input)
        {
            var m = Regex.Match(input, @"^(?<command>[^:]+):?(?<payload>.*)$");
            return (m.Groups["command"].Value, m.Groups["payload"].Value);
        }
    }
}

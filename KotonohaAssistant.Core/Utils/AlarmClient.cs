using KotonohaAssistant.Core.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace KotonohaAssistant.Core.Utils;

public class AlarmClient : IDisposable
{
    private NamedPipeClientStream _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public AlarmClient()
    {
        _pipeClient = new NamedPipeClientStream(".", Const.AlarmAppPipeName, PipeDirection.InOut);
        _reader = null;
        _writer = null;
    }

    private async Task ConnectToServerAsync()
    {
        if (!_pipeClient.IsConnected)
        {
            await _pipeClient.ConnectAsync(5000); // 非同期接続

            _reader = new StreamReader(_pipeClient);
            _writer = new StreamWriter(_pipeClient) { AutoFlush = true };
        }
    }

    public async Task AddAlarm(TimeSpan time, string voicePath, bool isRepeated)
    {
        await ConnectToServerAsync();

        if (_writer is null || _reader is null)
        {
            return;
        }

        try
        {
            var request = new AddAlarmRequest()
            {
                TimeInSeconds = time.TotalSeconds,
                VoicePath = voicePath,
                IsRepeated = isRepeated
            };
            var serialized = JsonConvert.SerializeObject(request, Formatting.None);
            await _writer.WriteLineAsync("ADD_ALARM:" + serialized);

            await _reader.ReadLineAsync();
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("An error occurred while communicating with the server.", ex);
        }
    }

    public async Task StopAlarm()
    {
        await ConnectToServerAsync();

        if (_writer is null || _reader is null)
        {
            return;
        }

        try
        {
            await _writer.WriteLineAsync("STOP_ALARM");

            await _reader.ReadLineAsync();
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("An error occurred while communicating with the server.", ex);
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _pipeClient?.Dispose();
    }
}

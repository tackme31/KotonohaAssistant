using System;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;
using KotonohaAssistant.Core.Models;
using Newtonsoft.Json;

namespace KotonohaAssistant.Core.Utils;

/// <summary>
/// KotonohaAssistant.VoiceServerのクライアント
/// </summary>
public class VoiceClient : IDisposable
{
    private NamedPipeClientStream _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public VoiceClient()
    {
        _pipeClient = new NamedPipeClientStream(".", Const.VoiceEditorPipeName, PipeDirection.InOut);
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

    public async Task SpeakAsync(Kotonoha sister, string message)
    {
        if (_writer is null || _reader is null)
        {
            return;
        }

        await ConnectToServerAsync();

        try
        {
            var request = new SpeakRequest()
            {
                SisterType = sister,
                Message = message,
            };
            var serialized = JsonConvert.SerializeObject("SPEAK:" + request, Formatting.None);
            await _writer.WriteLineAsync(serialized);

            await _reader.ReadLineAsync();
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("An error occurred while communicating with the server.", ex);
        }
    }

    public async Task StopAsync()
    {
        if (_writer is null || _reader is null)
        {
            return;
        }

        await ConnectToServerAsync();

        try
        {
            await _writer.WriteLineAsync("STOP");

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

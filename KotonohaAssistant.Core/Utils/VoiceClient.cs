using System;
using System.IO;
using System.IO.Pipes;
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

    public async Task SpeakAsync(Kotonoha sister, Emotion emotion, string message)
    {
        await ConnectToServerAsync();

        if (_writer is null || _reader is null)
        {
            return;
        }

        try
        {
            var request = new SpeakRequest()
            {
                SisterType = sister,
                Emotion = emotion,
                Message = message,
            };
            var serialized = JsonConvert.SerializeObject(request, Formatting.None);
            await _writer.WriteLineAsync("SPEAK:" + serialized);

            await _reader.ReadLineAsync();
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("An error occurred while communicating with the server.", ex);
        }
    }

    public async Task StopAsync()
    {
        await ConnectToServerAsync();

        if (_writer is null || _reader is null)
        {
            return;
        }

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

    public async Task ExportVoiceAsync(Kotonoha sister, Emotion emotion, string message, string path)
    {
        await ConnectToServerAsync();

        if (_writer is null || _reader is null)
        {
            return;
        }

        try
        {
            var request = new ExportVoiceRequest()
            {
                SisterType = sister,
                Emotion = emotion,
                Message = message,
                SavePath = path
            };
            var serialized = JsonConvert.SerializeObject(request, Formatting.None);
            await _writer.WriteLineAsync("EXPORT:" + serialized);

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

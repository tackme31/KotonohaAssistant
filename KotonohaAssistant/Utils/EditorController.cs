using AI.Talk.Editor.Api;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KotonohaAssistant.Utils;

class EditorController
{
    private readonly TtsControl _ttsControl = new();
    private static readonly int _waitCheckInterval = 500;
    private static readonly int _waitTimeout = 15 * 1000;

    public void InitializeHost()
    {
        var availableHosts = _ttsControl.GetAvailableHostNames();
        if (!availableHosts.Any())
        {
            throw new Exception("利用可能なホストが存在しません。");
        }
        
        try
        {
            var host = availableHosts.First();
            _ttsControl.Initialize(host);

            if (_ttsControl.Status == HostStatus.NotRunning)
            {
                Console.WriteLine($"Starting host");
                _ttsControl.StartHost();
            }

            Console.WriteLine($"Connecting to host");
            _ttsControl.Connect();
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
            throw;
        }
    }

    public async Task SpeakAsync(SisterType sister, string message)
    {
        try
        {
            await WaitForStatusAsync(HostStatus.Idle);

            _ttsControl.CurrentVoicePresetName = sister switch
            {
                SisterType.KotonohaAkane => "琴葉 茜",
                SisterType.KotonohaAoi => "琴葉 葵",
                _ => throw new NotSupportedException($"未対応のキャラクターです: {sister}")
            };

            _ttsControl.Text = message;
            _ttsControl.Play();

            await WaitForStatusAsync(HostStatus.Idle);
        }
        catch(Exception e)
        {
            Console.WriteLine(e.Message);
            throw;
        }
    }

    private async Task WaitForStatusAsync(HostStatus status)
    {
        var startTime = DateTime.Now;

        while (_ttsControl.Status != status)
        {
            // タイムアウトチェック
            if ((DateTime.Now - startTime).TotalMilliseconds > _waitTimeout)
            {
                throw new TimeoutException();
            }

            await Task.Delay(_waitCheckInterval);
        }
    }
}

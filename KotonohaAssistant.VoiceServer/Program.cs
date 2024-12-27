using System;
using System.Linq;
using System.Threading.Tasks;
using AI.Talk.Editor.Api;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using CoreAudio;

namespace KotonohaAssistant.VoiceServer
{
    /// <summary>
    /// A.I. VOICEのエディターを操作するためのサーバー
    /// エディタのAPIが.NET Frameworkにしか対応していないのので
    /// Blazor/CLIアプリからはこのサーバーを介してボイスの読み上げを行う
    /// </summary>
    internal class Program
    {
        private static readonly TtsControl _ttsControl = new TtsControl();
        private static readonly int _waitCheckInterval = 500;
        private static readonly int _waitTimeout = 15 * 1000;

        private static bool _switchSpeaker;
        private static MMDevice _akaneDevice;
        private static MMDevice _aoiDevice;



        static async Task Main(string[] args)
        {
            // load .env
            DotNetEnv.Env.TraversePath().Load();

            // 姉妹のスピーカー切り替え
            (_switchSpeaker, _akaneDevice, _aoiDevice) = GetSpeakerSettings();

            Console.WriteLine("Initializing editor host...");
            InitializeEditorHost();

            Console.WriteLine("Named Pipe Server is starting...");

            var tasks = new List<Task>();

            while (true) // クライアントの接続を待ち続ける
            {
                // パイプサーバーを作成
                var pipeServer = new NamedPipeServerStream(Const.VoiceEditorPipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                Console.WriteLine("Waiting for client connection...");

                try
                {
                    await pipeServer.WaitForConnectionAsync();
                    Console.WriteLine("Client connected.");

                    // 新しい接続を処理するタスクを開始
                    var clientTask = HandleClientAsync(pipeServer);
                    tasks.Add(clientTask);  // タスクをリストに追加

                    // 接続されたクライアントを処理するタスクを非同期で実行
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
                                await RunCommand(command, payload);

                                await writer.WriteLineAsync("OK");
                            }
                        }

                        // クライアントが切断された後に行う処理
                        Console.WriteLine("Client disconnected.");
                        ps.Disconnect(); // 接続を切断
                    }
                }
                catch (IOException)
                {
                    // 接続エラー処理
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                }
            }

            (string command, string payload) ParseRequest(string input)
            {
                var m = Regex.Match(input, @"^(?<command>[^:]+):?(?<payload>.*)$");
                return (m.Groups["command"].Value, m.Groups["payload"].Value);
            }
        }

        private static (bool switchSpeaker, MMDevice akaneSpeaker, MMDevice aoiSpeaker) GetSpeakerSettings()
        {
            var switchSpeaker = bool.TryParse(Environment.GetEnvironmentVariable("SWITCH_SPEAKER") ?? throw new Exception("環境変数 'SWITCH_SPEAKER' が存在しません。"), out var v) ? v : throw new FormatException("無効な環境変数です: 'SWITCH_SPEAKER'");
            if (!switchSpeaker)
            {
                return (switchSpeaker, null, null);
            }

            var deviceEnumerator = new MMDeviceEnumerator();
            var endpoints = deviceEnumerator
                .EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active)
                .ToList();

            var akaneSpeakerName = Environment.GetEnvironmentVariable("AKANE_SPEAKER") ?? throw new Exception("環境変数 'AKANE_SPEAKER' が存在しません。");
            var aoiSpeakerName = Environment.GetEnvironmentVariable("AOI_SPEAKER") ?? throw new Exception("環境変数 'AOI_SPEAKER' が存在しません。");
            var akaneDevice = endpoints.FirstOrDefault(ep => ep.DeviceInterfaceFriendlyName == akaneSpeakerName) ?? throw new Exception($"オーディオデバイス '{akaneSpeakerName}' が見つかりません。");
            var aoiDevice = endpoints.FirstOrDefault(ep => ep.DeviceInterfaceFriendlyName == aoiSpeakerName) ?? throw new Exception($"オーディオデバイス '{aoiSpeakerName}' が見つかりません。");

            return (switchSpeaker, akaneDevice, aoiDevice);
        }

        private static async Task RunCommand(string command, string payload)
        {
            switch (command)
            {
                case "SPEAK":
                    var request = JsonSerializer.Deserialize<SpeakRequest>(payload);
                    await SpeakAsync(request.SisterType, request.Message);
                    break;
                case "STOP":
                    await StopAsync();
                    break;
            }
        }

        public static void InitializeEditorHost()
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
                    _ttsControl.StartHost();
                }

                _ttsControl.Connect();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        public static void EnsureEditorConnected()
        {
            try
            {
                if (_ttsControl.Status == HostStatus.NotConnected)
                {
                    _ttsControl.Connect();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        public static async Task SpeakAsync(Kotonoha sister, string message)
        {
            EnsureEditorConnected();
            SwitchSpeakerTo(sister);

            try
            {
                await WaitForStatusAsync(HostStatus.Idle);

                switch (sister)
                {
                    case Kotonoha.Akane:
                        _ttsControl.CurrentVoicePresetName = "琴葉 茜";
                        break;
                    case Kotonoha.Aoi:
                        _ttsControl.CurrentVoicePresetName = "琴葉 葵";
                        break;
                }

                _ttsControl.Text = message;
                _ttsControl.Play();

                await WaitForStatusAsync(HostStatus.Idle);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static async Task StopAsync()
        {
            EnsureEditorConnected();

            if (_ttsControl.Status == HostStatus.Idle)
            {
                return;
            }

            try
            {
                _ttsControl.Stop();

                await WaitForStatusAsync(HostStatus.Idle);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static async Task WaitForStatusAsync(HostStatus status)
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

        private static void SwitchSpeakerTo(Kotonoha sister)
        {
            if (!_switchSpeaker)
            {
                return;
            }

            var deviceEnumerator = new MMDeviceEnumerator();
            switch (sister)
            {
                case Kotonoha.Akane:
                    deviceEnumerator.SetDefaultAudioEndpoint(_akaneDevice);
                    break;
                case Kotonoha.Aoi:
                    deviceEnumerator.SetDefaultAudioEndpoint(_aoiDevice);
                    break;
            }
        }
    }
}

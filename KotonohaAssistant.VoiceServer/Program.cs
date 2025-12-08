using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AI.Talk;
using AI.Talk.Editor.Api;
using CoreAudio;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using Newtonsoft.Json;

namespace KotonohaAssistant.VoiceServer
{
    /// <summary>
    /// A.I. VOICEのエディターを操作するためのサーバー
    /// エディタのAPIが.NET Frameworkにしか対応していないのので
    /// Blazor/CLIアプリからはこのサーバーを介してボイスの読み上げを行う
    /// </summary>
    internal class Program
    {
        private static readonly string AppDirectory = EnvVarUtils.TraverseEnvFileFolder(AppContext.BaseDirectory) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotonoha Assistant");
        private static readonly string LogPath = Path.Combine(AppDirectory, "log.voiceserver.txt");
        private static readonly TtsControl TtsControl = new TtsControl();
        private static readonly int WaitCheckInterval = 250;
        private static readonly int WaitTimeout = 15 * 1000;
        private static readonly ILogger Logger = new Logger(LogPath, isConsoleLoggingEnabled: true);

        private static bool EnableChannelSwitching;

        /// <summary>
        /// スピーカー
        /// </summary>
        private static MMDevice SpeakerDevice;

        /// <summary>
        /// デフォルトのボリューム設定
        /// </summary>
        private static readonly float[] InitialVolumeLevelScalars = new float[2];

        static async Task Main(string[] args)
        {
            // load .env
            DotNetEnv.Env.TraversePath().Load();

            EnableChannelSwitching = bool.TryParse(Environment.GetEnvironmentVariable("ENABLE_CHANNEL_SWITCHING"), out var enableChannelSwitching) && enableChannelSwitching;
            SpeakerDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (SpeakerDevice is null)
            {
                Console.WriteLine("No audio output devices found. Press any key to exit.");
                Console.ReadKey();
                return;
            }

            if (SpeakerDevice.AudioEndpointVolume.Channels.Count >= 2)
            {
                InitialVolumeLevelScalars[0] = SpeakerDevice.AudioEndpointVolume.Channels[0].VolumeLevelScalar;
                InitialVolumeLevelScalars[1] = SpeakerDevice.AudioEndpointVolume.Channels[1].VolumeLevelScalar;
            }

            Console.WriteLine("Initializing editor host...");
            InitializeEditorHost();

            Console.WriteLine("Named Pipe Server is starting...");

            // 同時接続を処理するため接続のたびにタスクとして管理する
            var tasks = new List<Task>();

            // クライアントの接続を待ち続ける
            while (true)
            {
                var pipeServer = new NamedPipeServerStream(Const.VoiceEditorPipeName, PipeDirection.InOut, 10, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                Console.WriteLine("Waiting for client connection...");

                try
                {
                    await pipeServer.WaitForConnectionAsync();
                    Console.WriteLine("Client connected.");

                    // 新しい接続を処理するタスクを開始
                    var clientTask = HandleClientAsync(pipeServer);
                    tasks.Add(clientTask);
                }
                catch (IOException)
                {
                    // 接続エラー処理
                    // reader/writerのdisposeの関係で毎回発生するため一旦無視する
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
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
                    case "SPEAK":
                        var speakRequest = JsonConvert.DeserializeObject<SpeakRequest>(payload);
                        await SpeakAsync(speakRequest.SisterType, speakRequest.Emotion, speakRequest.Message);
                        break;
                    case "STOP":
                        await StopAsync();
                        break;
                    case "EXPORT":
                        var exportRequest = JsonConvert.DeserializeObject<ExportVoiceRequest>(payload);
                        await ExportVoiceAsync(exportRequest);
                        break;
                }
            }

            (string command, string payload) ParseRequest(string input)
            {
                var m = Regex.Match(input, @"^(?<command>[^:]+):?(?<payload>.*)$");
                return (m.Groups["command"].Value, m.Groups["payload"].Value);
            }
        }

        public static void InitializeEditorHost()
        {
            var availableHosts = TtsControl.GetAvailableHostNames();
            if (!availableHosts.Any())
            {
                throw new Exception("利用可能なホストが存在しません。");
            }

            try
            {
                var host = availableHosts.First();
                TtsControl.Initialize(host);
                TtsControl.Connect();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                throw;
            }
        }

        public static void EnsureEditorConnected()
        {
            try
            {
                if (TtsControl.Status == HostStatus.NotConnected)
                {
                    TtsControl.Connect();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                throw;
            }
        }

        public static async Task SpeakAsync(Kotonoha sister, Emotion emotion, string message)
        {
            EnsureEditorConnected();
            SwitchSpeakerChannelVolumeLevels(sister);

            try
            {
                await WaitForStatusAsync(HostStatus.Idle);

                var presetName = GetPresetName(sister);
                TtsControl.CurrentVoicePresetName = presetName;
                ChangeStyle(presetName, emotion);

                TtsControl.Text = message;
                TtsControl.Play();

                await WaitForStatusAsync(HostStatus.Idle);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }

            ResetChannelVolumeLevels();
        }

        public static async Task StopAsync()
        {
            EnsureEditorConnected();

            if (TtsControl.Status == HostStatus.Idle)
            {
                return;
            }

            try
            {
                TtsControl.Stop();

                await WaitForStatusAsync(HostStatus.Idle);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        public static async Task ExportVoiceAsync(ExportVoiceRequest request)
        {
            EnsureEditorConnected();
            SwitchSpeakerChannelVolumeLevels(request.SisterType);

            try
            {
                await WaitForStatusAsync(HostStatus.Idle);

                var presetName = GetPresetName(request.SisterType);
                TtsControl.CurrentVoicePresetName = presetName;
                ChangeStyle(presetName, request.Emotion);

                var dir = Path.GetDirectoryName(request.SavePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                TtsControl.Text = request.Message;
                TtsControl.SaveAudioToFile(request.SavePath);

                await WaitForStatusAsync(HostStatus.Idle);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }

            ResetChannelVolumeLevels();
        }

        private static async Task WaitForStatusAsync(HostStatus status)
        {
            var startTime = DateTime.Now;

            while (TtsControl.Status != status)
            {
                // タイムアウトチェック
                if ((DateTime.Now - startTime).TotalMilliseconds > WaitTimeout)
                {
                    throw new TimeoutException();
                }

                await Task.Delay(WaitCheckInterval);
            }
        }

        public static void ChangeStyle(string presetName, Emotion emotion)
        {
            var presetValue = TtsControl.GetVoicePreset(presetName);
            var preset = JsonConvert.DeserializeObject<VoicePreset>(presetValue);

            switch (emotion)
            {
                case Emotion.Calm:
                    preset.Styles["J"].Value = 0;
                    preset.Styles["A"].Value = 0;
                    preset.Styles["S"].Value = 0;
                    break;
                case Emotion.Joy:
                    preset.Styles["J"].Value = 0.15;
                    preset.Styles["A"].Value = 0;
                    preset.Styles["S"].Value = 0;
                    break;
                case Emotion.Anger:
                    preset.Styles["J"].Value = 0;
                    preset.Styles["A"].Value = 0.3;
                    preset.Styles["S"].Value = 0;
                    break;
                case Emotion.Sadness:
                    preset.Styles["J"].Value = 0;
                    preset.Styles["A"].Value = 0;
                    preset.Styles["S"].Value = 0.3;
                    break;
            }

            var newPreset = JsonConvert.SerializeObject(preset);

            TtsControl.SetVoicePreset(newPreset);
        }

        private static void SwitchSpeakerChannelVolumeLevels(Kotonoha sister)
        {
            if (!EnableChannelSwitching || SpeakerDevice.AudioEndpointVolume.Channels.Count < 2)
            {
                return;
            }

            try
            {
                switch (sister)
                {
                    case Kotonoha.Akane:
                        SpeakerDevice.AudioEndpointVolume.Channels[0].VolumeLevelScalar = 1;
                        SpeakerDevice.AudioEndpointVolume.Channels[1].VolumeLevelScalar = 0;
                        break;
                    case Kotonoha.Aoi:
                        SpeakerDevice.AudioEndpointVolume.Channels[0].VolumeLevelScalar = 0;
                        SpeakerDevice.AudioEndpointVolume.Channels[1].VolumeLevelScalar = 1;
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private static void ResetChannelVolumeLevels()
        {
            if (!EnableChannelSwitching || SpeakerDevice.AudioEndpointVolume.Channels.Count < 2)
            {
                return;
            }

            try
            {
                SpeakerDevice.AudioEndpointVolume.Channels[0].VolumeLevelScalar = InitialVolumeLevelScalars[0];
                SpeakerDevice.AudioEndpointVolume.Channels[1].VolumeLevelScalar = InitialVolumeLevelScalars[1];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private static string GetPresetName(Kotonoha sister)
        {
            switch (sister)
            {
                case Kotonoha.Akane:
                    return "琴葉 茜";
                case Kotonoha.Aoi:
                    return "琴葉 葵";
                default:
                    return TtsControl.CurrentVoicePresetName;
            }
        }
    }
}

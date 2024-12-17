using System;
using System.Linq;
using System.Threading.Tasks;
using AI.Talk.Editor.Api;
using System.IO;
using System.IO.Pipes;

namespace KotonohaAssistant.VoiceServer
{
    internal class Program
    {
        private static readonly TtsControl _ttsControl = new TtsControl();
        private static readonly int _waitCheckInterval = 500;
        private static readonly int _waitTimeout = 15 * 1000;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Initializing editor host...");
            InitializeEditorHost();

            Console.WriteLine("Named Pipe Server is starting...");

            while (true) // クライアントの接続を待ち続ける
            {
                // パイプサーバーを作成
                using (var pipeServer = new NamedPipeServerStream("KotonohaAssistant.VoiceServer", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                {
                    Console.WriteLine("Waiting for client connection...");

                    try
                    {
                        await pipeServer.WaitForConnectionAsync();
                        Console.WriteLine("Client connected.");

                        using (var reader = new StreamReader(pipeServer))
                        using (var writer = new StreamWriter(pipeServer) { AutoFlush = true })
                        {
                            var message = await reader.ReadToEndAsync();

                            await SpeakAsync(message);

                            writer.WriteLine("OK");
                        }
                    }
                    catch (IOException ex)
                    {
                        // 接続エラー処理
                        Console.WriteLine($"IOException: {ex.Message}");
                    }
                    finally
                    {
                        if (pipeServer.IsConnected)
                        {
                            pipeServer.Disconnect();
                        }
                        Console.WriteLine("Client disconnected. Ready for new connection.");
                    }
                }
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
                    throw new Exception("エディターが起動していません。");
                }

                _ttsControl.Connect();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        public static async Task SpeakAsync(string message)
        {
            try
            {
                await WaitForStatusAsync(HostStatus.Idle);

                _ttsControl.CurrentVoicePresetName = "琴葉 茜";
                _ttsControl.Text = message;
                _ttsControl.Play();

                await WaitForStatusAsync(HostStatus.Idle);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
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
    }
}

using KotonohaAssistant.Core.Utils;
using ILogger = KotonohaAssistant.Core.Utils.ILogger;

namespace KotonohaAssistant.Vui
{
    public partial class App : Application
    {
        private readonly ILogger _logger;

        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            _logger = serviceProvider.GetRequiredService<ILogger>();

            // 非同期タスクで発生する未処理例外
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                LogException(e.Exception);
                e.SetObserved();
            };

            // UIスレッドで発生する例外（主に .NET 6 / 7 の MAUI）
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LogException(ex);
                }
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage())
            {
                Title = "KotonohaAssistant.Vui",
                Width = 800,
                Height = 1000,
            };
        }

        private void LogException(Exception ex)
        {
            _logger.LogError(ex);
        }
    }
}

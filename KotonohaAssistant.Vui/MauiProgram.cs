using Microsoft.Extensions.Logging;
using Toolbelt.Blazor.Extensions.DependencyInjection;

namespace KotonohaAssistant.Vui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // load .env
            DotNetEnv.Env.TraversePath().Load();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            builder.Services.AddSpeechRecognition();
            builder.Services.AddConversationService();

            return builder.Build();
        }
    }
}

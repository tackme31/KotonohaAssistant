using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.Vui;

public static class ServiceCollectionExtensions
{
    private static readonly string AppName = "Kotonoha Assistant";
    private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
    private static readonly string DBPath = Path.Combine(AppFolder, "app.db");
    private static readonly string AlarmDBPath = Path.Combine(AppFolder, "alarm.db");
    private static readonly string LogPath = Path.Combine(AppFolder, "log.txt");
    private static readonly string VoicePath = Path.Combine(AppFolder, "alarm voice");

    private static string OpenAIApiKey => GetEnvVar("OPENAI_API_KEY");
    private static string OpenAIModel = GetEnvVar("OPENAI_MODEL");
    private static string GoogleApiKey => GetEnvVar("GOOGLE_API_KEY");
    private static string CalendarId => GetEnvVar("CALENDAR_ID");
    private static string OwmApiKey => GetEnvVar("OWM_API_KEY");
    private static double OwmLat => double.TryParse(GetEnvVar("OWM_LAT"), out var owmLat) ? owmLat : throw new FormatException($"無効な環境変数です: 'OWM_LAT'");
    private static double OwmLon => double.TryParse(GetEnvVar("OWM_LON"), out var owmLon) ? owmLon : throw new FormatException($"無効な環境変数です: 'OWM_LON'");
    private static string AlarmSoundFile => GetEnvVar("ALARM_SOUND_FILE");

    public static void AddConversationService(this IServiceCollection services)
    {
        if (!Directory.Exists(AppFolder))
        {
            Directory.CreateDirectory(AppFolder);
        }

        services.AddSingleton<Core.Utils.ILogger>(new Logger(LogPath, isConsoleLoggingEnabled: false));
        services.AddSingleton<IAlarmRepository>(new AlarmRepository(AlarmDBPath));
        services.AddSingleton<ICalendarEventRepository>(new CalendarEventRepository(GoogleApiKey, CalendarId));
        services.AddSingleton<IWeatherRepository>(new WeatherRepository(OwmApiKey));
        services.AddSingleton<IChatMessageRepository>(new ChatMessageRepository(DBPath));
        services.AddSingleton<IChatCompletionRepository>(new ChatCompletionRepository(OpenAIModel, OpenAIApiKey));
        services.AddSingleton<ITimerService>(sp => new TimerService(AlarmSoundFile, sp.GetRequiredService<Core.Utils.ILogger>()));

        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<Core.Utils.ILogger>();
            var calendarRepository = sp.GetRequiredService<ICalendarEventRepository>();
            var weatherRepository = sp.GetRequiredService<IWeatherRepository>();
            var chatMessageRepository = sp.GetRequiredService<IChatMessageRepository>();
            var chatCompletionRepository = sp.GetRequiredService<IChatCompletionRepository>();
            var timerService = sp.GetRequiredService<ITimerService>();

            // 利用する関数一覧
            var functions = new ToolFunction[]
            {
                new CallMaster(VoicePath, logger),
                new StopAlarm(logger),
                new StartTimer(timerService, logger),
                new StopTimer(timerService, logger),
                new CreateCalendarEvent(calendarRepository, logger),
                new GetCalendarEvent(calendarRepository, logger),
                new GetWeather(weatherRepository, (OwmLat, OwmLon), logger),
                new ForgetMemory(logger),
            };

            return  new ConversationService(
                chatMessageRepository,
                chatCompletionRepository,
                functions,
                logger);
        });
    }

    private static string GetEnvVar(string name) => Environment.GetEnvironmentVariable(name) ?? throw new Exception($"環境変数'{name}'が見つかりません。");
}

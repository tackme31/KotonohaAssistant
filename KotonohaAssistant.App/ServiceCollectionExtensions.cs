using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using OpenAI.Chat;

namespace KotonohaAssistant.App;

public static class ServiceCollectionExtensions
{
    private static readonly string AppName = "Kotonoha Assistant";
    private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
    private static readonly string DBPath = Path.Combine(AppFolder, "app.db");
    private static readonly string AlarmDBPath = Path.Combine(AppFolder, "alarm.db");

    private static string OpenAIApiKey => GetEnvVar("OPENAI_API_KEY");
    private static string GoogleApiKey => GetEnvVar("GOOGLE_API_KEY");
    private static string CalendarId => GetEnvVar("CALENDAR_ID");
    private static string OwmApiKey => GetEnvVar("OWM_API_KEY");

    public static void AddConversationService(this IServiceCollection services)
    {
        if (!Directory.Exists(AppFolder))
        {
            Directory.CreateDirectory(AppFolder);
        }

        services.AddSingleton<IAlarmRepository>(new AlarmRepository(AlarmDBPath));
        services.AddSingleton<ICalendarEventRepository>(new CalendarEventRepository(GoogleApiKey, CalendarId));
        services.AddSingleton<IWeatherRepository>(new WeatherRepository(OwmApiKey));
        services.AddSingleton<IChatMessageRepositoriy>(new ChatMessageRepositoriy(DBPath));
        services.AddSingleton<IAlarmService, AlarmService>();
        services.AddSingleton<ITimerService, TimerService>();
        services.AddSingleton<IAlarmService>(sl =>
        {
            var alarmRepository = sl.GetService<IAlarmRepository>();
            var alarmService = new AlarmService(alarmRepository);

            // アラームをスタート
            alarmService.Start();

            return alarmService;
        });

        services.AddSingleton(sl =>
        {
            var calendarRepository = sl.GetService<ICalendarEventRepository>();
            var weatherRepository = sl.GetService<IWeatherRepository>();
            var chatMessageRepository = sl.GetService<IChatMessageRepositoriy>();
            var alarmService = sl.GetService<IAlarmService>();
            var timerService = sl.GetService<ITimerService>();

            // 利用する関数一覧
            var functions = new ToolFunction[]
            {
                new CallMaster(alarmService),
                new StopAlarm(alarmService),
                new StartTimer(timerService),
                new StopTimer(timerService),
                new CreateCalendarEvent(calendarRepository),
                new GetCalendarEvent(calendarRepository),
                new GetWeather(weatherRepository),
                new ForgetMemory(),
            };

            var options = new ChatCompletionOptions()
            {
                AllowParallelToolCalls = true
            };
            foreach (var function in functions)
            {
                options.Tools.Add(function.CreateChatTool());
            }

            var chatCompletionRepository = new ChatCompletionRepository(
                Settings.ModelName,
                OpenAIApiKey,
                options);

            return  new ConversationService(
                chatMessageRepository,
                chatCompletionRepository,
                functions,
                akaneBehaviour: Behaviour.Default,
                aoiBehaviour: Behaviour.Default);
        });
    }

    private static string GetEnvVar(string name) => Environment.GetEnvironmentVariable(name) ?? throw new Exception($"環境変数'{name}'が見つかりません。");
}

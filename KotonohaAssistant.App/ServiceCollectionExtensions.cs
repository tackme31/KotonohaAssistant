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

    public static void AddConversationService(this IServiceCollection services)
    {
        var alarmService = new AlarmService(new AlarmRepository(AlarmDBPath));
        var timerService = new TimerService();
        // 利用する関数一覧
        var functions = new ToolFunction[]
        {
            new CallMaster(alarmService),
            new StopAlarm(alarmService),
            new StartTimer(timerService),
            new StopTimer(timerService),
            new CreateCalendarEvent(),
            new GetCalendarEvent(new CalendarEventRepository(GoogleApiKey, CalendarId)),
            new GetWeather(),
            new TurnOnHeater(),
            new ForgetMemory(),
        };
        // 怠け癖の対象外の関数一覧
        var excludeFunctionNamesFromLazyMode = new[]
        {
            nameof(StartTimer),
            nameof(StopTimer),
            nameof(ForgetMemory),
        };
        var chatMessageRepository = CreateChatMessageRepository();
        var chatCompletionRepository = CreateChatCompletionRepository(functions);
        var service = new ConversationService(
            chatMessageRepository,
            chatCompletionRepository,
            functions,
            excludeFunctionNamesFromLazyMode,
            akaneBehaviour: Behaviour.Default,
            aoiBehaviour: Behaviour.Default);

        services.AddSingleton(service);

        // アラームをスタート
        alarmService.Start();
    }

    private static IChatMessageRepositoriy CreateChatMessageRepository()
    {
        if (!Directory.Exists(AppFolder))
        {
            Directory.CreateDirectory(AppFolder);
        }

        return new ChatMessageRepositoriy(DBPath);
    }

    private static IChatCompletionRepository CreateChatCompletionRepository(IEnumerable<ToolFunction> functions)
    {

        var options = new ChatCompletionOptions();
        foreach (var function in functions)
        {
            options.Tools.Add(function.CreateChatTool());
        }
        return new ChatCompletionRepository(
            Settings.ModelName,
            OpenAIApiKey,
            options);
    }

    private static string GetEnvVar(string name) => Environment.GetEnvironmentVariable(name) ?? throw new Exception($"環境変数'{name}'が見つかりません。");
}

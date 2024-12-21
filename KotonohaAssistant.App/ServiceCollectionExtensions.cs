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

    public static void AddConversationService(this IServiceCollection services)
    {
        var calendarEventRepository = new CalendarEventRepository(
            GetEnvVar("GOOGLE_API_KEY"),
            GetEnvVar("CALENDAR_ID"));

        // 利用する関数一覧
        var functions = new ToolFunction[]
        {
            new CallMaster(),
            new StartTimer(),
            new CreateCalendarEvent(),
            new GetCalendarEvent(calendarEventRepository),
            new GetWeather(),
            new TurnOnHeater(),
            new ForgetMemory(),
        };
        // 怠け癖の対象外の関数一覧
        var excludeFunctionNamesFromLazyMode = new[]
        {
            nameof(StartTimer),
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
    }

    private static IChatMessageRepositoriy CreateChatMessageRepository()
    {
        var dbPath = Path.Combine(AppFolder, "app.db");
        if (!Directory.Exists(AppFolder))
        {
            Directory.CreateDirectory(AppFolder);
        }

        return new ChatMessageRepositoriy(dbPath);
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
            GetEnvVar("OPENAI_API_KEY"),
            options);
    }

    private static string GetEnvVar(string name) => Environment.GetEnvironmentVariable(name) ?? throw new Exception($"環境変数'{name}'が見つかりません。");
}

using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

// load .env
DotNetEnv.Env.TraversePath().Load();

var calendarEventRepository = new CalendarEventRepository(
    Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? string.Empty,
    Environment.GetEnvironmentVariable("CALENDAR_ID") ?? string.Empty);

// 利用可能な関数
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
// 怠け癖の対象外の関数
List<string> excludeFunctionNamesFromLazyMode =
[
    nameof(StartTimer),
    nameof(ForgetMemory)
];

// DBの保存先
var appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotonoha Assistant");
var dbPath = Path.Combine(appDirectory, "app.cli.db");
if (!Directory.Exists(appDirectory))
{
    Directory.CreateDirectory(appDirectory);
}

var chatMessageRepository = new ChatMessageRepositoriy(dbPath);

var options = new ChatCompletionOptions();
foreach (var function in functions)
{
    options.Tools.Add(function.CreateChatTool());
}

var chatCompletionRepository = new ChatCompletionRepository(
    modelName: "gpt-4o-mini",
    Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty,
    options);

var service = new ConversationService(
    chatMessageRepository,
    chatCompletionRepository,
    functions,
    excludeFunctionNamesFromLazyMode,
    akaneBehaviour: Behaviour.Default,
    aoiBehaviour: Behaviour.Default);

await service.LoadLatestConversation();

foreach (var text in service.GetAllMessageTexts())
{
    Console.WriteLine(text);
}

try
{
    using var voiceClient = new VoiceClient();

    while (true)
    {
        Console.Write("私: ");
        var stdin = Console.ReadLine();
        var input = "私: " + stdin;

        await foreach (var result in service.TalkWithKotonohaSisters(input))
        {
            var name = result.Sister switch
            {
                Kotonoha.Akane => "茜: ",
                Kotonoha.Aoi => "葵: ",
                _ => string.Empty
            };

            if (result.Functions is not null)
            {
                foreach (var function in result.Functions)
                {
                    Console.WriteLine($"[FUNCTION CALLING]: {function.Name}({string.Join(", ", function.Arguments.Select(arg => $"{arg.Key}={arg.Value}"))})");
                    Console.WriteLine($"[FUNCTION RETURNS]: {function.Result}");

                }
            }

            Console.Write(name);
            Console.WriteLine(result.Message);

            await voiceClient.SpeakAsync(result.Sister, result.Message);
        }
    }
}
catch (Exception ex)
{
    throw;
}
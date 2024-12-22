using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Alarm;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

// load .env
DotNetEnv.Env.TraversePath().Load();

var modelName = "gpt-4o-mini";
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception("");
var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? throw new Exception("");
var calendarId = Environment.GetEnvironmentVariable("CALENDAR_ID") ?? throw new Exception("");
var appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotonoha Assistant");
var dbPath = Path.Combine(appDirectory, "app.cli.db");
var alarmDBPath = Path.Combine(appDirectory, "alarm.db");

// DBの保存先
if (!Directory.Exists(appDirectory))
{
    Directory.CreateDirectory(appDirectory);
}

// 利用可能な関数
var functions = new List<ToolFunction>
{
    new CallMaster(new AlarmRepository(alarmDBPath), new ChatCompletionRepository(modelName, openAiApiKey)),
    new StopAlarm(new AlarmRepository(alarmDBPath)),
    new StartTimer(),
    new CreateCalendarEvent(),
    new GetCalendarEvent(new CalendarEventRepository(googleApiKey, calendarId)),
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

var chatMessageRepository = new ChatMessageRepositoriy(dbPath);
var options = new ChatCompletionOptions
{
    AllowParallelToolCalls = true
};
functions.ForEach(f => options.Tools.Add(f.CreateChatTool()));
var chatCompletionRepository = new ChatCompletionRepository(modelName, openAiApiKey, options);

var service = new ConversationService(
    chatMessageRepository,
    chatCompletionRepository,
    functions,
    excludeFunctionNamesFromLazyMode,
    akaneBehaviour: Behaviour.Default,
    aoiBehaviour: Behaviour.Default);

//await service.LoadLatestConversation();

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
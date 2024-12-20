using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;

// load .env
DotNetEnv.Env.TraversePath().Load();

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelName = "gpt-4o-mini";

var calendarEventService = new CalendarEventService(
    Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? string.Empty,
    Environment.GetEnvironmentVariable("CALENDAR_ID") ?? string.Empty);

// 利用可能な関数
var functions = new ToolFunction[]
{
    new CallMaster(),
    new StartTimer(),
    new CreateCalendarEvent(),
    new GetCalendarEvent(calendarEventService),
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

using var voiceClient = new VoiceClient();
var service = new ConversationService(apiKey, modelName, functions, excludeFunctionNamesFromLazyMode);

try
{
    while (true)
    {
        Console.Write("私: ");
        var stdin = Console.ReadLine();
        var input = "私: " + stdin;

        await foreach (var result in service.TalkingWithKotonohaSisters(input))
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
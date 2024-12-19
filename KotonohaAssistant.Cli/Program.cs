using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;

// load .env
DotNetEnv.Env.TraversePath().Load();

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelName = "gpt-4o-mini";

List<ToolFunction> functions =
[
    new CallMaster(),
    new StartTimer(),
    new CreateCalendarEvent(),
    new GetCalendarEvent(),
    new GetWeather(),
    new TurnOnHeater(),
    new ForgetMemory(),
];

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
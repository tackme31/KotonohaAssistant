using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Services;

// load .env
DotNetEnv.Env.TraversePath().Load();

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
var modelName = "gpt-4o-mini";

List<ToolFunction> functions =
[
/*        new SetAlarm(),
        new StartTimer(),
        new CreateCalendarEvent(),
        new GetCalendarEvent(),
        new GetWeather(),
        new TurnOnHeater(),
        new ForgetMemory(),*/
];

// 怠け癖の対象外の関数
List<string> excludeFunctionNamesFromLazyMode =
[
/*        nameof(StartTimer),
        nameof(ForgetMemory)*/
];

var service = new ConversationService(apiKey, modelName, functions, excludeFunctionNamesFromLazyMode);

try
{
    while (true)
    {
        Console.Write("私: ");
        var stdin = Console.ReadLine();
        var input = "私: " + stdin;

        await foreach (var text in service.SpeakAI(input))
        {
            Console.WriteLine(text);
        }
    }
}
catch (Exception)
{

}
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Extensions;
using KotonohaAssistant.Core.Utils;

// load .env
DotNetEnv.Env.TraversePath().Load();

var modelName = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? throw new Exception();
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? throw new Exception();
var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? throw new Exception();
var calendarId = Environment.GetEnvironmentVariable("CALENDAR_ID") ?? throw new Exception();
var owmApiKey = Environment.GetEnvironmentVariable("OWM_API_KEY") ?? throw new Exception();
_ = double.TryParse(Environment.GetEnvironmentVariable("OWM_LAT"), out var owmLat) ? true : throw new Exception();
_ = double.TryParse(Environment.GetEnvironmentVariable("OWM_LON"), out var owmLon) ? true : throw new Exception();
var alarmSoundFile = Environment.GetEnvironmentVariable("ALARM_SOUND_FILE") ?? throw new Exception();
var appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotonoha Assistant");
var dbPath = Path.Combine(appDirectory, "app.cli.db");
var alarmDBPath = Path.Combine(appDirectory, "alarm.db");
var logPath = Path.Combine(appDirectory, "log.cli.txt");

// DBの保存先
if (!Directory.Exists(appDirectory))
{
    Directory.CreateDirectory(appDirectory);
}

// 利用可能な関数
var logger = new Logger(logPath, isConsoleLoggingEnabled: true);
var timerService = new TimerService(alarmSoundFile, logger);
var alarmService = new AlarmService(new AlarmRepository(alarmDBPath), alarmSoundFile, logger);
var calendarRepository = new CalendarEventRepository(googleApiKey, calendarId);
using var weatherRepository = new WeatherRepository(owmApiKey);
var functions = new List<ToolFunction>
{
    new CallMaster(alarmService, logger),
    new StopAlarm(alarmService, logger),
    new StartTimer(timerService, logger),
    new StopTimer(timerService, logger),
    new CreateCalendarEvent(calendarRepository, logger),
    new GetCalendarEvent(calendarRepository, logger),
    new GetWeather(weatherRepository, (owmLat, owmLon), logger),
    new ForgetMemory(logger),
};

var assistantDataRepository = new AssistantDataRepository(dbPath);
var assistantRepository = new AssistantRepository(openAiApiKey);

var service = new ConversationService(
    assistantDataRepository,
    assistantRepository,
    functions,
    logger,
    akaneBehaviour: Behaviour.Default,
    aoiBehaviour: Behaviour.Default);

alarmService.Start();

foreach (var (sister, message) in await service.GetAllMessages())
{
    var name = sister?.ToDisplayName() ?? "私";
    Console.WriteLine($"{name}: {message}");
}

try
{
    using var voiceClient = new VoiceClient();

    while (true)
    {
        Console.Write("私: ");
        var input = Console.ReadLine();
        if (input is null)
        {
            continue;
        }

        await foreach (var result in service.TalkWithKotonohaSisters(input))
        {
            if (result.Functions is not null)
            {
                foreach (var function in result.Functions)
                {
                    Console.WriteLine($"[FUNCTION CALLING]: {function.Name}({string.Join(", ", function.Arguments.Select(arg => $"{arg.Key}={arg.Value}"))})");
                    Console.WriteLine($"[FUNCTION RETURNS]: {function.Result}");
                }
            }

            Console.WriteLine($"{result.Sister.ToDisplayName()}: {result.Message}");

            await voiceClient.SpeakAsync(result.Sister, result.Emotion, result.Message);
        }
    }
}
catch (Exception ex)
{
    throw;
}
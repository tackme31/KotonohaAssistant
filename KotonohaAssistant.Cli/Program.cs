using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Cli;
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
var appDirectory = EnvVarUtils.TraverseEnvFileFolder(AppContext.BaseDirectory) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotonoha Assistant");
var dbPath = Path.Combine(appDirectory, "app.cli.db");
var alarmDBPath = Path.Combine(appDirectory, "alarm.db");
var logPath = Path.Combine(appDirectory, "log.cli.txt");
var voicePath = Path.Combine(appDirectory, "alarm voice");

var enableCalendarFunction = EnvUtils.GetBooleanValueOrDefault("ENABLE_CALENDAR_FUNCTION", false);
var enableWeatherFunction = EnvUtils.GetBooleanValueOrDefault("ENABLE_WEATHER_FUNCTION", false);

// DBの保存先
if (!Directory.Exists(appDirectory))
{
    Directory.CreateDirectory(appDirectory);
}

// リポジトリ周り
var logger = new Logger(logPath, isConsoleLoggingEnabled: true);
var calendarRepository = new CalendarEventRepository(googleApiKey, calendarId);
using var weatherRepository = new WeatherRepository(owmApiKey);
var chatMessageRepository = new ChatMessageRepository(dbPath);
var chatCompletionRepository = new ChatCompletionRepository(modelName, openAiApiKey);

// 利用可能な関数
var functions = new List<ToolFunction>
{
    new CallMaster(voicePath, logger),
    new StopAlarm(logger),
    new StartTimer(logger),
    new StopTimer(logger),
    new ForgetMemory(logger)
};

if (enableCalendarFunction)
{
    functions.AddRange([
        new CreateCalendarEvent(calendarRepository, logger),
        new GetCalendarEvent(calendarRepository, logger)
    ]);
}

if (enableWeatherFunction)
{
    functions.AddRange([
        new GetWeather(weatherRepository, (owmLat, owmLon), logger)
    ]);
}

var sisterSwitchingService = new SisterSwitchingService();
var functionsDictionary = functions.ToDictionary(f => f.GetType().Name);
var lazyModeHandler = new LazyModeHandler(functionsDictionary, logger);
var service = new ConversationService(
    chatMessageRepository,
    chatCompletionRepository,
    functions,
    sisterSwitchingService,
    lazyModeHandler,
    logger);

foreach (var (sister, message) in service.GetAllMessages())
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
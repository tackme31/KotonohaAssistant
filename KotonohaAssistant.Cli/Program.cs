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
var dbPath = Path.Combine(appDirectory, "app.db");
var alarmDBPath = Path.Combine(appDirectory, "alarm.db");
var logPath = Path.Combine(appDirectory, "log.cli.txt");
var voicePath = Path.Combine(appDirectory, "alarm voice");
var promptPath = Path.Combine(appDirectory, "prompts");

var enableCalendarFunction = EnvUtils.GetBooleanValueOrDefault("ENABLE_CALENDAR_FUNCTION", false);
var enableWeatherFunction = EnvUtils.GetBooleanValueOrDefault("ENABLE_WEATHER_FUNCTION", false);
var enableInactivityNotification = EnvUtils.GetBooleanValueOrDefault("ENABLE_INACTIVITY_NOTIFICATION", false);
var lineChannelAccessToken = EnvUtils.GetStringValueOrDefault("LINE_CHANNEL_ACCESS_TOKEN", string.Empty);
var lineUserId = EnvUtils.GetStringValueOrDefault("LINE_USER_ID", string.Empty);
var inactivityNotifyIntervalDays = EnvUtils.GetIntValueOrDefault("INACTIVITY_NOTIFY_INTERVAL_DAYS", 7);
var inactivityNotifyTime = EnvUtils.GetTimeSpanValueOrDefault("INACTIVITY_NOTIFY_TIME", new TimeSpan(9, 0, 0));

// DBの保存先
if (!Directory.Exists(appDirectory))
{
    Directory.CreateDirectory(appDirectory);
}

// リポジトリ周り
var logger = new Logger(logPath, isConsoleLoggingEnabled: false);
var chatMessageRepository = new ChatMessageRepository(dbPath);
var chatCompletionRepository = new ChatCompletionRepository(modelName, openAiApiKey);
var promptRepository = new PromptRepository(promptPath);
using var voiceClient = new VoiceClient();
using var alarmClient = new AlarmClient();

// 利用可能な関数
var functions = new List<ToolFunction>
{
    new SetAlarm(promptRepository, voicePath, voiceClient, alarmClient, logger),
    new StopAlarm(promptRepository, alarmClient, logger),
    new StartTimer(promptRepository, alarmClient, logger),
    new StopTimer(promptRepository, alarmClient, logger),
    new ForgetMemory(promptRepository, new SystemRandomGenerator(), logger)
};

if (enableCalendarFunction)
{
    var calendarRepository = new CalendarEventRepository(googleApiKey, calendarId);
    functions.AddRange([
        new CreateCalendarEvent(promptRepository, calendarRepository, logger),
        new GetCalendarEvent(promptRepository, calendarRepository, logger)
    ]);
}

if (enableWeatherFunction)
{
    var weatherRepository = new WeatherRepository(owmApiKey, logger);
    functions.AddRange([
        new GetWeather(promptRepository, weatherRepository, (owmLat, owmLon), logger)
    ]);
}

var functionsDictionary = functions.ToDictionary(f => f.GetType().Name);
var lazyModeHandler = new LazyModeHandler(functionsDictionary, logger);
var service = new ConversationService(
    promptRepository,
    chatMessageRepository,
    chatCompletionRepository,
    functions,
    lazyModeHandler,
    logger);

await service.LoadLatestConversation();
foreach (var (sister, message) in service.GetAllMessages())
{
    var name = sister?.ToDisplayName() ?? "私";
    Console.WriteLine($"{name}: {message}");
}

// InactivityNotificationServiceの開始
if (enableInactivityNotification)
{
    // LineMessagingRepositoryの作成
    ILineMessagingRepository lineRepository;
    if (!string.IsNullOrEmpty(lineChannelAccessToken) && !string.IsNullOrEmpty(lineUserId))
    {
        lineRepository = new LineMessagingRepository(lineChannelAccessToken, logger);
    }
    else
    {
        lineRepository = new NullLineMessagingRepository(logger);
    }

    var inactivityNotificationService = new InactivityNotificationService(
        chatMessageRepository,
        chatCompletionRepository,
        functions,
        promptRepository,
        logger,
        lineRepository,
        lineUserId ?? string.Empty);
    inactivityNotificationService.Start(TimeSpan.FromDays(inactivityNotifyIntervalDays), inactivityNotifyTime);
    logger.LogInformation($"[Inactivity] InactivityNotificationService started. Interval: {inactivityNotifyIntervalDays} days, Time: {inactivityNotifyTime}");
}

try
{
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
catch (Exception)
{
    throw;
}

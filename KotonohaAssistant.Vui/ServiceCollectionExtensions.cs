using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace KotonohaAssistant.Vui;

public static class ServiceCollectionExtensions
{
    private static readonly string AppName = "Kotonoha Assistant";
    private static readonly string AppFolder = EnvVarUtils.TraverseEnvFileFolder(AppContext.BaseDirectory) ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
    private static readonly string DBPath = Path.Combine(AppFolder, "app.db");
    private static readonly string AlarmDBPath = Path.Combine(AppFolder, "alarm.db");
    private static readonly string LogPath = Path.Combine(AppFolder, "log.txt");
    private static readonly string VoicePath = Path.Combine(AppFolder, "alarm voice");
    private static readonly string PromptPath = Path.Combine(AppFolder, "prompts");

    private static string OpenAIApiKey => GetEnvVar("OPENAI_API_KEY");
    private static string OpenAIModel = GetEnvVar("OPENAI_MODEL");
    private static string GoogleApiKey => GetEnvVar("GOOGLE_API_KEY");
    private static string CalendarId => GetEnvVar("CALENDAR_ID");
    private static string OwmApiKey => GetEnvVar("OWM_API_KEY");
    private static double OwmLat => double.TryParse(GetEnvVar("OWM_LAT"), out var owmLat) ? owmLat : throw new FormatException($"無効な環境変数です: 'OWM_LAT'");
    private static double OwmLon => double.TryParse(GetEnvVar("OWM_LON"), out var owmLon) ? owmLon : throw new FormatException($"無効な環境変数です: 'OWM_LON'");

    private static bool EnableCalendarFunction => GetBoolVarOrDefault("ENABLE_CALENDAR_FUNCTION", false);
    private static bool EnableWeatherFunction => GetBoolVarOrDefault("ENABLE_WEATHER_FUNCTION", false);
    private static bool EnableInactivityNotification => GetBoolVarOrDefault("ENABLE_INACTIVITY_NOTIFICATION", false);
    private static string LineChannelAccessToken => GetStringVarOrDefault("LINE_CHANNEL_ACCESS_TOKEN", string.Empty);
    private static string LineUserId => GetStringVarOrDefault("LINE_USER_ID", string.Empty);
    private static int InactivityNotifyIntervalDays => GetIntVarOrDefault("INACTIVITY_NOTIFY_INTERVAL_DAYS", 7);
    private static TimeSpan InactivityNotifyTime => GetTimeSpanVarOrDefault("INACTIVITY_NOTIFY_TIME", new TimeSpan(9, 0, 0));

    public static void AddConversationService(this IServiceCollection services)
    {
        if (!Directory.Exists(AppFolder))
        {
            Directory.CreateDirectory(AppFolder);
        }

        services.AddSingleton<ILogger>(new Logger(LogPath, isConsoleLoggingEnabled: false));
        services.AddSingleton<IAlarmRepository>(_ => new AlarmRepository(AlarmDBPath));

        if (EnableCalendarFunction)
        {
            services.AddSingleton<ICalendarEventRepository>(_ => new CalendarEventRepository(GoogleApiKey, CalendarId));
        }

        if (EnableWeatherFunction)
        {
            services.AddSingleton<IWeatherRepository>(_ => new WeatherRepository(OwmApiKey));
        }

        // InactivityNotificationServiceの登録
        if (EnableInactivityNotification)
        {
            // LineMessagingRepositoryの登録
            services.AddSingleton<ILineMessagingRepository>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger>();
                if (!string.IsNullOrEmpty(LineChannelAccessToken) && !string.IsNullOrEmpty(LineUserId))
                {
                    return new LineMessagingRepository(LineChannelAccessToken, logger);
                }
                return new NullLineMessagingRepository(logger);
            });

            // InactivityNotificationServiceの登録
            services.AddSingleton<IInactivityNotificationService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger>();
                var chatMessageRepository = sp.GetRequiredService<IChatMessageRepository>();
                var chatCompletionRepository = sp.GetRequiredService<IChatCompletionRepository>();
                var promptRepository = sp.GetRequiredService<IPromptRepository>();
                var lineRepository = sp.GetRequiredService<ILineMessagingRepository>();
                var functions = GetAvailableFunctions(sp);

                var inactivityService = new InactivityNotificationService(
                    chatMessageRepository,
                    chatCompletionRepository,
                    functions,
                    promptRepository,
                    logger,
                    lineRepository,
                    LineUserId ?? string.Empty);

                inactivityService.Start(TimeSpan.FromDays(InactivityNotifyIntervalDays), InactivityNotifyTime);
                logger.LogInformation($"[Inactivity] InactivityNotificationService started. Interval: {InactivityNotifyIntervalDays} days, Time: {InactivityNotifyTime}");

                return inactivityService;
            });
        }

        services.AddSingleton<IChatMessageRepository>(_ => new ChatMessageRepository(DBPath));
        services.AddSingleton<IChatCompletionRepository>(_ => new ChatCompletionRepository(OpenAIModel, OpenAIApiKey));
        services.AddSingleton<IPromptRepository>(_ => new PromptRepository(PromptPath));
        services.AddSingleton<ISisterSwitchingService, SisterSwitchingService>();

        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger>();
            var promptRepository = sp.GetRequiredService<IPromptRepository>();
            var chatMessageRepository = sp.GetRequiredService<IChatMessageRepository>();
            var chatCompletionRepository = sp.GetRequiredService<IChatCompletionRepository>();
            var sisterSwitchingService = sp.GetRequiredService<ISisterSwitchingService>();

            // 利用する関数一覧
            var functions = GetAvailableFunctions(sp);

            // LazyModeHandlerの作成（関数辞書が必要）
            var functionsDictionary = functions.ToDictionary(f => f.GetType().Name);
            var lazyModeHandler = new LazyModeHandler(functionsDictionary, logger);

            return new ConversationService(
                promptRepository,
                chatMessageRepository,
                chatCompletionRepository,
                functions,
                sisterSwitchingService,
                lazyModeHandler,
                logger);
        });
    }

    private static List<ToolFunction> GetAvailableFunctions(IServiceProvider sp)
    {
        var logger = sp.GetRequiredService<ILogger>();
        var promptRepository = sp.GetRequiredService<IPromptRepository>();
        var chatMessageRepository = sp.GetRequiredService<IChatMessageRepository>();
        var chatCompletionRepository = sp.GetRequiredService<IChatCompletionRepository>();
        var sisterSwitchingService = sp.GetRequiredService<ISisterSwitchingService>();

        // 利用する関数一覧
        var functions = new List<ToolFunction>
            {
                new CallMaster(promptRepository, VoicePath, logger),
                new StopAlarm(promptRepository, logger),
                new StartTimer(promptRepository, logger),
                new StopTimer(promptRepository, logger),
                new ForgetMemory(promptRepository, new SystemRandomGenerator(), logger),
            };

        if (EnableCalendarFunction)
        {
            var calendarRepository = sp.GetRequiredService<ICalendarEventRepository>();
            functions.AddRange([
                new CreateCalendarEvent(promptRepository, calendarRepository, logger),
                    new GetCalendarEvent(promptRepository, calendarRepository, logger)
            ]);
        }

        if (EnableWeatherFunction)
        {
            var weatherRepository = sp.GetRequiredService<IWeatherRepository>();
            functions.AddRange([
                new GetWeather(promptRepository, weatherRepository, (OwmLat, OwmLon), logger)
            ]);
        }

        return functions;
    }

    private static string GetEnvVar(string name) => Environment.GetEnvironmentVariable(name) ?? throw new Exception($"環境変数'{name}'が見つかりません。");

    private static string GetStringVarOrDefault(string key, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    private static bool GetBoolVarOrDefault(string key, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }
        return defaultValue;
    }

    private static int GetIntVarOrDefault(string key, int defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (int.TryParse(value, out int result))
        {
            return result;
        }
        return defaultValue;
    }

    private static TimeSpan GetTimeSpanVarOrDefault(string key, TimeSpan defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (TimeSpan.TryParse(value, out TimeSpan result))
        {
            return result;
        }
        return defaultValue;
    }
}

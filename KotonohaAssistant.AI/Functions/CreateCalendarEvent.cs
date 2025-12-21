using System.ComponentModel;
using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class CreateCalendarEvent(IPromptRepository promptRepository, ICalendarEventRepository calendarEventRepository, ILogger logger)
    : ToolFunction(logger)
{
    private record Parameters(
        [property: Description("作成する予定のタイトル")]
        string Title,
        [property: Description("予定の日にち。形式はyyyy/MM/dd")]
        string Date,
        [property: Description("予定の時間。HH:mm形式。不明な場合はnull")]
        string? Time);

    public override string Description => promptRepository.CreateCalendarEventDescription;
    protected override Type ParameterType => typeof(Parameters);

    private readonly ICalendarEventRepository _calendarEventRepository = calendarEventRepository;

    protected override bool ValidateParameters<T>(T parameters)
    {
        if (parameters is not Parameters args)
        {
            return false;
        }

        // タイトルの検証
        if (string.IsNullOrWhiteSpace(args.Title))
        {
            Logger.LogWarning("Title is required");
            return false;
        }

        // 日付の検証
        if (!DateTime.TryParse(args.Date, out _))
        {
            Logger.LogWarning($"Invalid date format: {args.Date}");
            return false;
        }

        // 時間の検証（オプショナル）
        if (args.Time != null && !TimeSpan.TryParse(args.Time, out _))
        {
            Logger.LogWarning($"Invalid time format: {args.Time}");
            return false;
        }

        return true;
    }

    public override async Task<string?> Invoke(JsonDocument argumentsDoc, IReadOnlyConversationState state)
    {
        var args = Deserialize<Parameters>(argumentsDoc);
        if (args is null)
        {
            return null;
        }

        var title = args.Title;
        var date = DateTime.Parse(args.Date);
        TimeSpan? time = args.Time != null ? TimeSpan.Parse(args.Time) : null;

        try
        {
            _ = await _calendarEventRepository.CreateEventAsync(title, date, time);

            return "予定を作成しました。";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);

            return "予定の作成に失敗しました。";
        }
    }
}

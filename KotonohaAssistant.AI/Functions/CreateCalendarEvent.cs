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

    public override async Task<string?> Invoke(JsonDocument argumentsDoc, ConversationState state)
    {
        var args = Deserialize<Parameters>(argumentsDoc);
        if (args is null)
        {
            return null;
        }

        var title = args.Title;
        if (!DateTime.TryParse(args.Date, out var date))
        {
            return null;
        }

        if (!TimeSpan.TryParse(args.Time, out var time))
        {
            return null;
        }

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

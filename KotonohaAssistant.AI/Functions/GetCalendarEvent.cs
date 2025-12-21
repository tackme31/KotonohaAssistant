using System.ComponentModel;
using System.Text;
using System.Text.Json;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class GetCalendarEvent(IPromptRepository promptRepository, ICalendarEventRepository calendarEventRepository, ILogger logger)
    : ToolFunction(logger)
{
    protected record Parameters(
        [property: Description("予定を取得する日にち。形式はyyyy/MM/dd")]
        string Date);

    public override string Description => promptRepository.GetCalendarEventDescription;
    protected override Type ParameterType => typeof(Parameters);

    private readonly ICalendarEventRepository _calendarEventService = calendarEventRepository;

    public override async Task<string?> Invoke(JsonDocument argumentsDoc, ConversationState state)
    {
        var args = Deserialize<Parameters>(argumentsDoc);
        if (args is null)
        {
            return null;
        }

        if (!DateTime.TryParse(args.Date, out var date))
        {
            return null;
        }

        try
        {
            var events = await _calendarEventService.GetEventsAsync(date);
            var sb = new StringBuilder();
            sb.AppendLine($"## {date:M月d日}の予定");
            if (!events.Any())
            {
                return $"予定はありません。";
            }

            foreach (var eventItem in events)
            {
                var start = eventItem.Start.DateTimeDateTimeOffset;
                var end = eventItem.End.DateTimeDateTimeOffset;
                if (start is null || end is null)
                {
                    sb.AppendLine($"- {eventItem.Summary}");
                    continue;
                }

                // 今日をまたぐ予定
                if (!IsToday(start.Value) && !IsToday(end.Value) &&
                    start.Value < DateTime.Now && DateTime.Now < end.Value)
                {
                    sb.AppendLine($"- {eventItem.Summary}");
                    continue;
                }

                if (IsToday(start.Value) && !IsToday(end.Value))
                {
                    sb.AppendLine($"- [{start:HH:mm}から] {eventItem.Summary}");
                    continue;
                }

                if (!IsToday(start.Value) && IsToday(end.Value))
                {
                    sb.AppendLine($"- [{end:HH:mm}まで] {eventItem.Summary}");
                    continue;
                }

                if (start == end)
                {
                    sb.AppendLine($"- [{end:HH:mm}] {eventItem.Summary}");
                    continue;
                }

                sb.AppendLine($"- [{start:HH:mm}から{end:HH:mm}まで] {eventItem.Summary}");
            }

            return sb.ToString();
            ;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);

            return "予定が取得できませんでした";
        }

        static bool IsToday(DateTimeOffset datetime)
        {
            return datetime.Month == DateTime.Today.Month
                && datetime.Day == DateTime.Today.Day;
        }
    }
}

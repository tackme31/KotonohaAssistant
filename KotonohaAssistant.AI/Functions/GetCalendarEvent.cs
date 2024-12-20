using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Services;
using System.Text;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class GetCalendarEvent(ICalendarEventService calendarEventService) : ToolFunction
{
    private readonly ICalendarEventService _calendarEventService = calendarEventService;

    public override string Description => """
指定された日の予定をGoogleカレンダーから取得します。予定を尋ねられたときに呼び出されます。

呼び出される例:「明日の予定教えて」「今日の15時からなにか予定あったっけ？」
""";

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "date": {
            "type": "string",
            "description": "予定を取得する日にち。形式はyyyy/MM/dd"
        }
    },
    "required": [ "date" ],
    "additionalProperties": false
}
""";

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var date = doc.RootElement.GetDateTimeProperty("date");
        if (date is null)
        {
            return false;
        }
        arguments["date"] = date;

        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments)
    {
        try
        {
            var date = (DateTime)arguments["date"];
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

                if (!IsToday(start.Value) && !IsToday(end.Value))
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

            return sb.ToString(); ;
        }
        catch (Exception)
        {
            return "予定が取得できませんでした";
        }

        static bool IsToday(DateTimeOffset datetime)
        {
            return datetime.Month == DateTime.Today.Month
                && datetime.Day == DateTime.Today.Day;
        }
    }
}
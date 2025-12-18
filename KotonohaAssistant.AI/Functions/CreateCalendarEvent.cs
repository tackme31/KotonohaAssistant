using System.Text.Json;
using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Functions;

public class CreateCalendarEvent(IPromptRepository promptRepository, ICalendarEventRepository calendarEventRepository, ILogger logger) : ToolFunction(logger)
{
    public override string Description => promptRepository.CreateCalendarEventDescription;

    public override string Parameters => """
{
    "type": "object",
    "properties": {
        "title": {
            "type": "string",
            "description": "作成する予定のタイトル"
        },
        "date": {
            "type": "string",
            "description": "予定の日にち。yyyy/MM/dd形式"
        },
        "time": {
            "type": "string",
            "description": "予定の時間。HH:mm形式。不明な場合はnull"
        }
    },
    "required": [ "title", "date" ],
    "additionalProperties": false
}
""";

    private readonly ICalendarEventRepository _calendarEventRepository = calendarEventRepository;

    public override bool TryParseArguments(JsonDocument doc, out IDictionary<string, object> arguments)
    {
        arguments = new Dictionary<string, object>();

        var title = doc.RootElement.GetStringProperty("title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }
        arguments["title"] = title;

        var date = doc.RootElement.GetDateTimeProperty("date");
        if (date is null)
        {
            return false;
        }
        arguments["date"] = date;

        var time = doc.RootElement.GetTimeSpanProperty("time");
        if (time is not null)
        {
            arguments["time"] = time;
        }

        return true;
    }

    public override async Task<string> Invoke(IDictionary<string, object> arguments, ConversationState_ state)
    {
        var title = (string)arguments["title"];
        var date = (DateTime)arguments["date"];
        var time = arguments.ContainsKey("time") ? (TimeSpan?)arguments["time"] : default(TimeSpan?);

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

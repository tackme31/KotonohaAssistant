using KotonohaAssistant.AI.Extensions;
using System.Text.Json;

namespace KotonohaAssistant.AI.Functions;

public class GetCalendarEvent : ToolFunction
{
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

  public override string Invoke(JsonDocument arguments)
  {
    var date = arguments.RootElement.GetStringProperty("date");
    
    System.Console.WriteLine($"  => {nameof(GetCalendarEvent)}(date={date})");

    return """
[終日] 原神アップデート日
[15:00 - 16:00] 通院
[18:00 - 20:00] Amazon荷物受け取り
""";
  }
}
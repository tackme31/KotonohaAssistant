using System.Text.Json;

namespace KotonohaAssistant.Functions;

class GetCalendarEvent : ToolFunction
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
[終日] 宝石の13巻発売日
15:00 - 16:00 通院
21:00 - 21:30 原神バージョン予告放送
""";
  }
}
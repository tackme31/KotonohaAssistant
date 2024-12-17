using System.Text.Json;

namespace KotonohaAssistant.Functions;

class GetWeather : ToolFunction
{
  public override string Description => """
指定された日の天気を取得します。天気を尋ねられたときに呼び出されます。

呼び出される例:「今日の天気は？」「今日は傘必要そう？」「今って晴れてる？」
""";

  public override string Parameters =>  """
{
    "type": "object",
    "properties": {
        "date": {
            "type": "string",
            "description": "天気を取得する日にち。形式はyyyy/MM/dd"
        }
    },
    "required": [ "date" ],
    "additionalProperties": false
}
""";

  public override string Invoke(JsonDocument arguments)
  {
    var date = arguments.RootElement.GetStringProperty("date");

    System.Console.WriteLine($"  => {nameof(GetWeather)}(date={date})");
    return """
- 10時: 晴れ
- 12時: 晴れ
- 14時: 晴れ
- 16時: 曇り
- 18時: 雨
- 20時: 雨
- 22時: 雨
""";
  }
}
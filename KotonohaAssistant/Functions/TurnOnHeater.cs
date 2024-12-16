using System.Text.Json;

namespace KotonohaAssistant.Functions;

class TurnOnHeater : ToolFunction
{
  public override string Description => """
暖房の設定を依頼されたときに呼び出されます。
暖房の設定に成功した場合はokを返し、失敗した場合はngを返します。

呼び出される:「18時ごろに部屋温めておいて」「朝7時に暖房設定しておいて」

時間が不明な場合は、呼び出さず、聞き返してください。
""";

  public override string Parameters => """
{
    "type": "object",
    "properties": {
        "time": {
            "type": "string",
            "description": "暖房の設定時間。HH:mm形式"
        }
    },
    "required": [ "time" ],
    "additionalProperties": false
}
""";

  public override string Invoke(JsonDocument arguments)
  {
    var time = arguments.RootElement.GetStringProperty("time");

    System.Console.WriteLine($"  => {nameof(TurnOnHeater)}(time={time})");
    return "ok";
  }
}
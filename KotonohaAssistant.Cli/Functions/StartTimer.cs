using System.Text.Json;

namespace KotonohaAssistant.Functions;

class StartTimer : ToolFunction
{
  public override string Description => """
タイマーの設定を依頼されたときに呼び出されます。
タイマーの設定に成功した場合はokを返し、失敗した場合はngを返します。

呼び出される例: 「タイマー3分」「90秒数えて」

秒数が不明な場合は呼び出さず、聞き返してください。
""";

  public override string Parameters => """
{
    "type": "object",
    "properties": {
        "seconds": {
            "type": "number",
            "description": "タイマーの秒数"
        }
    },
    "required": [ "seconds" ],
    "additionalProperties": false
}
""";

  public override string Invoke(JsonDocument arguments)
  {
    var seconds = arguments.RootElement.GetIntProperty("seconds");

    System.Console.WriteLine($"  => {nameof(StartTimer)}(seconds={seconds})");

    return "ok";
  }
}
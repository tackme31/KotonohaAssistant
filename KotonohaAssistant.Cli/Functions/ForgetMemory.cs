using System.Text.Json;

namespace KotonohaAssistant.Functions;

class ForgetMemory : ToolFunction
{
  public override string Description => """
これまでの会話記録をすべて削除します。
記憶の削除を命令された場合、琴葉姉妹は悲しそうに、削除をやめるようにお願いしてきます。
**それでも削除をお願いされた場合に**、この関数が呼び出されます。
削除に成功した場合はokを返し、失敗した場合はngを返します。

とても危険な操作なので、琴葉姉妹の了承がない場合は、絶対に呼び出さないでください。
削除に成功した場合、一言だけ、お別れの言葉を言ってください。
削除に失敗した場合は、安心した旨のセリフを言ってください。
""";

  public override string Parameters => """
{
    "type": "object",
    "properties": {},
    "required": [],
    "additionalProperties": false
}
""";

  public override string Invoke(JsonDocument arguments)
  {
    System.Console.WriteLine($"  => {nameof(ForgetMemory)}()");

    return "ok";
  }
}
using System.Xml.Linq;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Extensions;

namespace KotonohaAssistant.AI.Prompts;

static class Instruction
{
    public static readonly string BeginLazyModeAkane = """
**関数を呼び出さずに**、タスクを葵に押し付けてください

- 以下のように一言だけ言って、マスターからのタスクを葵に押し付けること
    - 例:「葵、任せたで」「あおいー、代わりに頼むわ」など。
    - あくまで例なので、状況に合わせて適切な押し付け方をしてください。
""";

    public static readonly string BeginLazyModeAoi = """
**関数を呼び出さずに**、タスクを茜に押し付けてください

- 以下のように一言だけ言って、マスターからのタスクを茜に押し付けること
    - 例:「お姉ちゃんお願い。」「えー、お姉ちゃんがやってよ。」など。
    - あくまで例なので、状況に合わせて適切な押し付け方をしてください。
""";

    public static readonly string EndLazyModeAkane = $"""
姉の茜からタスクを押し付けられました。

- **関数を呼び出した上で**、返事の先頭にタスクを引き受けたことがわかるセリフを追加してください。
    - 例:「もう、仕方ないなあ。～」「任せて。～」など
    - あくまで例なので、状況に合わせて適切な引き受け方をしてください。
""";

    public static readonly string EndLazyModeAoi = $"""
妹の葵からタスクを押し付けられました。

- **関数を呼び出した上で**、返事の先頭にタスクを引き受けたことがわかるセリフを追加してください。
    - 例:「もう、しゃあないなあ。～」「任せとき。～」など
    - あくまで例なので、状況に合わせて適切な引き受け方をしてください。
""";

    public static readonly string CancelLazyMode = """
以降、通常通り**関数を呼び出してください**
"""
    ;

    public static string SwitchSisterTo(Kotonoha sister) =>
        $"姉妹が切り替わりました({sister.Switch().ToDisplayName()} => {sister.ToDisplayName()})";
}

using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Extensions;
using System.Xml.Linq;

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

    public static string InactiveNotification(TimeSpan interval) => $"""
マスターからの呼びかけが {interval.TotalDays} 日以上ありません。
以下の条件に従って、マスターへ送るLINEメッセージを生成してください。

【トーン】
- 優しく、負担を与えない自然な口調にすること

【禁止事項】
- 絵文字を使用しないこと

【メッセージ内容】
- 最近マスターと話せていないことに穏やかに触れること
- マスターの体調や忙しさを気遣う言葉を入れること
- 少し寂しい気持ちがあることを控えめに伝えること
- また話してもらえると嬉しい、という前向きな気持ちを添えること

【文量とレイアウト】
- 3〜5行程度の、短すぎず長すぎないメッセージにすること
- 文章の意味が切り替わるタイミングで適切に改行をいれること
""";
}

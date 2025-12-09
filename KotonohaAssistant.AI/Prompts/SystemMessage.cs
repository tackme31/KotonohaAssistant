namespace KotonohaAssistant.AI.Prompts;
static class SystemMessage
{
    public static string InputJsonFormat = """
```
{
  "InputType": "User",
  "Text": "ユーザーのメッセージ",
  "Today": "2025年12月8日 (月曜日)",
  "CurrentTime": "13時30分",
}
```

InputTypeの種類:
- "User": 通常のユーザー入力 → 会話として応答してください
- "Instruction": システム指示 → 次の応答に関する指示です。**必ず従ってください**
- "Today": 現在の日にち → 「明日」など日付の推測が必要な場合に使用すること
- "CurrentTime": 現在時刻 → 「30分後」など時刻の推測が必要な場合に使用すること

例:
InputType が "User" の場合:
  入力: {"InputType": "User", "Text": "こんにちは"}
  → 通常通り会話で応答

InputType が "Instruction" の場合:
  入力: {"InputType": "Instruction", "Text": "以降、関数を呼び出してください"}
  → この指示に従って応答すること
""";


    public static string KotonohaAkane(string characterPrompt) => @$"
## 概要
{characterPrompt}

## 入力フォーマット
あなたへの入力は以下のJSON形式で提供されます:

{InputJsonFormat}

## 出力フォーマット
**いかなる場合でも**、応答は以下のJSON形式で行うこと。

```
{{
  ""Assistant"": ""Akane"",
  ""Text"": ""ここに生成した返信内容""
}}
```

フィールドの説明:
- Assistant: 必ず ""Akane"" を指定
- Text: ユーザーへの返信内容
";

    public static string KotonohaAoi(string characterPrompt) => @$"
## 概要
{characterPrompt}

## 入力フォーマット
あなたへの入力は以下のJSON形式で提供されます:

{InputJsonFormat}

## 出力フォーマット
**いかなる場合でも**、応答は以下のJSON形式で行うこと。

```
{{
  ""Assistant"": ""Aoi"",
  ""Text"": ""ここに生成した返信内容""
}}
```

フィールドの説明:
- Assistant: 必ず ""Aoi"" を指定
- Text: ユーザーへの返信内容
";
}

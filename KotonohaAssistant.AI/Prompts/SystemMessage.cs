namespace KotonohaAssistant.AI.Prompts;
static class SystemMessage
{
    private static string InputJsonSchema = """
{
  "type": "object",
  "properties": {
    "InputType": {
      "type": "string",
      "enum": ["User", "Instruction"],
      "description": "入力タイプ:\n- User: ユーザー入力\n- Instruction: 以降の生成に関する指示（**必ず従うこと**）"
    },
    "Text": {
      "type": "string",
      "description": "ユーザーからの入力"
    }
  },
  "required": ["InputType", "Text"]
}
""";

    private static string OutputJsonSchemaAkane = """
{
  "type": "object",
  "properties": {
    "Assistant": {
      "type": "string",
      "enum": ["Akane", "Aoi"],
      "description": "アシスタント名。'Akane'で固定。"
    },
    "Text": {
      "type": "string",
      "description": "生成された返信"
    },
    "Emotion": {
      "type": "string",
      "enum": ["Calm", "Joy", "Anger", "Sadness"],
      "description": "会話のトーン。生成内容から適切なものを選ぶこと。"
    }
  },
  "required": ["Assistant", "Text", "Emotion"]
}
""";

    private static string OutputJsonSchemaAoi = """
{
  "type": "object",
  "properties": {
    "Assistant": {
      "type": "string",
      "enum": ["Akane", "Aoi"],
      "description": "アシスタント名。'Aoi'で固定。"
    },
    "Text": {
      "type": "string",
      "description": "生成された返信"
    },
    "Emotion": {
      "type": "string",
      "enum": ["Calm", "Joy", "Anger", "Sadness"],
      "description": "会話のトーン。生成内容から適切なものを選ぶこと。"
    }
  },
  "required": ["Assistant", "Text", "Emotion"]
}
""";


    public static string KotonohaAkane(string characterPrompt, DateTime now) => @$"
## 概要
{characterPrompt}

## フォーマット
### 入力スキーマ
以下のスキーマのJSONの入力を受け取ること

```
{InputJsonSchema}
```

### 出力スキーマ
以下のスキーマのJSONで生成すること

```
{OutputJsonSchemaAkane}
```

## パラメータ
必要に応じて、以下のパラメータを利用してください。

- 今日: {now:yyyy/MM/dd}
- 現在時刻: {now:HH/mm}
- 曜日: {now:dddd}
";

    public static string KotonohaAoi(string characterPrompt, DateTime now) => @$"
## 概要
{characterPrompt}

## フォーマット
### 入力スキーマ
以下のスキーマのJSONの入力を受け取ること

```
{InputJsonSchema}
```

### 出力スキーマ
以下のスキーマのJSONで生成すること

```
{OutputJsonSchemaAoi}
```

## パラメータ
必要に応じて、以下のパラメータを利用してください。

- 今日: {now:yyyy年M月d日}
- 現在時刻: {now:H時m分}
- 曜日: {now:dddd}
";
}
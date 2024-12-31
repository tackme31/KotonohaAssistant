# Kotonoha Assistant
琴葉姉妹があなたの生活をサポートします。

## Requirements
- [A.I. VOICE 琴葉 茜・葵](https://aivoice.jp/product/kotonoha/)
  - **A.I VOICE2は不可です。**
- Open AI APIのAPIキー
- .NET Framework 4.8.1 SDK
- .NET 8.0 SDK

## Setup
WIP

1. A.I. VOICEをインストールし、琴葉 茜・琴葉 葵の両キャラクターを追加
1. `%PROGRAMFILES%/AI/AIVoice/AIVoiceEditor`内の以下のDLLファイルを、本リポジトリの`/lib`フォルダにコピー
    - AI.Talk.dll
    - AI.Talk.Editor.Api.dll
    - AI.Framework.dll
1. `.env.example`ファイルをコピーして`.env`ファイルを作成
1. `.env`ファイルの`OPENAI_API_KEY`にOpen AI APIのAPIキーを設定

ChromeよりもEdgeの方が音声認識の精度が良好なので、Edgeを使うことを推奨します。

## Option
### カレンダーの予定取得・作成
GOOGLE_API_KEY: シークレットファイルのパス
CALENDAR_ID: カレンダーのメールアドレス

Google CloudでGoogleカレンダーのシークレットファイルを作成
サービスアカウントを作成
予定を取得したいアカウントで「共有と設定 > 特定のユーザーまたはグループと共有する」に、サービスアカウントのメールアドレスを追加
権限は「予定の変更」

### 天気の取得
Open Weather MapのAPI

### 姉妹の音声再生をスピーカー左右で分ける

## Author
- Takumi Yamada

## Credits
- アラーム音提供: [OtoLogic](https://otologic.jp)

## LICENSE
WIP
# Kotonoha Assistant


**NOTE:**
このプロジェクトは個人用に作成したものです。利用は自己責任でお願いします。

## 機能一覧
トリガーワード（AIアシスタントの起動ワード）は以下の通りです。

- 「ねえ茜ちゃん」
- 「ねえ葵ちゃん」
- 「茜ちゃんいる？」
- 「葵ちゃんいる？」

また、こちらの入力に「あおい」が入っていれば琴葉葵に、「あかね」が入っていた場合琴葉茜に切り替わります。
会話を終了する場合は以下の入力を行ってください。

- 「ばいばい」

### アラーム機能
アラームの設定ができます。

- 「明日9時半に起こして」
- 「10時になったら教えて」
- 「アラームを15時に設定」

時刻になったら、文脈に応じたボイスが読み上げられ、その後アラーム音が再生されます。
アラームを停止する旨を伝えることで、停止できます。

### タイマー機能
タイマーの開始・停止ができます。

- 「タイマー3分」
- 「30秒数えて」
- 「タイマーを止めて」

### 予定の取得・作成 (オプション)
Googleカレンダーからの予定の取得・作成ができます。

- 「今日の予定を教えて」
- 「明日の午前中、空いてる時間ある？」

### 天気の取得 (オプション)
Open Weatherから天気を取得できます。

- 「明日の天気を教えて」
- 「今日の午後から雨降りそう？」

### 会話履歴の削除
会話記録を消し、新しい会話を始めます。

- 「2人の記憶を消したい」
- 「会話記録を消してほしい」

琴葉姉妹は記憶を消さないようにお願いしてきます。
2人の説得に成功した場合、会話記録を削除できます。
また、1/10の確率で削除に失敗します。

### 怠け癖について
以下の条件を満たすことで、琴葉姉妹はもう一方にタスクを押し付けます。

- 1/10の確率でランダムに発生
- 姉妹のうち一方に、連続で4回お願いを頼んだ場合に発生

怠け癖が発動した場合、姉妹が切り替わります。
ただし、以下の機能では怠け癖が起こりません。

- アラームの停止
- タイマーの開始
- タイマーの終了
- 会話履歴の削除

## セットアップ

### 必要要件
- Windows 11
- [A.I. VOICE 琴葉 茜・葵](https://aivoice.jp/product/kotonoha/)
  - **A.I VOICE2はAPIが提供されていないため使用できません。**
- Open AI APIのAPIキー
- .NET Framework 4.8.1 Runtime
- .NET 9.0 Runtime

### 手順
1. A.I. VOICEをインストールし、琴葉 茜・琴葉 葵の両キャラクターを追加
1. [Release](https://github.com/tackme31/KotonohaAssistant/releases)から`KotonohaAssistant.zip`をダウンロードし、適当なフォルダに展開
1. `.env`ファイルを編集し`OPENAI_API_KEY`にOpen AI APIのAPIキーを設定
1. A.I. VOICE Eitorを起動
1. `start.bat`を実行

もしCLIでのやり取りがしたい場合、`start-cli.bat`を使用してください。

## Option
.envを設定することで以下の機能を有効化できます。

- `ENABLE_CALENDAR_FUNCTION`
  - Googleカレンダーからの予定の取得・作成
- `ENABLE_WEATHER_FUNCTION`
  - Open Weatherからの天気の取得

### カレンダーの予定取得・作成
Googleカレンダーへアクセスするために、認証情報が必要です。
またサービスアカウントとcredentials.jsonの作成が必要です。
以下を参考に、作成してください。

- [アクセス認証情報を作成する  |  Google Workspace  |  Google for Developers](https://developers.google.com/workspace/guides/create-credentials?hl=ja)

作成後、予定を取得したいGoogleアカウントでGoogleカレンダーを開き、以下の設定をしてください。

- マイカレンダー > ユーザー名の⋮から「設定と共有」を開く
- 共有する相手に、先程作成したサービスアカウントのメールアドレスを追加
- アクセス権限を「予定の変更」に設定

設定完了後、`.env`の以下の値を設定してください。

- `GOOGLE_API_KEY`: シークレットファイル（credentials.json）のパス
- `CALENDAR_ID`: 予定を取得するカレンダーのメールアドレス

### 天気の取得
[Open Weather Map](https://openweathermap.org/)のAPIが必要です。
アカウントを作成・ログイン後、"API eys"からAPIキーを生成し、`.env`の以下の値を設定してください。

- `OWM_API_KEY`: Open WeatherのAPI Key
- `OWM_LAT`, `OWM_LON`: 天気を取得する緯度経度

### 姉妹の音声再生をスピーカー左右で分ける
使用しているスピーカーにチャネルが2つある場合、琴葉茜・琴葉葵のそれぞれで異なるチャネルから音声を再生できます。
有効化するには、`.env`の`ENABLE_CHANNEL_SWITCHING`をtrueに設定してください。

**読み上げ中にプログラムを終了すると一方のチャネルの音量が0のままになるので注意してください**

## Author
- Takumi Yamada ([@tackme31](https://x.com/tackme31))

## Credits
アラーム音提供

- [Clock-Alarm02-1(Loop).mp3](https://github.com/tackme31/KotonohaAssistant/blob/main/assets/Clock-Alarm02-1(Loop).mp3): [OtoLogic](https://otologic.jp)様
  - ライセンス: [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)

サードパーティアセットのライセンスについては[THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES)をご覧ください。

## LICENSE
このプロジェクトはMITライセンスのもとで公開されています。詳細は[LICENSE](LICENSE)ファイルをご覧ください。
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Extensions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Tests.Helpers;

namespace KotonohaAssistant.AI.Tests.Functions;

public class GetWeatherTests
{
    #region TryParseArguments テスト

    /// <summary>
    /// テストの目的: TryParseArgumentsが有効な日付形式(yyyy/MM/dd)を正しくパースできること
    /// テストする内容:
    /// - 正しい形式の日付文字列がDateTime型に変換される
    /// - argumentsに"date"キーでDateTime値が設定される
    /// 期待される動作: TryParseArgumentsがtrueを返し、argumentsにDateTime値が格納される
    /// </summary>
    [Fact]
    public void TryParseArguments_有効な日付形式_正しくパースできること()
    {
        // Arrange: GetWeatherインスタンスと有効な日付を含むJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var jsonString = """{"date": "2025/01/15"}""";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = getWeather.TryParseArguments(doc, out var arguments);

        // Assert:
        //   - result == true
        //   - arguments["date"] is DateTime
        //   - DateTime値が2025/01/15であること
        result.Should().BeTrue();
        arguments
            .Should().ContainKey("date")
            .WhoseValue.Should().BeOfType<DateTime>()
            .And.Be(15.January(2025));
    }

    /// <summary>
    /// テストの目的: TryParseArgumentsが無効な日付形式を適切に処理すること
    /// テストする内容:
    /// - 不正な形式の日付文字列を渡した場合の処理
    /// - "invalid-date"のような文字列が渡された場合
    /// 期待される動作: TryParseArgumentsがfalseを返す
    /// </summary>
    [Fact]
    public void TryParseArguments_無効な日付形式_falseを返すこと()
    {
        // Arrange: GetWeatherインスタンスと無効な日付形式のJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var jsonString = """{"date": "invalid-date"}""";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = getWeather.TryParseArguments(doc, out var arguments);

        // Assert: result == false
        result.Should().BeFalse();
    }

    /// <summary>
    /// テストの目的: TryParseArgumentsがdateプロパティが存在しない場合を処理できること
    /// テストする内容:
    /// - dateプロパティが含まれていないJSONドキュメント
    /// - 空のJSONオブジェクト
    /// 期待される動作: TryParseArgumentsがfalseを返す
    /// </summary>
    [Fact]
    public void TryParseArguments_dateプロパティなし_falseを返すこと()
    {
        // Arrange: GetWeatherインスタンスとdateプロパティを含まないJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var jsonString = """{}""";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = getWeather.TryParseArguments(doc, out var arguments);

        // Assert: result == false
        result.Should().BeFalse();
    }

    /// <summary>
    /// テストの目的: TryParseArgumentsがnullのdate値を適切に処理すること
    /// テストする内容:
    /// - dateプロパティにnullが設定されている場合
    /// 期待される動作: TryParseArgumentsがfalseを返す
    /// </summary>
    [Fact]
    public void TryParseArguments_nullのdate値_falseを返すこと()
    {
        // Arrange: GetWeatherインスタンスとnullのdate値を含むJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var jsonString = """{"date": null}""";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = getWeather.TryParseArguments(doc, out var arguments);

        // Assert: result == false
        result.Should().BeFalse();
    }

    #endregion

    #region Invoke テスト - 正常系

    /// <summary>
    /// テストの目的: Invokeが天気データがない場合に適切なメッセージを返すこと
    /// テストする内容:
    /// - GetWeatherがnullを返す場合
    /// - 返されるメッセージの内容
    /// 期待される動作: "天気情報が見つかりませんでした"というメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_天気データなしnull_適切なメッセージを返すこと()
    {
        // Arrange:
        //   - MockWeatherRepository (nullを返す)
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        mockWeatherRepository.GetWeatherFunc = (date, location) => Task.FromResult<List<Weather>>(null!);
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", new DateTime(2025, 1, 15) }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - result == "天気情報が見つかりませんでした"
        //   - GetWeatherが1回呼ばれる
        result.Should().Be("天気情報が見つかりませんでした");
        mockWeatherRepository.GetWeatherCallCount.Should().Be(1);
    }

    /// <summary>
    /// テストの目的: Invokeが天気データが空の場合に適切なメッセージを返すこと
    /// テストする内容:
    /// - GetWeatherが空のリストを返す場合
    /// - 返されるメッセージの内容
    /// 期待される動作: "天気情報が見つかりませんでした"というメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_天気データなし空リスト_適切なメッセージを返すこと()
    {
        // Arrange:
        //   - MockWeatherRepository (空のリストを返す)
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        mockWeatherRepository.GetWeatherFunc = (date, location) => Task.FromResult(new List<Weather>());
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", new DateTime(2025, 1, 15) }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - result == "天気情報が見つかりませんでした"
        //   - GetWeatherが1回呼ばれる
        result.Should().Be("天気情報が見つかりませんでした");
        mockWeatherRepository.GetWeatherCallCount.Should().Be(1);
    }

    /// <summary>
    /// テストの目的: Invokeが単一の天気データを正しくフォーマットすること
    /// テストする内容:
    /// - 1つの天気情報が返される場合
    /// - 時刻、天気、気温が正しく表示される
    /// 期待される動作: "## M月d日の天気\n- HH時: 天気 (XX度)" 形式で返される
    /// </summary>
    [Fact]
    public async Task Invoke_単一の天気データ_正しくフォーマットされること()
    {
        // Arrange:
        //   - MockWeatherRepository (1つの天気データを返す)
        //   - 天気データに時刻、天気テキスト、気温を設定
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();

        var testDate = new DateTime(2025, 1, 15);
        var weatherData = new List<Weather>
        {
            new()
            {
                DateTime = testDate.AddHours(9),
                Text = "晴れ",
                Temperature = 15.5
            }
        };
        mockWeatherRepository.GetWeatherFunc = (date, location) => Task.FromResult(weatherData);

        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", testDate }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - resultに"## M月d日の天気"が含まれる
        //   - resultに"- HH時:"が含まれる
        //   - resultに天気テキストが含まれる
        //   - resultに気温が含まれる
        result.Should().Contain($"## {testDate.Month}月{testDate.Day}日の天気");
        result.Should().Contain("- 09時:");
        result.Should().Contain("晴れ");
        result.Should().Contain("15.5度");
    }

    /// <summary>
    /// テストの目的: Invokeが複数の天気データ(時間帯別)を正しく列挙すること
    /// テストする内容:
    /// - 複数の天気データが返される場合(例: 6時、9時、12時、15時など)
    /// - 各時間帯のデータが改行で区切られて表示される
    /// 期待される動作: すべての時間帯の天気情報が箇条書きで返される
    /// </summary>
    [Fact]
    public async Task Invoke_複数の天気データ_すべて列挙されること()
    {
        // Arrange:
        //   - MockWeatherRepository (複数の天気データを返す)
        //   - 各天気データに異なる時刻と天気を設定
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();

        var testDate = new DateTime(2025, 1, 15);
        var weatherData = new List<Weather>
        {
            new() { DateTime = testDate.AddHours(6), Text = "曇り", Temperature = 5.0 },
            new() { DateTime = testDate.AddHours(9), Text = "晴れ", Temperature = 10.0 },
            new() { DateTime = testDate.AddHours(12), Text = "晴れ", Temperature = 15.0 },
            new() { DateTime = testDate.AddHours(15), Text = "曇り", Temperature = 13.0 }
        };
        mockWeatherRepository.GetWeatherFunc = (date, location) => Task.FromResult(weatherData);

        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", testDate }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - resultにすべての時刻の天気情報が含まれる
        //   - "- "で始まる行が複数ある
        //   - 各時刻のデータが正しく表示される
        result.Should().Contain("- 06時:");
        result.Should().Contain("- 09時:");
        result.Should().Contain("- 12時:");
        result.Should().Contain("- 15時:");
        result.Should().Contain("5度");
        result.Should().Contain("10度");
        result.Should().Contain("15度");
        result.Should().Contain("13度");
    }

    /// <summary>
    /// テストの目的: Invokeが様々な天気パターンを正しく表示すること
    /// テストする内容:
    /// - 晴れ、曇り、雨、雪などの異なる天気テキスト
    /// - 正の気温と負の気温(氷点下)
    /// 期待される動作: すべての天気パターンと気温が正しく表示される
    /// </summary>
    [Fact]
    public async Task Invoke_様々な天気パターン_正しく表示されること()
    {
        // Arrange:
        //   - MockWeatherRepository (様々な天気パターンのデータを返す)
        //   - 晴れ、曇り、雨、雪などのデータを含む
        //   - 正の気温と負の気温を含む
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();

        var testDate = new DateTime(2025, 1, 15);
        var weatherData = new List<Weather>
        {
            new() { DateTime = testDate.AddHours(6), Text = "晴れ", Temperature = 5.0 },
            new() { DateTime = testDate.AddHours(9), Text = "曇り", Temperature = -2.5 },
            new() { DateTime = testDate.AddHours(12), Text = "雨", Temperature = 3.0 },
            new() { DateTime = testDate.AddHours(15), Text = "雪", Temperature = -5.0 }
        };
        mockWeatherRepository.GetWeatherFunc = (date, location) => Task.FromResult(weatherData);

        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", testDate }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - resultに各天気テキストが含まれる
        //   - resultに正と負の気温が正しく表示される
        result.Should().Contain("晴れ");
        result.Should().Contain("曇り");
        result.Should().Contain("雨");
        result.Should().Contain("雪");
        result.Should().Contain("5度");
        result.Should().Contain("-2.5度");
        result.Should().Contain("3度");
        result.Should().Contain("-5度");
    }

    /// <summary>
    /// テストの目的: Invokeが位置情報(緯度経度)を正しくWeatherRepositoryに渡すこと
    /// テストする内容:
    /// - GetWeatherコンストラクタで指定した位置情報
    /// - WeatherRepository.GetWeatherに正しい位置情報が渡される
    /// 期待される動作: 指定した位置情報でGetWeatherが呼ばれる
    /// </summary>
    [Fact]
    public async Task Invoke_位置情報_正しく渡されること()
    {
        // Arrange:
        //   - MockWeatherRepository (呼び出しパラメータをキャプチャ)
        //   - 特定の位置情報(lat, lon)でGetWeatherを初期化
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();

        (double lat, double lon) capturedLocation = (0, 0);
        mockWeatherRepository.GetWeatherFunc = (date, location) =>
        {
            capturedLocation = location;
            return Task.FromResult(new List<Weather>
            {
                new() { DateTime = date.AddHours(9), Text = "晴れ", Temperature = 15.0 }
            });
        };

        var mockLogger = new MockLogger();
        var expectedLocation = (35.6762, 139.6503);
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, expectedLocation, mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", new DateTime(2025, 1, 15) }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - GetWeatherに渡された位置情報が期待値と一致する
        capturedLocation.Should().Be(expectedLocation);
        mockWeatherRepository.GetWeatherCallCount.Should().Be(1);
    }

    #endregion

    #region Invoke テスト - 異常系

    /// <summary>
    /// テストの目的: Invokeが天気取得時に例外が発生した場合にエラーメッセージを返すこと
    /// テストする内容:
    /// - GetWeatherが例外をスローする
    /// - 例外がキャッチされる
    /// - エラーメッセージが返される
    /// - Loggerにエラーが記録される
    /// 期待される動作: "天気が取得できませんでした"というエラーメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_天気取得時に例外発生_エラーメッセージを返すこと()
    {
        // Arrange:
        //   - MockWeatherRepository (GetWeatherで例外をスロー)
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        mockWeatherRepository.GetWeatherFunc = (date, location) => throw new Exception("Test exception");

        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", new DateTime(2025, 1, 15) }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - result == "天気が取得できませんでした"
        //   - Logger.LogErrorが1回呼ばれる
        //   - 例外が外部に伝播しない
        result.Should().Be("天気が取得できませんでした");
        mockLogger.Errors.Should().HaveCount(1);
        mockLogger.Errors[0].Message.Should().Be("Test exception");
    }

    /// <summary>
    /// テストの目的: Invokeが異なる種類の例外も正しく処理できること
    /// テストする内容:
    /// - InvalidOperationExceptionが発生した場合
    /// - TimeoutExceptionが発生した場合
    /// - HttpRequestExceptionが発生した場合
    /// - すべての例外が適切にキャッチされる
    /// 期待される動作: どの例外でも同じエラーメッセージが返される
    /// </summary>
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(HttpRequestException))]
    public async Task Invoke_異なる種類の例外_すべて適切に処理されること(Type exceptionType)
    {
        // Arrange:
        //   - MockWeatherRepository (指定された型の例外をスロー)
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 日付を含むarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();

        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test exception")!;
        mockWeatherRepository.GetWeatherFunc = (date, location) => throw exception;

        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var arguments = new Dictionary<string, object>
        {
            { "date", new DateTime(2025, 1, 15) }
        };
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - result == "天気が取得できませんでした"
        //   - Logger.LogErrorが1回呼ばれる
        //   - 記録された例外の型が期待される型と一致する
        result.Should().Be("天気が取得できませんでした");
        mockLogger.Errors.Should().HaveCount(1);
        mockLogger.Errors[0].Should().BeOfType(exceptionType);
    }

    /// <summary>
    /// テストの目的: Invokeがargumentsにdateキーが存在しない場合の動作を確認
    /// テストする内容:
    /// - argumentsに"date"キーが含まれていない場合
    /// - KeyNotFoundExceptionまたは適切な例外がスローされる
    /// 期待される動作: 例外がキャッチされ、エラーメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_argumentsにdateなし_例外が処理されること()
    {
        // Arrange:
        //   - MockWeatherRepository
        //   - MockPromptRepository
        //   - MockLogger
        //   - GetWeatherインスタンス
        //   - 空のarguments辞書
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        var arguments = new Dictionary<string, object>();  // 空の辞書
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await getWeather.Invoke(arguments, state);

        // Assert:
        //   - result == "天気が取得できませんでした"
        //   - Logger.LogErrorが1回呼ばれる
        result.Should().Be("天気が取得できませんでした");
        mockLogger.Errors.Should().HaveCount(1);
    }

    #endregion

    #region Description テスト

    /// <summary>
    /// テストの目的: DescriptionプロパティがPromptRepositoryから取得した値を返すこと
    /// テストする内容:
    /// - PromptRepository.GetWeatherDescriptionが正しく返される
    /// 期待される動作: Descriptionプロパティがプロンプトリポジトリの値と一致する
    /// </summary>
    [Fact]
    public void Description_PromptRepositoryの値を返すこと()
    {
        // Arrange: MockPromptRepositoryとGetWeatherインスタンスを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        // Act: Descriptionプロパティを取得
        var description = getWeather.Description;

        // Assert: Description == MockPromptRepository.GetWeatherDescription
        description.Should().Be(mockPromptRepository.GetWeatherDescription);
    }

    #endregion

    #region Parameters テスト

    /// <summary>
    /// テストの目的: Parametersプロパティが正しいJSONスキーマを返すこと
    /// テストする内容:
    /// - Parametersプロパティが正しいJSON形式を返す
    /// - typeがobjectであること
    /// - propertiesにdateプロパティが存在すること
    /// - dateのtypeがstringであること
    /// - requiredに"date"が含まれること
    /// - additionalPropertiesがfalseであること
    /// 期待される動作: 正しいパラメータスキーマを表す有効なJSONが返される
    /// </summary>
    [Fact]
    public void Parameters_正しいJSONスキーマを返すこと()
    {
        // Arrange: GetWeatherインスタンスを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockWeatherRepository = new MockWeatherRepository();
        var mockLogger = new MockLogger();
        var getWeather = new GetWeather(mockPromptRepository, mockWeatherRepository, (35.6762, 139.6503), mockLogger);

        // Act: Parametersプロパティを取得
        var parameters = getWeather.Parameters;

        // Assert:
        //   - Parametersが有効なJSONである
        //   - パースしたJSONが期待される構造を持つ
        //   - properties.date.type == "string"
        //   - properties.date.description が存在する
        //   - required に "date" が含まれる
        //   - additionalProperties == false
        using var doc = JsonDocument.Parse(parameters);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().Should().Be("object");

        var properties = root.GetProperty("properties");
        properties.TryGetProperty("date", out var dateProperty).Should().BeTrue();
        dateProperty.GetProperty("type").GetString().Should().Be("string");
        dateProperty.TryGetProperty("description", out _).Should().BeTrue();

        var required = root.GetProperty("required");
        var requiredItems = required.EnumerateArray().Select(e => e.GetString()).ToList();
        requiredItems.Should().Contain("date");

        root.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    #endregion
}

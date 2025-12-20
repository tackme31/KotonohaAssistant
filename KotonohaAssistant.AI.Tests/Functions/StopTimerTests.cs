using System.Text.Json;
using FluentAssertions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Tests.Helpers;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Tests.Functions;

public class StopTimerTests
{
    #region TryParseArguments テスト

    /// <summary>
    /// テストの目的: TryParseArgumentsが空のJSONドキュメントを正しく処理できること
    /// テストする内容:
    /// - 空のJSONドキュメントを渡した場合にtrueを返す
    /// - argumentsに空の辞書が設定される
    /// 期待される動作: TryParseArgumentsがtrueを返し、argumentsに空の辞書が設定される
    /// </summary>
    [Fact]
    public void TryParseArguments_空のJSONドキュメント_正常に処理できること()
    {
        // Arrange: StopTimerインスタンスと空のJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();
        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        var jsonString = "{}";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = stopTimer.TryParseArguments(doc, out var arguments);

        // Assert:
        //   - result == true
        //   - arguments != null
        //   - arguments.Count == 0
        result.Should().BeTrue();
        arguments.Should().NotBeNull();
        arguments.Should().BeEmpty();
    }

    /// <summary>
    /// テストの目的: TryParseArgumentsがnullでないJSONドキュメントを処理できること
    /// テストする内容:
    /// - プロパティを含むJSONドキュメントを渡した場合の動作
    /// - JSONドキュメントの内容に関わらずtrueを返す
    /// 期待される動作: TryParseArgumentsがtrueを返し、argumentsに空の辞書が設定される
    /// </summary>
    [Fact]
    public void TryParseArguments_プロパティを含むJSONドキュメント_正常に処理できること()
    {
        // Arrange: StopTimerインスタンスとプロパティを含むJSONドキュメントを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();
        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        var jsonString = "{\"foo\": \"bar\", \"number\": 123}";
        using var doc = JsonDocument.Parse(jsonString);

        // Act: TryParseArgumentsを呼び出す
        var result = stopTimer.TryParseArguments(doc, out var arguments);

        // Assert:
        //   - result == true
        //   - arguments != null
        //   - arguments.Count == 0 (プロパティは無視される)
        result.Should().BeTrue();
        arguments.Should().NotBeNull();
        arguments.Should().BeEmpty();
    }

    #endregion

    #region Invoke テスト

    /// <summary>
    /// テストの目的: Invokeがタイマー停止に成功した場合の動作を確認
    /// テストする内容:
    /// - AlarmClient.StopTimer()が正常に完了する
    /// - 成功メッセージが返される
    /// 期待される動作: "タイマーを設定しました"というメッセージが返される
    /// 注意: 実装には "タイマーを停止しました" が適切と思われるが、現在の実装に従う
    /// </summary>
    [Fact]
    public async Task Invoke_タイマー停止成功_成功メッセージを返すこと()
    {
        // Arrange:
        //   - MockAlarmClient (StopTimer成功)
        //   - MockPromptRepository
        //   - MockLogger
        //   - StopTimerインスタンス
        //   - 空のarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();
        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        var arguments = new Dictionary<string, object>();
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await stopTimer.Invoke(arguments, state);

        // Assert:
        //   - result == "タイマーを設定しました"
        //   - AlarmClient.StopTimerが1回呼ばれる
        //   - Loggerにエラーが記録されない
        result.Should().Be("タイマーを設定しました");
        mockAlarmClient.StopTimerCallCount.Should().Be(1);
        mockLogger.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// テストの目的: Invokeがタイマー停止時に例外が発生した場合の動作を確認
    /// テストする内容:
    /// - AlarmClient.StopTimer()が例外をスローする
    /// - 例外がキャッチされる
    /// - エラーメッセージが返される
    /// - Loggerにエラーが記録される
    /// 期待される動作: "タイマーの設定に失敗しました。"というエラーメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_タイマー停止時に例外発生_エラーメッセージを返すこと()
    {
        // Arrange:
        //   - MockAlarmClient (StopTimerで例外をスロー)
        //   - MockPromptRepository
        //   - MockLogger
        //   - StopTimerインスタンス
        //   - 空のarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();
        mockAlarmClient.StopTimerFunc = () => throw new Exception("Test exception");

        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        var arguments = new Dictionary<string, object>();
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await stopTimer.Invoke(arguments, state);

        // Assert:
        //   - result == "タイマーの設定に失敗しました。"
        //   - Logger.LogErrorが1回呼ばれる
        //   - 例外が外部に伝播しない
        result.Should().Be("タイマーの設定に失敗しました。");
        mockLogger.Errors.Should().HaveCount(1);
        mockLogger.Errors[0].Message.Should().Be("Test exception");
    }

    /// <summary>
    /// テストの目的: Invokeが空のargumentsでも正常動作すること
    /// テストする内容:
    /// - argumentsが空の辞書の場合の動作
    /// - タイマー停止処理が正常に実行される
    /// 期待される動作: "タイマーを設定しました"というメッセージが返される
    /// </summary>
    [Fact]
    public async Task Invoke_空のarguments_正常に動作すること()
    {
        // Arrange:
        //   - MockAlarmClient (StopTimer成功)
        //   - MockPromptRepository
        //   - MockLogger
        //   - StopTimerインスタンス
        //   - 空のarguments辞書
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();
        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        var arguments = new Dictionary<string, object>();
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await stopTimer.Invoke(arguments, state);

        // Assert:
        //   - result == "タイマーを設定しました"
        //   - AlarmClient.StopTimerが1回呼ばれる
        result.Should().Be("タイマーを設定しました");
        mockAlarmClient.StopTimerCallCount.Should().Be(1);
    }

    /// <summary>
    /// テストの目的: Invokeが異なる種類の例外も正しく処理できること
    /// テストする内容:
    /// - InvalidOperationExceptionが発生した場合
    /// - TimeoutExceptionが発生した場合
    /// - すべての例外が適切にキャッチされる
    /// 期待される動作: どの例外でも同じエラーメッセージが返される
    /// </summary>
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(TimeoutException))]
    [InlineData(typeof(Exception))]
    public async Task Invoke_異なる種類の例外_すべて適切に処理されること(Type exceptionType)
    {
        // Arrange:
        //   - MockAlarmClient (指定された型の例外をスロー)
        //   - MockPromptRepository
        //   - MockLogger
        //   - StopTimerインスタンス
        //   - 空のarguments
        //   - ConversationState
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();

        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test exception")!;
        mockAlarmClient.StopTimerFunc = () => throw exception;

        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        var arguments = new Dictionary<string, object>();
        var state = TestStateFactory.CreateTestState();

        // Act: Invokeを呼び出す
        var result = await stopTimer.Invoke(arguments, state);

        // Assert:
        //   - result == "タイマーの設定に失敗しました。"
        //   - Logger.LogErrorが1回呼ばれる
        //   - 記録された例外の型が期待される型と一致する
        result.Should().Be("タイマーの設定に失敗しました。");
        mockLogger.Errors.Should().HaveCount(1);
        mockLogger.Errors[0].Should().BeOfType(exceptionType);
    }

    #endregion

    #region CanBeLazy テスト

    /// <summary>
    /// テストの目的: CanBeLazyプロパティがfalseを返すこと
    /// テストする内容:
    /// - StopTimerのCanBeLazyプロパティの値
    /// 期待される動作: CanBeLazyがfalseを返す（タイマー停止は怠けてはいけない操作）
    /// </summary>
    [Fact]
    public void CanBeLazy_常にfalseを返すこと()
    {
        // Arrange: StopTimerインスタンスを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();
        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        // Act: CanBeLazyプロパティを取得
        var canBeLazy = stopTimer.CanBeLazy;

        // Assert: CanBeLazy == false
        canBeLazy.Should().BeFalse();
    }

    #endregion

    #region Description テスト

    /// <summary>
    /// テストの目的: DescriptionプロパティがPromptRepositoryから取得した値を返すこと
    /// テストする内容:
    /// - PromptRepository.StopTimerDescriptionが正しく返される
    /// 期待される動作: Descriptionプロパティがプロンプトリポジトリの値と一致する
    /// </summary>
    [Fact]
    public void Description_PromptRepositoryの値を返すこと()
    {
        // Arrange: MockPromptRepositoryとStopTimerインスタンスを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();
        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        // Act: Descriptionプロパティを取得
        var description = stopTimer.Description;

        // Assert: Description == MockPromptRepository.StopTimerDescription
        description.Should().Be(mockPromptRepository.StopTimerDescription);
    }

    #endregion

    #region Parameters テスト

    /// <summary>
    /// テストの目的: Parametersプロパティが空のJSONスキーマを返すこと
    /// テストする内容:
    /// - Parametersプロパティが正しいJSON形式を返す
    /// - typeがobjectであること
    /// - propertiesが空であること
    /// - requiredが空配列であること
    /// - additionalPropertiesがfalseであること
    /// 期待される動作: 空のパラメータスキーマを表す有効なJSONが返される
    /// </summary>
    [Fact]
    public void Parameters_空のJSONスキーマを返すこと()
    {
        // Arrange: StopTimerインスタンスを準備
        var mockPromptRepository = new MockPromptRepository();
        var mockAlarmClient = new MockAlarmClient();
        var mockLogger = new MockLogger();
        var stopTimer = new StopTimer(mockPromptRepository, mockAlarmClient, mockLogger);

        // Act: Parametersプロパティを取得
        var parameters = stopTimer.Parameters;

        // Assert:
        //   - Parametersが有効なJSONである
        //   - パースしたJSONがtype=object, properties={}, required=[], additionalProperties=falseを持つ
        using var doc = JsonDocument.Parse(parameters);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().Should().Be("object");
        root.GetProperty("properties").EnumerateObject().Should().BeEmpty();
        root.GetProperty("required").EnumerateArray().Should().BeEmpty();
        root.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    #endregion
}

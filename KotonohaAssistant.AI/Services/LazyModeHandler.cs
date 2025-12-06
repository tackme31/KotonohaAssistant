using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Services;

/// <summary>
/// 怠け癖モードの結果
/// </summary>
public class LazyModeResult
{
    /// <summary>
    /// 最終的な生成結果
    /// </summary>
    public required ChatCompletion FinalCompletion { get; set; }

    /// <summary>
    /// 怠け癖が発動したかどうか
    /// </summary>
    public bool WasLazy { get; set; }

    /// <summary>
    /// 怠け癖時のタスク押し付け応答（フロントに表示用）
    /// </summary>
    public ConversationResult? LazyResponse { get; set; }
}

public interface ILazyModeHandler
{
    /// <summary>
    /// 怠け癖モードを処理します
    /// </summary>
    /// <param name="completion">初回の生成結果</param>
    /// <param name="state">会話の状態</param>
    /// <param name="regenerateCompletionAsync">生成を再実行する関数</param>
    /// <returns>怠け癖モードの処理結果</returns>
    Task<LazyModeResult> HandleLazyModeAsync(
        ChatCompletion completion,
        ConversationState state,
        Func<Task<ChatCompletion?>> regenerateCompletionAsync);
}

public class LazyModeHandler : ILazyModeHandler
{
    private const string LogPrefix = "[LazyMode]";

    private readonly IDictionary<string, ToolFunction> _functions;
    private readonly Random _random = new();
    private readonly ILogger _logger;

    public LazyModeHandler(
        IDictionary<string, ToolFunction> functions,
        ILogger logger)
    {
        _functions = functions;
        _logger = logger;
    }

    /// <summary>
    /// 怠け癖モードを処理します
    /// </summary>
    public async Task<LazyModeResult> HandleLazyModeAsync(
        ChatCompletion completion,
        ConversationState state,
        Func<Task<ChatCompletion?>> regenerateCompletionAsync)
    {
        // 怠け癖判定
        if (!ShouldBeLazy(completion, state))
        {
            return new LazyModeResult
            {
                FinalCompletion = completion,
                WasLazy = false,
                LazyResponse = null
            };
        }

        _logger.LogInformation($"{LogPrefix} Lazy mode activated for {state.CurrentSister}.");

        // 怠け癖モード開始
        state.AddLazyModeInstruction();

        // 再度返信を生成（タスクを押し付ける）
        _logger.LogInformation($"{LogPrefix} Generating refusal response...");
        var lazyCompletion = await regenerateCompletionAsync();

        // それでも関数呼び出しされることがあるのでチェック
        if (lazyCompletion is null || lazyCompletion.FinishReason == ChatFinishReason.ToolCalls)
        {
            _logger.LogWarning($"{LogPrefix} Lazy mode cancelled: still received tool calls.");
            state.AddInstruction(Prompts.Instruction.CancelLazyMode);
            return new LazyModeResult
            {
                FinalCompletion = completion,
                WasLazy = false,
                LazyResponse = null
            };
        }

        // 実際に怠けた場合の処理
        // 怠け癖応答を保存
        state.AddAssistantMessage(lazyCompletion);

        // 怠け癖応答をフロントに送信するため保存
        ConversationResult? lazyResponse = null;
        if (ChatResponse.TryParse(lazyCompletion.Content[0].Text, out var response))
        {
            lazyResponse = new ConversationResult
            {
                Message = response?.Text ?? string.Empty,
                Emotion = response?.Emotion ?? Emotion.Calm,
                Sister = response?.Assistant ?? state.CurrentSister
            };
        }

        // 姉妹を切り替えて引き受けるモード
        var previousSister = state.CurrentSister;
        state.AddEndLazyModeInstruction();
        state.SwitchToOtherSister();
        _logger.LogInformation($"{LogPrefix} Switching sister: {previousSister} -> {state.CurrentSister}");

        // 再度生成（引き受ける）
        _logger.LogInformation($"{LogPrefix} Generating acceptance response...");
        var acceptCompletion = await regenerateCompletionAsync();
        if (acceptCompletion is null)
        {
            _logger.LogWarning($"{LogPrefix} Failed to generate acceptance response.");
            // 生成失敗時は元の完了を返す
            return new LazyModeResult
            {
                FinalCompletion = completion,
                WasLazy = false,
                LazyResponse = null
            };
        }

        // 怠けると姉妹が入れ替わるのでカウンターをリセット
        state.ResetPatienceCount();
        _logger.LogInformation($"{LogPrefix} Lazy mode completed successfully.");

        return new LazyModeResult
        {
            FinalCompletion = acceptCompletion,
            WasLazy = true,
            LazyResponse = lazyResponse
        };
    }

    /// <summary>
    /// 怠け癖を発動すべきかどうかを判定します
    /// </summary>
    private bool ShouldBeLazy(ChatCompletion completionValue, IReadOnlyConversationState state)
    {
        // 関数呼び出し以外は怠けない
        if (completionValue.FinishReason != ChatFinishReason.ToolCalls)
        {
            return false;
        }

        // 怠け癖対象外の関数が含まれていたら怠けない
        var targetFunctions = completionValue.ToolCalls
            .Where(toolCall => _functions.ContainsKey(toolCall.FunctionName))
            .Select(toolCall => _functions[toolCall.FunctionName]);
        if (targetFunctions.Any(func => !func.CanBeLazy))
        {
            _logger.LogInformation($"{LogPrefix} Lazy mode skipped: function cannot be lazy.");
            return false;
        }

        // 4回以上同じ方にお願いすると怠ける
        if (state.PatienceCount > 3)
        {
            _logger.LogInformation($"{LogPrefix} Lazy mode triggered: patience count exceeded ({state.PatienceCount}).");
            return true;
        }

        // 1/10の確率で怠け癖発動
        var lazy = _random.NextDouble() < 1d / 10d;
        if (lazy)
        {
            _logger.LogInformation($"{LogPrefix} Lazy mode triggered: random probability.");
        }
        return lazy;
    }
}

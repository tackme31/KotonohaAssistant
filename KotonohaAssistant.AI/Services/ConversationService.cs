using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Utils;
using KotonohaAssistant.Core;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KotonohaAssistant.AI.Services;

public class ConversationService
{
    private readonly ConversationState _state;
    private readonly ChatClient _chatClient;
    private readonly IDictionary<string, ToolFunction> _functions;
    private readonly IList<string> _excludeFunctionNamesFromLazyMode;
    private readonly ChatCompletionOptions _options;
    private readonly Random _r = new();

    public ConversationService(
        string chatApiKey,
        string modelName,
        IList<ToolFunction> availableFunctions,
        IList<string> excludeFunctionNamesFromLazyMode,
        Kotonoha defaultSister = Kotonoha.Akane,
        string? akaneBehaviour = null,
        string? aoiBehaviour = null)
    {
        _state = new ConversationState()
        {
            CurrentSister = defaultSister,
            AkaneBehaviour = akaneBehaviour,
            AoiBehaviour = aoiBehaviour
        };

        // 生成時の参考のためにあらかじめ会話を入れておく
        _state.AddAssistantMessage("葵: はじめまして、マスター。私は琴葉葵。こっちは姉の茜。");
        _state.AddAssistantMessage("茜: 今日からうちらがマスターのことサポートするで。");
        _state.AddUserMessage("私: うん。よろしくね。");

        _chatClient = new ChatClient(modelName, chatApiKey);
        _functions = availableFunctions.ToDictionary(f => f.GetType().Name);
        _excludeFunctionNamesFromLazyMode = excludeFunctionNamesFromLazyMode;
        _options = new ChatCompletionOptions();
        foreach (var function in availableFunctions)
        {
            _options.Tools.Add(function.CreateChatTool());
        }

        _state.PatienceCount = 0;
        _state.LastToolCallSister = 0;
    }

    /// <summary>
    /// 入力したテキストで琴葉姉妹と会話します
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<ConversationResult> TalkingWithKotonohaSisters(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            yield break;
        }

        // 姉妹切り替え
        if (_state.CurrentSister == Kotonoha.Aoi &&
            (input.Contains("茜") || input.Contains("あかね")))
        {
            _state.CurrentSister = Kotonoha.Akane;
            _state.AddHint(Hint.SwitchSisterTo(Kotonoha.Akane));
        }
        if (_state.CurrentSister == Kotonoha.Akane &&
            (input.Contains("葵") || input.Contains("あおい")))
        {
            _state.CurrentSister = Kotonoha.Aoi;
            _state.AddHint(Hint.SwitchSisterTo(Kotonoha.Aoi));
        }

        // 返信を生成
        _state.AddUserMessage($"私: {input}");
        var completion = await CompleteChatAsync(_state);

        // 忍耐値の処理
        if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
        {
            // 連続して同じ方にお願いした場合
            if (_state.LastToolCallSister == _state.CurrentSister)
            {
                _state.PatienceCount++;
            }
            else
            {
                _state.PatienceCount = 1;
            }

            _state.LastToolCallSister = _state.CurrentSister;
        }

        // 怠け癖発動
        if (ShouldBeLazy(completion.Value))
        {
            BeginLazyMode();

            // 再度返信を生成
            completion = await CompleteChatAsync(_state);

            // それでも関数呼び出しされることがあるのでチェック
            if (completion.Value.FinishReason != ChatFinishReason.Stop)
            {
                _state.AddHint(Hint.CancelLazyMode);
            }
            // 実際に怠けた場合の処理
            else
            {
                // フロントに生成テキストを送信
                yield return new ConversationResult
                {
                    Message = TrimSisterName(completion.Value.Content[0].Text),
                    Sister = _state.CurrentSister
                }; ;

                _state.AddAssistantMessage(completion.Value);

                EndLazyMode();

                // 姉妹を切り替えて、再度呼び出し
                _state.CurrentSister = _state.CurrentSister.Switch();
                _state.AddHint(Hint.SwitchSisterTo(_state.CurrentSister));
                completion = await CompleteChatAsync(_state);

                // 怠けると姉妹が入れ替わるのでカウンターをリセット
                _state.PatienceCount = 1;
            }
        }

        _state.AddAssistantMessage(completion.Value);

        // 関数の実行
        List<ConversationFunction> functions;
        (completion, functions) = await InvokeFunctions(completion);

        // フロントに生成テキストを送信
        yield return new ConversationResult
        {
            Message = TrimSisterName(completion.Value.Content[0].Text),
            Sister = _state.CurrentSister,
            Functions = functions
        }; ;

        void BeginLazyMode()
        {
            switch (_state.CurrentSister)
            {
                case Kotonoha.Akane:
                    _state.AddHint(Hint.BeginLazyModeAkane);
                    break;
                case Kotonoha.Aoi:
                    _state.AddHint(Hint.BeginLazyModeAoi);
                    break;
            }
        }

        void EndLazyMode()
        {
            switch (_state.CurrentSister)
            {
                case Kotonoha.Akane:
                    _state.AddHint(Hint.EndLazyModeAkane);
                    break;
                case Kotonoha.Aoi:
                    _state.AddHint(Hint.EndLazyModeAoi);
                    break;
            }
        }

        static string TrimSisterName(string input) => Regex.Replace(input, @"^(茜|葵):\s+", string.Empty);
    }

    /// <summary>
    /// Function callingで呼び出された関数の実行を行います
    /// </summary>
    /// <param name="completion"></param>
    /// <returns></returns>
    private async Task<(ClientResult<ChatCompletion> result, List<ConversationFunction> functions)> InvokeFunctions(ClientResult<ChatCompletion> completion)
    {
        var invokedFunctions = new List<ConversationFunction>();
        while (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
        {
            foreach (var toolCall in completion.Value.ToolCalls)
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
                if (!_functions.TryGetValue(toolCall.FunctionName, out var function) || function is null)
                {
                    _state.AddToolMessage(toolCall.Id, "ERROR");
                    continue;
                }

                if (!function.TryParseArguments(doc, out var arguments))
                {
                    _state.AddToolMessage(toolCall.Id, "ERROR");
                    continue;
                }

                var result = function.Invoke(arguments);
                invokedFunctions.Add(new ConversationFunction
                {
                    Name = toolCall.FunctionName,
                    Arguments = arguments,
                    Result = result
                });

                _state.AddToolMessage(toolCall.Id, result);
            }

            completion = await CompleteChatAsync(_state);
            _state.AddAssistantMessage(completion.Value);
        }

        return (completion, invokedFunctions);
    }

    private Task<ClientResult<ChatCompletion>> CompleteChatAsync(ConversationState state) => _chatClient.CompleteChatAsync(state.ChatMessages, _options);

    private bool ShouldBeLazy(ChatCompletion completionValue)
    {
        // 関数呼び出し以外は怠けない
        if (completionValue.FinishReason != ChatFinishReason.ToolCalls)
        {
            return false;
        }

        // 怠け癖対象外の関数なら怠けない
        if (completionValue.ToolCalls.Any(toolCall => _excludeFunctionNamesFromLazyMode.Contains(toolCall.FunctionName)))
        {
            return false;
        }

        // 4回以上同じ方にお願いすると怠ける
        if (_state.PatienceCount > 3)
        {
            return true;
        }

        // 1/10の確率で怠け癖発動
        var lazy = _r.NextDouble() < 1d / 10d;
        return lazy;
    }
}

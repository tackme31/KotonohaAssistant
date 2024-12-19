using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Utils;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace KotonohaAssistant.AI.Services;

public class ConversationService : IDisposable
{
    private readonly ChatMessageManager _messageManager;
    private readonly ChatClient _chatClient;
    private readonly VoiceClient _voiceClient;
    private readonly IDictionary<string, ToolFunction> _functions;
    private readonly IList<string> _excludeFunctionNamesFromLazyMode;
    private readonly ChatCompletionOptions _options;
    private readonly Random _r = new();

    /// <summary>
    /// 同じ方に連続してお願いした回数。忍耐値。
    /// </summary>
    private int _patienceCount;

    /// <summary>
    /// 最後にお願いを聞いてくれた方を格納。
    /// </summary>
    private Kotonoha _lastToolCallSister;

    public ConversationService(
        string chatApiKey,
        string modelName,
        IList<ToolFunction> availableFunctions,
        IList<string> excludeFunctionNamesFromLazyMode,
        Kotonoha defaultSister = Kotonoha.Akane,
        string? akaneBehaviour = null,
        string? aoiBehaviour = null)
    {
        _messageManager = new ChatMessageManager(defaultSister, akaneBehaviour, aoiBehaviour);
        _messageManager.AddAssistantMessage("葵: はじめまして、マスター。私は琴葉葵。こっちは姉の茜。");
        _messageManager.AddAssistantMessage("茜: 今日からうちらがマスターのことサポートするで。");
        _messageManager.AddUserMessage("私: うん。よろしくね。");
        _messageManager.AddUserMessage("======= LazyMode: OFF =======");

        _chatClient = new ChatClient(modelName, chatApiKey);
        _voiceClient = new VoiceClient();
        _functions = availableFunctions.ToDictionary(f => f.GetType().Name);
        _excludeFunctionNamesFromLazyMode = excludeFunctionNamesFromLazyMode;
        _options = new ChatCompletionOptions();
        foreach (var function in availableFunctions)
        {
            _options.Tools.Add(function.CreateChatTool());
        }

        _patienceCount = 0;
        _lastToolCallSister = 0;
    }

    public async IAsyncEnumerable<ConversationResult> TalkingWithKotonohaSisters(string input)
    {
        using var voiceClient = new VoiceClient();

        if (string.IsNullOrWhiteSpace(input))
        {
            yield break;
        }

        // 姉妹切り替え
        if (_messageManager.CurrentSister == Kotonoha.Aoi &&
            (input.Contains("茜") || input.Contains("あかね")))
        {
            _messageManager.CurrentSister = Kotonoha.Akane;
            _messageManager.AddUserMessage($"[Hint]: 姉妹が切り替わりました(葵 => 茜)");
        }
        if (_messageManager.CurrentSister == Kotonoha.Akane &&
            (input.Contains("葵") || input.Contains("あおい")))
        {
            _messageManager.CurrentSister = Kotonoha.Aoi;
            _messageManager.AddUserMessage($"[Hint]: 姉妹が切り替わりました(茜 => 葵)");
        }

        _messageManager.AddUserMessage($"私: {input}");

        var completion = await CompleteChatAsync(_messageManager.ChatMessages);

        // 忍耐値カウント
        if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
        {
            if (_lastToolCallSister == _messageManager.CurrentSister)
            {
                _patienceCount++;
            }
            else
            {
                _patienceCount = 1;
            }

            _lastToolCallSister = _messageManager.CurrentSister;
        }

        // TODO: 怠け癖発動し、その後に関数の呼び出しが入らなかった場合
        // 2回読み上げられる

        // 怠け癖発動
        if (ShouldBeLazy(completion.Value))
        {
            // 怠け者モードをONにして、再度呼び出し。
            _messageManager.AddUserMessage($"""
[Hint]: LazyModeがONになりました

- 以降、関数を呼び出さないでください
- 琴葉 {_messageManager.CurrentSister.ToDisplayName()}に指示を押し付けてください
""");
            completion = await CompleteChatAsync(_messageManager.ChatMessages);

            // それでも関数呼び出しされることがあるのでチェック
            if (completion.Value.FinishReason != ChatFinishReason.Stop)
            {
                _messageManager.AddUserMessage("""
[Hint]: LazyModeがOFFになりました。

- 以降、通常通り**関数を呼び出してください**
""");
            }
            // 実際に怠けた場合の処理
            else
            {
                // フロントに生成テキストを送信
                yield return new ConversationResult
                {
                    Message = TrimSisterName(completion.Value.Content[0].Text),
                    Sister = _messageManager.CurrentSister
                };

                _messageManager.AddAssistantMessage(completion.Value);

                // 押し付けセリフを読み上げ
                await SpeakCompletionAsync(completion);

                // 怠け者モードをOFF
                switch (_messageManager.CurrentSister)
                {
                    case Kotonoha.Akane:
                        _messageManager.AddUserMessage($"""
[Hint]: LazyModeがOFFになりました。

- 以降、通常通り関数を呼び出してください
- また、姉の茜からタスクを押し付けられました。**関数を呼び出した上で**、返事の先頭にタスクを引き受けたことがわかるセリフを追加してください。
    - 例:「もう、仕方ないなあ。～」「任せて。～」など
""");
                        break;
                    case Kotonoha.Aoi:
                        _messageManager.AddUserMessage($"""
[Hint]: LazyModeがOFFになりました。

- 以降、通常通り関数を呼び出してください
- また、妹の葵からタスクを押し付けられました。**関数を呼び出した上で**、返事の先頭にタスクを引き受けたことがわかるセリフを追加してください。
    - 例:「もう、しゃあないなあ。～」「任せとき。～」など
""");
                        break;
                }


                // 姉妹を切り替えて、再度呼び出し
                var prev = _messageManager.CurrentSister.ToDisplayName();
                var next = _messageManager.CurrentSister.Switch().ToDisplayName();
                _messageManager.CurrentSister = _messageManager.CurrentSister.Switch();

                _messageManager.AddUserMessage($"[Hint]: 姉妹が切り替わりました({prev} => {next})");

                completion = await CompleteChatAsync(_messageManager.ChatMessages);

                // 怠けると姉妹が入れ替わるのでカウンターをリセット
                _patienceCount = 1;
            }
        }

        _messageManager.AddAssistantMessage(completion.Value);

        List<ConversationFunction> functions;
        (completion, functions) = await InvokeFunctions(completion);

        // フロントに生成テキストを送信
        yield return new ConversationResult
        {
            Message = TrimSisterName(completion.Value.Content[0].Text),
            Sister = _messageManager.CurrentSister,
            Functions = functions
        };

        // 読み上げ
        await SpeakCompletionAsync(completion);

        static string TrimSisterName(string input) => Regex.Replace(input, @"^(茜|葵):\s+", string.Empty);
    }

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
                    _messageManager.AddToolMessage(toolCall.Id, "ERROR");
                    continue;
                }

                if (!function.TryParseArguments(doc, out var arguments))
                {
                    _messageManager.AddToolMessage(toolCall.Id, "ERROR");
                    continue;
                }

                var result = function.Invoke(arguments);
                invokedFunctions.Add(new ConversationFunction
                {
                    Name = toolCall.FunctionName,
                    Arguments = arguments,
                    Result = result
                });

                _messageManager.AddToolMessage(toolCall.Id, result);
            }

            completion = await CompleteChatAsync(_messageManager.ChatMessages);
            _messageManager.AddAssistantMessage(completion.Value);
        }

        return (completion, invokedFunctions);
    }

    private Task<ClientResult<ChatCompletion>> CompleteChatAsync(IEnumerable<ChatMessage> messages) => _chatClient.CompleteChatAsync(messages, _options);

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
        if (_patienceCount > 2)
        {
            return true;
        }

        // 1/10の確率で怠け癖発動
        var lazy = _r.NextDouble() < 1d / 10d;
        return lazy;
    }

    private async Task SpeakCompletionAsync(ClientResult<ChatCompletion> completion)
    {
        if (completion.Value.FinishReason != ChatFinishReason.Stop)
        {
            return;
        }
        var message = completion.Value.Content[0].Text;

        var messageWithoutName = Regex.Replace(message, @"^(茜|葵):", string.Empty);
        await _voiceClient.SpeakAsync(_messageManager.CurrentSister, messageWithoutName);
    }

    public void Dispose()
    {
        _voiceClient?.Dispose();
    }
}

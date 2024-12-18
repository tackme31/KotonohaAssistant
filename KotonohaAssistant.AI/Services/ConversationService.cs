using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Utils;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;

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

        // 怠け癖発動
        if (ShouldBeLazy(completion.Value))
        {
            await foreach (var c in PassTaskToAnotherSisterAsync())
            {
                if (c.Value.FinishReason == ChatFinishReason.Stop)
                {
                    // フロントに生成テキストを送信
                    yield return new ConversationResult
                    {
                        Message = TrimSisterName(c.Value.Content[0].Text),
                        Sister = _messageManager.CurrentSister
                    };
                }

                _messageManager.AddAssistantMessage(c.Value);

                // 読み上げ
                await SpeakCompletionAsync(c);

                completion = c;
            }

            // 怠けると姉妹が入れ替わるのでカウンターをリセット
            _patienceCount = 1;
        }
        else
        {
            _messageManager.AddAssistantMessage(completion.Value);
        }

        var functions = new List<ConversationFunction>();
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
                functions.Add(new ConversationFunction
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

        // フロントに生成テキストを送信
        yield return new ConversationResult
        {
            Message = TrimSisterName(completion.Value.Content[0].Text),
            Sister = _messageManager.CurrentSister,
            Functions = functions
        };

        // 読み上げ
        await SpeakCompletionAsync(completion);

        string TrimSisterName(string input) => Regex.Replace(input, @"^(茜|葵):\s+", string.Empty);
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
        if (_patienceCount > 3)
        {
            return true;
        }

        // 1/10の確率で怠け癖発動
        var lazy = _r.NextDouble() < 1d / 10d;
        return lazy;
    }

    private async IAsyncEnumerable<ClientResult<ChatCompletion>> PassTaskToAnotherSisterAsync()
    {
        // 怠け者モードをONにして、再度呼び出し。
        _messageManager.AddUserMessage("[Hint]: LazyMode=ON: 以降、関数を呼び出さないでください。");
        var completion = await CompleteChatAsync(_messageManager.ChatMessages);

        // それでも関数呼び出しされることがあるのでチェック
        if (completion.Value.FinishReason != ChatFinishReason.Stop)
        {
            // 怠け者モードをOFF
            _messageManager.AddUserMessage("[Hint]: LazyMode=OFF: 以降、通常通り関数を呼び出してください。");

            yield return completion;
            yield break;
        }


        yield return completion;
        _messageManager.AddAssistantMessage(completion.Value);

        // 怠け者モードをOFF
        _messageManager.AddUserMessage("[Hint]: LazyMode=OFF: 以降、通常通り関数を呼び出してください。");

        // 姉妹を切り替えて、再度呼び出し
        var nextSister = _messageManager.CurrentSister switch
        {
            Kotonoha.Akane => Kotonoha.Aoi,
            Kotonoha.Aoi => Kotonoha.Akane,
            _ => _messageManager.CurrentSister
        };

        var prev = _messageManager.CurrentSister switch
        {
            Kotonoha.Akane => "茜",
            Kotonoha.Aoi => "葵",
            _ => string.Empty
        };
        var next = nextSister switch
        {
            Kotonoha.Akane => "茜",
            Kotonoha.Aoi => "葵",
            _ => string.Empty
        };
        _messageManager.AddUserMessage($"[Hint]: 姉妹が切り替わりました({prev} => {next})");

        _messageManager.CurrentSister = nextSister;
        yield return await CompleteChatAsync(_messageManager.ChatMessages);
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

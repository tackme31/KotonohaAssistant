using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
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
    private readonly IDictionary<string, ToolFunction> _functions;
    private readonly Random _r = new();

    private readonly ChatCompletionOptions _options;

    /// <summary>
    /// 最後に保存したメッセージ
    /// </summary>
    private ChatMessage? _lastSavedMessage;

    private IChatMessageRepositoriy _chatMessageRepositoriy;
    private IChatCompletionRepository _chatCompletionRepository;

    private long? _currentConversationId = null;

    public IReadOnlyConversationState State => _state;

    public ConversationService(
        IChatMessageRepositoriy chatMessageRepositoriy,
        IChatCompletionRepository chatCompletionRepository,
        IList<ToolFunction> availableFunctions,
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

        _options = new ChatCompletionOptions
        {
            AllowParallelToolCalls = true,
        };
        foreach (var func in availableFunctions)
        {
            _options.Tools.Add(func.CreateChatTool());
        }

        _functions = availableFunctions.ToDictionary(f => f.GetType().Name);
        _state.PatienceCount = 0;
        _state.LastToolCallSister = 0;

        _chatMessageRepositoriy = chatMessageRepositoriy;
        _chatCompletionRepository = chatCompletionRepository;
    }

    public IEnumerable<string> GetAllMessageTexts()
    {
        foreach (var message in _state.ChatMessages)
        {
            if (message is UserChatMessage user &&
                user.Content.Any() &&
                user.Content[0].Text.StartsWith("私:"))
            {
                yield return user.Content[0].Text;
            }

            if (message is AssistantChatMessage assistant &&
                assistant.Content.Any())
            {
                yield return assistant.Content[0].Text;
            }
        }
    }

    /// <summary>
    /// 新しい会話を開始します
    /// </summary>
    /// <returns></returns>
    private async Task<long> CreateNewConversationAsync()
    {
        // 生成時の参考のためにあらかじめ会話を入れておく
        _state.ClearChatMessages();
        _state.AddAssistantMessage("葵: はじめまして、マスター。私は琴葉葵。こっちは姉の茜。");
        _state.AddAssistantMessage("茜: 今日からうちらがマスターのことサポートするで。");
        _state.AddAssistantMessage("葵: これから一緒に過ごすことになるけど、気軽に声をかけてね。");
        _state.AddAssistantMessage("茜: せやな！これからいっぱい思い出作っていこな。");
        _state.AddUserMessage("私: うん。よろしくね。");

        _lastSavedMessage = null;

        return await _chatMessageRepositoriy.CreateNewConversationIdAsync();
    }

    /// <summary>
    /// 直近の会話を読み込みます
    /// </summary>
    /// <returns></returns>
    public async Task LoadLatestConversation()
    {
        var conversationId = await _chatMessageRepositoriy.GetLatestConversationIdAsync();
        // 会話履歴が存在しない場合
        if (conversationId < 0)
        {
            _currentConversationId = await CreateNewConversationAsync();
            return;
        }

        var messages = await _chatMessageRepositoriy.GetAllChatMessagesAsync(conversationId);

        _currentConversationId = conversationId;
        _state.LoadMessages(messages);
        _lastSavedMessage = messages.LastOrDefault();

        if (_lastSavedMessage is null)
        {
            return;
        }

        var lastText = _lastSavedMessage.Content.FirstOrDefault()?.Text ?? "";
        if (lastText.StartsWith("茜"))
        {
            _state.CurrentSister = Kotonoha.Akane;
        }
        if (lastText.StartsWith("葵"))
        {
            _state.CurrentSister = Kotonoha.Aoi;
        }
    }

    private async Task SaveState()
    {
        if (_currentConversationId is null)
        {
            return;
        }

        var unsavedMessages = _lastSavedMessage is null
            ? _state.ChatMessages
            : _state.ChatMessages.SkipWhile(message => message != _lastSavedMessage).Skip(1);

        await _chatMessageRepositoriy.InsertChatMessagesAsync(unsavedMessages, _currentConversationId.Value);

        _lastSavedMessage = _state.ChatMessages.Last();
    }

    /// <summary>
    /// 入力したテキストで琴葉姉妹と会話します
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<ConversationResult> TalkWithKotonohaSisters(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            yield break;
        }

        if (_currentConversationId is null)
        {
            _currentConversationId = await CreateNewConversationAsync();
        }

        // 姉妹切り替え
        var nextSister = GuessTargetSister(input);
        switch (nextSister)
        {
            case Kotonoha.Akane when _state.CurrentSister == Kotonoha.Aoi:
                _state.CurrentSister = Kotonoha.Akane;
                _state.AddHint(Hint.SwitchSisterTo(Kotonoha.Akane));
                break;
            case Kotonoha.Aoi when _state.CurrentSister == Kotonoha.Akane:
                _state.CurrentSister = Kotonoha.Aoi;
                _state.AddHint(Hint.SwitchSisterTo(Kotonoha.Aoi));
                break;
            default:
                break;
        }

        // 返信を生成
        _state.AddUserMessage($"私: {input}");
        var completion = await _chatCompletionRepository.CompleteChatAsync(_state.ChatMessagesWithSystemMessage, _options);

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
            completion = await _chatCompletionRepository.CompleteChatAsync(_state.ChatMessagesWithSystemMessage, _options);

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
                completion = await _chatCompletionRepository.CompleteChatAsync(_state.ChatMessagesWithSystemMessage, _options);

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

        // 記憶削除時は新しい会話にする
        if (functions.Any(f => f.Name == nameof(ForgetMemory) && f.Result == ForgetMemory.SuccessMessage))
        {
            _currentConversationId = await CreateNewConversationAsync();
        }

        await SaveState();

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
    }

    private static string TrimSisterName(string input) => Regex.Replace(input, @"^(茜|葵):\s+", string.Empty);

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
                    _state.AddToolMessage(toolCall.Id, $"Function '{toolCall.FunctionName} does not exist.'");
                    continue;
                }

                if (!function.TryParseArguments(doc, out var arguments))
                {
                    _state.AddToolMessage(toolCall.Id, $"Failed to parse arguments of '{toolCall.FunctionName}'.");
                    continue;
                }

                var result = await function.Invoke(arguments, _state);
                invokedFunctions.Add(new ConversationFunction
                {
                    Name = toolCall.FunctionName,
                    Arguments = arguments,
                    Result = result
                });

                _state.AddToolMessage(toolCall.Id, result);
            }

            completion = await _chatCompletionRepository.CompleteChatAsync(_state.ChatMessagesWithSystemMessage, _options);
            _state.AddAssistantMessage(completion.Value);
        }

        return (completion, invokedFunctions);
    }

    /// <summary>
    /// 会話対象の姉妹を取得します。
    /// 両方含まれていた場合、最初にヒットした方を返します。
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private Kotonoha? GuessTargetSister(string input)
    {
        var namePairs = new (string search, Kotonoha? sister)[]
        {
            ("茜ちゃん", Kotonoha.Akane),
            ("あかねちゃん", Kotonoha.Akane),
            ("葵ちゃん", Kotonoha.Aoi),
            ("あおいちゃん", Kotonoha.Aoi)
        };

        return namePairs
            .Select(name => (name.sister, index: input.IndexOf(name.search)))
            .Where(r => r.index >= 0)
            .OrderBy(r => r.index)
            .Select(r => r.sister)
            .FirstOrDefault();
    }

    private bool ShouldBeLazy(ChatCompletion completionValue)
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

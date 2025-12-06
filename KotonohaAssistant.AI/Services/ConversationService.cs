using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;
using System.Text.Json;

namespace KotonohaAssistant.AI.Services;

public class ConversationService
{
    private readonly ConversationState _state;
    private readonly Dictionary<string, ToolFunction> _functions;
    private readonly ChatCompletionOptions _options;

    /// <summary>
    /// 最後に保存したメッセージ
    /// </summary>
    private ChatMessage? _lastSavedMessage;
    private long? _currentConversationId = null;

    private readonly IChatMessageRepository _chatMessageRepositoriy;
    private readonly IChatCompletionRepository _chatCompletionRepository;
    private readonly ISisterSwitchingService _sisterSwitchingService;
    private readonly ILazyModeHandler _lazyModeHandler;
    private readonly ILogger _logger;

    public ConversationService(
        IPromptRepository promptRepository,
        IChatMessageRepository chatMessageRepository,
        IChatCompletionRepository chatCompletionRepository,
        IList<ToolFunction> availableFunctions,
        ISisterSwitchingService sisterSwitchingService,
        ILazyModeHandler lazyModeHandler,
        ILogger logger,
        Kotonoha defaultSister = Kotonoha.Akane)
    {
        _state = new ConversationState()
        {
            CurrentSister = defaultSister,
            CharacterPromptAkane = promptRepository.GetCharacterPrompt(Kotonoha.Akane),
            CharacterPromptAoi = promptRepository.GetCharacterPrompt(Kotonoha.Aoi)
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

        _chatMessageRepositoriy = chatMessageRepository;
        _chatCompletionRepository = chatCompletionRepository;
        _sisterSwitchingService = sisterSwitchingService;
        _lazyModeHandler = lazyModeHandler;
        _logger = logger;
    }

    public IEnumerable<(Kotonoha? sister, string message)> GetAllMessages()
    {
        foreach (var message in _state.ChatMessages.Skip(InitialConversation.Count)) // CreateNewConversationAsyncで追加した生成参考用の会話をスキップ
        {
            if (!message.Content.Any())
            {
                continue;
            }

            var content = message.Content[0].Text;
            switch (message)
            {
                case AssistantChatMessage when ChatResponse.TryParse(content, out var response):
                    yield return (response?.Assistant, response?.Text ?? string.Empty);
                    continue;
                case UserChatMessage when ChatRequest.TryParse(content, out var request) && request?.InputType == ChatInputType.User:
                    yield return (null, request?.Text ?? string.Empty);
                    continue;
                case ToolChatMessage:
                    continue;
            }
        }
    }

    /// <summary>
    /// 新しい会話を開始します
    /// </summary>
    /// <returns></returns>
    private async Task<long> CreateNewConversationAsync()
    {
        long conversationId = -1;
        try
        {
            conversationId = await _chatMessageRepositoriy.CreateNewConversationIdAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            return -1;
        }

        _state.ClearChatMessages();

        // 生成時の参考のためにあらかじめ会話を入れておく
        _state.LoadInitialConversation();

        _lastSavedMessage = null;

        return conversationId;
    }

    /// <summary>
    /// 直近の会話を読み込みます
    /// </summary>
    /// <returns></returns>
    public async Task LoadLatestConversation()
    {
        long conversationId = -1;
        try
        {
            conversationId = await _chatMessageRepositoriy.GetLatestConversationIdAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }

        // 会話履歴が存在しない場合
        if (conversationId < 0)
        {
            _currentConversationId = await CreateNewConversationAsync();
            return;
        }

        IEnumerable<ChatMessage>? messages;
        try
        {
            messages = await _chatMessageRepositoriy.GetAllChatMessagesAsync(conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            return;
        }

        _currentConversationId = conversationId;
        _state.LoadMessages(messages);
        _lastSavedMessage = messages.LastOrDefault();

        if (_lastSavedMessage is null)
        {
            return;
        }

        var lastText = _lastSavedMessage.Content.FirstOrDefault()?.Text ?? "";
        if (ChatResponse.TryParse(lastText, out var response) && response is not null)
        {
            _state.CurrentSister = response.Assistant;
        }
        else
        {
            _state.CurrentSister = Kotonoha.Akane;
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

        try
        {
            await _chatMessageRepositoriy.InsertChatMessagesAsync(unsavedMessages, _currentConversationId.Value);
            _lastSavedMessage = _state.ChatMessages.Last();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
        }
    }

    private async Task<ChatCompletion?> CompleteChatAsync(IEnumerable<ChatMessage> messages)
    {
        try
        {
            var trimmed = messages.TakeLast(300); // コンテキストウィンドウ対策
            return await _chatCompletionRepository.CompleteChatAsync(messages, _options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);

            return null;
        }
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

        await EnsureConversationExistsAsync();

        // 姉妹切り替え
        _sisterSwitchingService.TrySwitchSister(input, _state);

        // 返信を生成
        _state.AddUserMessage(input);
        var completion = await CompleteChatAsync(_state.ChatMessagesWithSystemMessage);
        if (completion is null)
        {
            yield break;
        }

        // 忍耐値の処理
        UpdatePatienceCounter(completion);

        // 怠け癖モード処理
        var lazyResult = await _lazyModeHandler.HandleLazyModeAsync(
            completion,
            _state,
            () => CompleteChatAsync(_state.ChatMessagesWithSystemMessage));

        // 怠け癖時のタスク押し付け応答を返す
        if (lazyResult.LazyResponse is not null)
        {
            yield return lazyResult.LazyResponse;
        }

        // 最終的な完了結果を使用
        completion = lazyResult.FinalCompletion;
        if (completion is null)
        {
            yield break;
        }
        _state.AddAssistantMessage(completion);

        // 関数の実行
        List<ConversationFunction> functions;
        (completion, functions) = await InvokeFunctions(completion);

        // フロントに生成テキストを送信
        {
            if (ChatResponse.TryParse(completion.Content[0].Text, out var response))
            {
                yield return new ConversationResult
                {
                    Message = response?.Text ?? string.Empty,
                    Emotion = response?.Emotion ?? Emotion.Calm,
                    Sister = _state.CurrentSister,
                    Functions = functions
                };
            }
        }

        // 記憶削除時は新しい会話にする
        await HandleMemoryDeletionAsync(functions);

        await SaveState();
    }

    /// <summary>
    /// 会話が存在しない場合は新規作成します
    /// </summary>
    private async Task EnsureConversationExistsAsync()
    {
        if (_currentConversationId is null)
        {
            _currentConversationId = await CreateNewConversationAsync();
        }
    }

    /// <summary>
    /// 忍耐値を更新します
    /// </summary>
    private void UpdatePatienceCounter(ChatCompletion completion)
    {
        if (completion.FinishReason == ChatFinishReason.ToolCalls)
        {
            _state.RecordToolCall(_state.CurrentSister);
        }
    }

    /// <summary>
    /// 記憶削除時は新しい会話を作成します
    /// </summary>
    private async Task HandleMemoryDeletionAsync(List<ConversationFunction> functions)
    {
        if (functions.Any(f => f.Name == nameof(ForgetMemory) && f.Result == ForgetMemory.SuccessMessage))
        {
            _currentConversationId = await CreateNewConversationAsync();
        }
    }

    /// <summary>
    /// Function callingで呼び出された関数の実行を行います
    /// </summary>
    /// <param name="completion"></param>
    /// <returns></returns>
    private async Task<(ChatCompletion result, List<ConversationFunction> functions)> InvokeFunctions(ChatCompletion completion)
    {
        var invokedFunctions = new List<ConversationFunction>();
        while (completion.FinishReason == ChatFinishReason.ToolCalls)
        {
            foreach (var toolCall in completion.ToolCalls)
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

            var nextCompletion = await CompleteChatAsync(_state.ChatMessagesWithSystemMessage);
            if (nextCompletion is null)
            {
                continue;
            }

            completion = nextCompletion;
            _state.AddAssistantMessage(completion);
        }

        return (completion, invokedFunctions);
    }
}
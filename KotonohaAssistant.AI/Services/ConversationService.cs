using System.Text.Json;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;
using KotonohaAssistant.Core.Utils;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Services;

public class ConversationService
{
    private const string LogPrefix = "[Conversation]";

    private readonly ConversationState _state;
    private readonly Dictionary<string, ToolFunction> _functions;
    private readonly ChatCompletionOptions _options;

    /// <summary>
    /// 会話の状態（読み取り専用）
    /// </summary>
    public IReadOnlyConversationState State => _state;

    /// <summary>
    /// 最後に保存したメッセージのインデックス（次に保存すべき位置）
    /// </summary>
    private int _lastSavedMessageIndex = 0;
    private long? _currentConversationId = null;

    private readonly IChatMessageRepository _chatMessageRepositoriy;
    private readonly IChatCompletionRepository _chatCompletionRepository;
    private readonly ILazyModeHandler _lazyModeHandler;
    private readonly ILogger _logger;

    public ConversationService(
        IPromptRepository promptRepository,
        IChatMessageRepository chatMessageRepository,
        IChatCompletionRepository chatCompletionRepository,
        IList<ToolFunction> availableFunctions,
        ILazyModeHandler lazyModeHandler,
        ILogger logger,
        Kotonoha defaultSister = Kotonoha.Akane)
        : this(
            new ConversationState()
            {
                CurrentSister = defaultSister,
                CharacterPromptAkane = promptRepository.GetCharacterPrompt(Kotonoha.Akane),
                CharacterPromptAoi = promptRepository.GetCharacterPrompt(Kotonoha.Aoi)
            },
            chatMessageRepository,
            chatCompletionRepository,
            availableFunctions,
            lazyModeHandler,
            logger)
    {
    }

    /// <summary>
    /// テスト用のコンストラクタ（ConversationStateを外部から注入可能）
    /// </summary>
    internal ConversationService(
        ConversationState state,
        IChatMessageRepository chatMessageRepository,
        IChatCompletionRepository chatCompletionRepository,
        IList<ToolFunction> availableFunctions,
        ILazyModeHandler lazyModeHandler,
        ILogger logger)
    {
        _state = state;

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
        _logger.LogInformation($"{LogPrefix} Creating new conversation...");

        long conversationId = -1;
        try
        {
            conversationId = await _chatMessageRepositoriy.CreateNewConversationIdAsync();
            _logger.LogInformation($"{LogPrefix} New conversation created: ID={conversationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            return -1;
        }

        _state.ClearChatMessages();

        // 生成時の参考のためにあらかじめ会話を入れておく
        _state.LoadInitialConversation();

        _lastSavedMessageIndex = _state.ChatMessages.Count();

        return conversationId;
    }

    /// <summary>
    /// 直近の会話を読み込みます
    /// </summary>
    /// <returns></returns>
    public async Task LoadLatestConversation()
    {
        _logger.LogInformation($"{LogPrefix} Loading latest conversation...");

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
            _logger.LogInformation($"{LogPrefix} No existing conversation found.");
            _currentConversationId = await CreateNewConversationAsync();
            return;
        }

        IEnumerable<ChatMessage>? messages;
        try
        {
            messages = await _chatMessageRepositoriy.GetAllChatMessagesAsync(conversationId);
            _logger.LogInformation($"{LogPrefix} Loaded conversation: ID={conversationId}, MessageCount={messages.Count()}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            return;
        }

        _currentConversationId = conversationId;
        _state.LoadMessages(messages);
        _lastSavedMessageIndex = _state.ChatMessages.Count();

        var lastSavedMessage = messages.LastOrDefault();
        if (lastSavedMessage is null)
        {
            return;
        }

        var lastText = lastSavedMessage.Content.FirstOrDefault()?.Text ?? "";
        if (ChatResponse.TryParse(lastText, out var response) && response is not null)
        {
            _state.CurrentSister = response.Assistant;
            _logger.LogInformation($"{LogPrefix} Current sister set to: {response.Assistant}");
        }
        else
        {
            _state.CurrentSister = Kotonoha.Akane;
            _logger.LogInformation($"{LogPrefix} Current sister set to default: Akane");
        }
    }

    private async Task SaveState()
    {
        if (_currentConversationId is null)
        {
            return;
        }

        // インデックスベースで未保存メッセージを取得
        var unsavedMessages = _state.ChatMessages
            .Skip(_lastSavedMessageIndex)
            .ToList();

        if (unsavedMessages.Count == 0)
        {
            return;
        }

        _logger.LogInformation($"{LogPrefix} Saving state: ConversationID={_currentConversationId}, UnsavedMessageCount={unsavedMessages.Count}");

        try
        {
            await _chatMessageRepositoriy.InsertChatMessagesAsync(unsavedMessages, _currentConversationId.Value);

            // インデックスを更新
            _lastSavedMessageIndex = _state.ChatMessages.Count();

            _logger.LogInformation($"{LogPrefix} State saved successfully.");
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
            // ToolCallを要求されていない状態でTooLChatMessageを送信すると400エラーになるのでスキップ
            var recentMessages = messages.TakeLast(20).SkipWhile(m => m is ToolChatMessage).ToList();
            return await _chatCompletionRepository.CompleteChatAsync(recentMessages, _options);
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

        _logger.LogInformation($"{LogPrefix} Starting conversation with input: '{input}'");

        await EnsureConversationExistsAsync();

        // 姉妹切り替え
        TrySwitchSister(input);

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
            // 怠けると姉妹が入れ替わるのでカウンターをリセット
            _state.ResetPatienceCount();

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
            else
            {
                _logger.LogError($"生成結果のパースに失敗しました: {completion.Content[0].Text}");
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
            _logger.LogInformation($"{LogPrefix} Memory deletion detected. Creating new conversation...");
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
            _logger.LogInformation($"{LogPrefix} Invoking {completion.ToolCalls.Count} function(s)...");

            foreach (var toolCall in completion.ToolCalls)
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
                if (!_functions.TryGetValue(toolCall.FunctionName, out var function) || function is null)
                {
                    _logger.LogWarning($"{LogPrefix} Function '{toolCall.FunctionName}' does not exist.");
                    _state.AddToolMessage(toolCall.Id, $"Function '{toolCall.FunctionName} does not exist.'");
                    continue;
                }

                if (!function.TryParseArguments(doc, out var arguments))
                {
                    _logger.LogWarning($"{LogPrefix} Failed to parse arguments of '{toolCall.FunctionName}'.");
                    _state.AddToolMessage(toolCall.Id, $"Failed to parse arguments of '{toolCall.FunctionName}'.");
                    continue;
                }

                _logger.LogInformation($"{LogPrefix} Executing function: {toolCall.FunctionName}");
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

    /// <summary>
    /// ユーザー入力を解析し、必要に応じて姉妹を切り替えます
    /// </summary>
    /// <param name="userInput">ユーザーの入力テキスト</param>
    /// <returns>姉妹が切り替わった場合はtrue</returns>
    public bool TrySwitchSister(string userInput)
    {
        var nextSister = GuessTargetSister(userInput);
        if (nextSister == null || nextSister == _state.CurrentSister)
        {
            return false;
        }

        _logger.LogInformation($"{LogPrefix} Sister switch detected: {_state.CurrentSister} -> {nextSister.Value}");
        _state.SwitchToSister(nextSister.Value);
        return true;
    }

    /// <summary>
    /// 会話対象の姉妹を取得します。
    /// 両方含まれていた場合、最初にヒットした方を返します。
    /// </summary>
    /// <param name="input">ユーザーの入力テキスト</param>
    /// <returns>検出された姉妹、または検出されなかった場合はnull</returns>
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
}

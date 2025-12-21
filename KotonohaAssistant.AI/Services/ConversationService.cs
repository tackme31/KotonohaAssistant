<<<<<<< HEAD
﻿using System.Runtime.Serialization;
using System.Text.Json;
=======
﻿using System.Text.Json;
using System.Text.Json.Nodes;
>>>>>>> 45b5b57 (ToolFunctionのプロパティ定義にJsonSchemaExporterを使用)
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

    private readonly Dictionary<string, ToolFunction> _functions;
    private readonly ChatCompletionOptions _options;

    private readonly IChatMessageRepository _chatMessageRepositoriy;
    private readonly IChatCompletionRepository _chatCompletionRepository;
    private readonly IPromptRepository _promptRepository;
    private readonly ILazyModeHandler _lazyModeHandler;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger _logger;

    private ConversationState DefaultState => new ConversationState
    {
        SystemMessageAkane = _promptRepository.GetSystemMessage(Kotonoha.Akane),
        SystemMessageAoi = _promptRepository.GetSystemMessage(Kotonoha.Aoi),
        CurrentSister = Kotonoha.Akane,
        LastToolCallSister = Kotonoha.Akane,
        ConversationId = null,
        LastSavedMessageIndex = 0,
        PatienceCount = 0,
    };

    public ConversationService(
        IPromptRepository promptRepository,
        IChatMessageRepository chatMessageRepository,
        IChatCompletionRepository chatCompletionRepository,
        IList<ToolFunction> availableFunctions,
        ILazyModeHandler lazyModeHandler,
        IDateTimeProvider dateTimeProvider,
        ILogger logger)
    {
        _options = new ChatCompletionOptions
        {
            AllowParallelToolCalls = true,
        };
        foreach (var func in availableFunctions)
        {
            _options.Tools.Add(func.CreateChatTool());
        }

        _functions = availableFunctions.ToDictionary(f => f.GetType().Name);

        _chatMessageRepositoriy = chatMessageRepository;
        _chatCompletionRepository = chatCompletionRepository;
        _promptRepository = promptRepository;
        _lazyModeHandler = lazyModeHandler;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public IEnumerable<(Kotonoha? sister, string message)> GetAllMessages(ConversationState state)
    {
        foreach (var message in state.ChatMessages.Skip(InitialConversation.Count)) // CreateNewConversationAsyncで追加した生成参考用の会話をスキップ
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
    private async Task<ConversationState> CreateNewConversationAsync()
    {
        _logger.LogInformation($"{LogPrefix} Creating new conversation...");

        long? newConversationId = null;
        var state = DefaultState;
        try
        {
            newConversationId = await _chatMessageRepositoriy.CreateNewConversationIdAsync();
            _logger.LogInformation($"{LogPrefix} New conversation created: ID={newConversationId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            return state with
            {
                ConversationId = null
            };
        }

        // 生成時の参考のためにあらかじめ会話を入れておく
        state = state.LoadInitialConversation(_dateTimeProvider.Now);

        return state with
        {
            ConversationId = newConversationId,
            LastSavedMessageIndex = 0,
        };
    }

    /// <summary>
    /// 直近の会話を読み込みます
    /// </summary>
    /// <returns></returns>
    public async Task<ConversationState> LoadLatestConversation()
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
        var state = DefaultState;
        if (conversationId < 0)
        {
            _logger.LogInformation($"{LogPrefix} No existing conversation found.");
            state = await CreateNewConversationAsync();
            return state;
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
            return state;
        }

        state = state with
        {
            ConversationId = conversationId,
            LastSavedMessageIndex = messages.Count()
        };

        var lastSavedMessage = messages.LastOrDefault();
        if (lastSavedMessage is null)
        {
            return state;
        }

        var lastText = lastSavedMessage.Content.FirstOrDefault()?.Text ?? "";
        if (ChatResponse.TryParse(lastText, out var response) && response is not null)
        {
            _logger.LogInformation($"{LogPrefix} Current sister set to: {response.Assistant}");

            return state with
            {
                CurrentSister = response.Assistant,
                ChatMessages = [.. messages],
            };
        }
        else
        {
            _logger.LogInformation($"{LogPrefix} Current sister set to default: Akane");

            return state with
            {
                CurrentSister = Kotonoha.Akane,
                ChatMessages = [.. messages],
            };
        }
    }

    private async Task<ConversationState> SaveState(ConversationState state)
    {
        if (state.ConversationId is null)
        {
            return state;
        }

        // インデックスベースで未保存メッセージを取得
        var unsavedMessages = state.ChatMessages
            .Skip(state.LastSavedMessageIndex)
            .ToList();

        if (unsavedMessages.Count == 0)
        {
            return state;
        }

        _logger.LogInformation($"{LogPrefix} Saving state: ConversationID={state.ConversationId}, UnsavedMessageCount={unsavedMessages.Count}");

        try
        {
            await _chatMessageRepositoriy.InsertChatMessagesAsync(unsavedMessages, state.ConversationId.Value);

            _logger.LogInformation($"{LogPrefix} State saved successfully.");
            return state with
            {
                // インデックスを更新
                LastSavedMessageIndex = state.ChatMessages.Count()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);

            return state;
        }
    }

    private async Task<ChatCompletion?> CompleteChatAsync(ConversationState state)
    {
        try
        {
            // ToolCallを要求されていない状態でTooLChatMessageを送信すると400エラーになるのでスキップ
            var recentMessages = state.FullChatMessages.TakeLast(20).SkipWhile(m => m is ToolChatMessage).ToList();
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
    public async IAsyncEnumerable<(ConversationState state, ConversationResult? result)> TalkAsync(string input, ConversationState state)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            yield return (state, null);
            yield break;
        }

        _logger.LogInformation($"{LogPrefix} Starting conversation with input: '{input}'");

        state = await EnsureConversationExistsAsync(state);

        // 姉妹切り替え
        var now = _dateTimeProvider.Now;
        state = TrySwitchSister(input, now, state);

        // 返信を生成
        state = state.AddUserMessage(input, now);
        var completion = await CompleteChatAsync(state);
        if (completion is null)
        {
            yield return (state, null);
            yield break;
        }

        // 忍耐値の処理
        state = UpdatePatienceCounter(completion, state);

        // 怠け癖モード処理
        LazyModeResult lazyResult;
        (lazyResult, state) = await _lazyModeHandler.HandleLazyModeAsync(
            completion,
            state,
            now,
            CompleteChatAsync);

        // 怠け癖時のタスク押し付け応答を返す
        if (lazyResult.LazyResponse is not null)
        {
            // 怠けると姉妹が入れ替わるのでカウンターをリセット
            state = state with
            {
                PatienceCount = 0
            };

            yield return (state, lazyResult.LazyResponse);
        }

        // 最終的な完了結果を使用
        completion = lazyResult.FinalCompletion;
        if (completion is null)
        {
            yield return (state, null);
            yield break;
        }
        state = state.AddAssistantMessage(completion);

        // 関数の実行
        List<ConversationFunction> functions;
        (state, completion, functions) = await InvokeFunctions(completion, state);

        // 記憶削除時は新しい会話にする
        state = await HandleMemoryDeletionAsync(functions, state);
        state = await SaveState(state);

        // フロントに生成テキストを送信
        if (ChatResponse.TryParse(completion.Content[0].Text, out var response))
        {
            var result = new ConversationResult
            {
                Message = response?.Text ?? string.Empty,
                Sister = state.CurrentSister,
                Functions = functions
            };

            yield return (state, result);
        }
        else
        {
            _logger.LogError($"生成結果のパースに失敗しました: {completion.Content[0].Text}");

            yield return (state, null);
        }
    }

    /// <summary>
    /// 会話が存在しない場合は新規作成します
    /// </summary>
    private async Task<ConversationState> EnsureConversationExistsAsync(ConversationState state)
    {
        if (state.ConversationId is null)
        {
            return await CreateNewConversationAsync();
        }
        else
        {
            return state;
        }
    }

    /// <summary>
    /// 忍耐値を更新します
    /// </summary>
    private ConversationState UpdatePatienceCounter(ChatCompletion completion, ConversationState state)
    {
        return completion.FinishReason == ChatFinishReason.ToolCalls
            ? state.RecordToolCall()
            : state;
    }

    /// <summary>
    /// 記憶削除時は新しい会話を作成します
    /// </summary>
    private async Task<ConversationState> HandleMemoryDeletionAsync(List<ConversationFunction> functions, ConversationState state)
    {
        if (functions.Any(f => f.Name == nameof(ForgetMemory) && f.Result == ForgetMemory.SuccessMessage))
        {
            _logger.LogInformation($"{LogPrefix} Memory deletion detected. Creating new conversation...");
            return await CreateNewConversationAsync();
        }
        else
        {
            return state;
        }
    }

    /// <summary>
    /// Function callingで呼び出された関数の実行を行います
    /// </summary>
    /// <param name="completion"></param>
    /// <returns></returns>
    private async Task<(ConversationState state, ChatCompletion result, List<ConversationFunction> functions)> InvokeFunctions(ChatCompletion completion, ConversationState state)
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
                    state = state.AddToolMessage(toolCall.Id, $"Function '{toolCall.FunctionName} does not exist.'");
                    continue;
                }

                _logger.LogInformation($"{LogPrefix} Executing function: {toolCall.FunctionName}");
                var result = await function.Invoke(doc, _state);
                if (result is null)
                {
<<<<<<< HEAD
                    _logger.LogWarning($"{LogPrefix} Failed to parse arguments of '{toolCall.FunctionName}'.");
                    state = state.AddToolMessage(toolCall.Id, $"Failed to parse arguments of '{toolCall.FunctionName}'.");
                    continue;
                }

                _logger.LogInformation($"{LogPrefix} Executing function: {toolCall.FunctionName}");
                var result = await function.Invoke(arguments, state);
=======
                    _logger.LogWarning($"{LogPrefix} Failed to invoke function '{toolCall.FunctionName}'.");
                    _state.AddToolMessage(toolCall.Id, $"Failed to invoke function: '{toolCall.FunctionName}'.");
                    continue;
                }

>>>>>>> 45b5b57 (ToolFunctionのプロパティ定義にJsonSchemaExporterを使用)
                invokedFunctions.Add(new ConversationFunction
                {
                    Name = toolCall.FunctionName,
                    Arguments = doc.RootElement.EnumerateObject().ToDictionary(obj => obj.Name, obj => (object)obj.Value.GetString()),
                    Result = result
                });

                state = state.AddToolMessage(toolCall.Id, result);
            }

            var nextCompletion = await CompleteChatAsync(state);
            if (nextCompletion is null)
            {
                continue;
            }

            completion = nextCompletion;
            state = state.AddAssistantMessage(completion);
        }

        return (state, completion, invokedFunctions);
    }

    /// <summary>
    /// ユーザー入力を解析し、必要に応じて姉妹を切り替えます
    /// </summary>
    /// <param name="userInput">ユーザーの入力テキスト</param>
    /// <returns>姉妹が切り替わった場合はtrue</returns>
    public ConversationState TrySwitchSister(string userInput, DateTime dateTime, ConversationState state)
    {
        var nextSister = GuessTargetSister(userInput);
        if (nextSister == null || nextSister == state.CurrentSister)
        {
            return state;
        }

        _logger.LogInformation($"{LogPrefix} Sister switch detected: {state.CurrentSister} -> {nextSister.Value}");

        // Atomic state update with instruction
        return state.SwitchToSister(nextSister.Value, dateTime);
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

using KotonohaAssistant.AI.Extensions;
using KotonohaAssistant.AI.Functions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Repositories;
using KotonohaAssistant.AI.Utils;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Extensions;
using KotonohaAssistant.Core.Utils;
using OpenAI.Assistants;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KotonohaAssistant.AI.Services;

public class ConversationService
{
    private readonly ConversationState _state;
    private readonly List<ThreadMessage> _messages = new List<ThreadMessage>();

    private readonly IDictionary<string, ToolFunction> _functions;
    private readonly Random _r = new();

    private readonly ChatCompletionOptions _options;

    private readonly IAssistantDataRepository _assistantDataRepository;
    private readonly IAssistantRepository _assistantRepository;
    private readonly ILogger _logger;

    private string? _currentThreadId = null;
    private string? _akaneAssistantId = null;
    private string? _aoiAssistantId = null;

    private string CurrentAssistantId => _state.CurrentSister switch
    {
        Kotonoha.Akane when _akaneAssistantId is not null => _akaneAssistantId,
        Kotonoha.Aoi when _aoiAssistantId is not null => _aoiAssistantId,
        _ => throw new Exception()
    };

    public IReadOnlyConversationState State => _state;

    public ConversationService(
        IAssistantDataRepository assistantDataRepository,
        IAssistantRepository assistantRepository,
        IList<ToolFunction> availableFunctions,
        ILogger logger,
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

        _assistantDataRepository = assistantDataRepository;
        _assistantRepository = assistantRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<(Kotonoha? sister, string message)>> GetAllMessages()
    {
        var threadId = await GetThreadIdAsync();
        var messages = await _assistantRepository.GetMessagesAsync(threadId);
        var result = new List<(Kotonoha? sister, string message)>();
        foreach (var message in messages)
        {
            if (message.Role == MessageRole.User && message.Content[0].Text.StartsWith("私:"))
            {
                result.Add((null, message.Content[0].Text.Replace("私: ", string.Empty)));
            }

            if (message.Role == MessageRole.Assistant &&
                message.Content.Any())
            {
                var (sister, _, messageText) = ParseMessage(message.Content[0].Text);
                result.Add((sister, messageText));
            }
        }

        result.Reverse();
        return result;
    }

    private async Task<string> GetAssistantIdAsync(Kotonoha sister)
    {
        var (name, instruction) = sister switch
        {
            Kotonoha.Akane => ("Kotonoha Akane", Instructions.KotonohaAkane),
            Kotonoha.Aoi => ("Kotonoha Aoi", Instructions.KotonohaAoi),
            _ => throw new Exception()
        };

        var assistants = await _assistantDataRepository.GetAssistantDataAsync(name);
        var latest = assistants.OrderByDescending(assistant => assistant.CreatedAt).FirstOrDefault();
        if (latest is null || latest.Id is null)
        {
            var assistant = await _assistantRepository.CreateAssistantAsync("gpt-4o-mini", name, instruction, _functions.Values);
            await _assistantDataRepository.InsertAssistantDataAsync(assistant.Id, name);
            return assistant.Id;
        }

        try
        {
            var assistant = await _assistantRepository.GetAssistantAsync(latest.Id);
            return assistant.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            var assistant = await _assistantRepository.CreateAssistantAsync("gpt-4o-mini", name, instruction, _functions.Values);
            await _assistantDataRepository.InsertAssistantDataAsync(assistant.Id, name);
            return assistant.Id;
        }
    }

    public async Task<string> CreateNewThreadAsync()
    {
        try
        {
            var thread = await _assistantRepository.CreateThreadAsync([
                (MessageRole.Assistant, "葵 [平常心]: はじめまして、マスター。私は琴葉葵。こっちは姉の茜。"),
                (MessageRole.Assistant, "茜 [平常心]: 今日からうちらがマスターのことサポートするで。"),
                (MessageRole.Assistant, "葵 [喜び]: これから一緒に過ごすことになるけど、気軽に声をかけてね。"),
                (MessageRole.Assistant, "茜 [喜び]: せやな！これからいっぱい思い出作っていこな。"),
                (MessageRole.User, "私: うん。よろしくね。")
                ]);
            await _assistantDataRepository.InsertThreadDataAsync(thread.Id);

            _currentThreadId = thread.Id;

            return _currentThreadId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex);
            throw;
        }
    }

    private async Task<string> GetThreadIdAsync()
    {
        var threads = await _assistantDataRepository.GetThreadDataAsync();
        var latest = threads.OrderByDescending(thread => thread.CreatedAt).FirstOrDefault();
        if (latest is not null && latest.Id is not null)
        {
            return latest.Id;
        }

        return await CreateNewThreadAsync();
    }

    public async Task LoadLatestThread()
    {
        var threadId = await GetThreadIdAsync();
        _currentThreadId = threadId;

        var messages = await _assistantRepository.GetMessagesAsync(threadId);
        messages.Reverse();

        _messages.AddRange(messages);

        if (_messages is [])
        {
            return;
        }

        var lastText = _messages.Last().Content[0].Text;
        if (lastText.StartsWith("茜"))
        {
            _state.CurrentSister = Kotonoha.Akane;
        }
        if (lastText.StartsWith("葵"))
        {
            _state.CurrentSister = Kotonoha.Aoi;
        }
    }

    public async IAsyncEnumerable<ConversationResult> TalkWithKotonohaSisters(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            yield break;
        }

        if (_akaneAssistantId is null)
        {
            _akaneAssistantId = await GetAssistantIdAsync(Kotonoha.Akane);
        }

        if (_aoiAssistantId is null)
        {
            _aoiAssistantId = await GetAssistantIdAsync(Kotonoha.Aoi);
        }

        if (_currentThreadId is null)
        {
            _currentThreadId = await GetThreadIdAsync();
        }

        // 姉妹切り替え
        var nextSister = GuessTargetSister(input);
        switch (nextSister)
        {
            case Kotonoha.Akane when _state.CurrentSister == Kotonoha.Aoi:
                _state.CurrentSister = Kotonoha.Akane;
                await _assistantRepository.CreateMessageAsync(_currentThreadId, MessageRole.User, $"[Hint]: {Hint.SwitchSisterTo(Kotonoha.Akane)}");
                break;
            case Kotonoha.Aoi when _state.CurrentSister == Kotonoha.Akane:
                _state.CurrentSister = Kotonoha.Aoi;
                await _assistantRepository.CreateMessageAsync(_currentThreadId, MessageRole.User, $"[Hint]: {Hint.SwitchSisterTo(Kotonoha.Aoi)}");
                break;
            default:
                break;
        }

        // 返信を生成
        var message = await _assistantRepository.CreateMessageAsync(_currentThreadId, MessageRole.User, $"私: {input}");
        var run = await _assistantRepository.CreateRunAsync(_currentThreadId, CurrentAssistantId, Hint.CurrentDateTime);
        run = await _assistantRepository.WaitForRunCompletedAsync(_currentThreadId, run.Id);

        // 忍耐値の処理
        if (run.RequiredActions is not [])
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
        if (ShouldBeLazy(run))
        {
            // 関数の実行はすべてキャンセル
            run = await _assistantRepository.CancelRunAsync(_currentThreadId, run.Id);

            // ヒント挿入
            await InsertBeginLazyModeHint(_currentThreadId);

            // 実行
            run = await _assistantRepository.CreateRunAsync(_currentThreadId, CurrentAssistantId, Hint.CurrentDateTime);
            run = await _assistantRepository.WaitForRunCompletedAsync(_currentThreadId, run.Id);

            // それでも関数呼び出しされることがあるのでチェック
            if (run.RequiredActions is not [])
            {
                await _assistantRepository.CreateMessageAsync(_currentThreadId, MessageRole.User, $"[Hint] {Hint.CancelLazyMode}");
            }
            // 実際に怠けた際の処理
            else
            {
                message = await _assistantRepository.GetLatestMessageAsync(_currentThreadId);
                var (_, emotion, m) = ParseMessage(message?.Content[0].Text ?? string.Empty);
                yield return new ConversationResult
                {
                    Message = m,
                    Emotion = emotion,
                    Sister = _state.CurrentSister
                };

                // 怠け癖終了
                await InsertEndLazyModeHint(_currentThreadId);

                // 姉妹を切り替え
                _state.CurrentSister = _state.CurrentSister.Switch();
                await _assistantRepository.CreateMessageAsync(_currentThreadId, MessageRole.User, $"[Hint]: {Hint.SwitchSisterTo(_state.CurrentSister)}");

                // 実行
                run = await _assistantRepository.CreateRunAsync(_currentThreadId, CurrentAssistantId, Hint.CurrentDateTime);
                run = await _assistantRepository.WaitForRunCompletedAsync(_currentThreadId, run.Id);

                // 怠けると姉妹が入れ替わるのでカウンターをリセット
                _state.PatienceCount = 1;
            }
        }

        // 関数実行
        var functions = new List<ConversationFunction>();
        if (run.RequiredActions.Any())
        {
            var outputs = new List<(string toolCallId, string output)>();
            foreach (var action in run.RequiredActions)
            {
                using var doc = JsonDocument.Parse(action.FunctionArguments);
                if (!_functions.TryGetValue(action.FunctionName, out var function) || function is null)
                {
                    continue;
                }

                if (!function.TryParseArguments(doc, out var arguments))
                {
                    continue;
                }

                var result = await function.Invoke(arguments, _state);
                outputs.Add((action.ToolCallId, result));
                functions.Add(new ConversationFunction
                {
                    Name = action.FunctionName,
                    Arguments = arguments,
                    Result = result
                });
            }

            run = await _assistantRepository.SubmitFunctionOutputsAsync(run.ThreadId, run.Id, outputs);
        }

        run = await _assistantRepository.WaitForRunCompletedAsync(_currentThreadId, run.Id);
        message = await _assistantRepository.GetLatestMessageAsync(_currentThreadId);

        {
            var (_, emotion, m) = ParseMessage(message?.Content[0].Text ?? string.Empty);
            yield return new ConversationResult
            {
                Message = m,
                Emotion = emotion,
                Sister = _state.CurrentSister,
                Functions = functions
            };
        }

        // 記憶削除時は新しい会話にする
        if (functions.Any(f => f.Name == nameof(ForgetMemory) && f.Result == ForgetMemory.SuccessMessage))
        {
            _currentThreadId = await CreateNewThreadAsync();
        }

        Task InsertBeginLazyModeHint(string threadId)
        {
            switch (_state.CurrentSister)
            {
                case Kotonoha.Akane:
                    _assistantRepository.CreateMessageAsync(threadId, MessageRole.User, $"[Hint] {Hint.BeginLazyModeAkane}");
                    break;
                case Kotonoha.Aoi:
                    _assistantRepository.CreateMessageAsync(threadId, MessageRole.User, $"[Hint] {Hint.BeginLazyModeAoi}");
                    break;
            }

            return Task.CompletedTask;
        }

        Task InsertEndLazyModeHint(string threadId)
        {
            switch (_state.CurrentSister)
            {
                case Kotonoha.Akane:
                    _assistantRepository.CreateMessageAsync(threadId, MessageRole.User, $"[Hint] {Hint.EndLazyModeAkane}");
                    break;
                case Kotonoha.Aoi:
                    _assistantRepository.CreateMessageAsync(threadId, MessageRole.User, $"[Hint] {Hint.EndLazyModeAoi}");
                    break;
            }

            return Task.CompletedTask;
        }
    }

    private static (Kotonoha sister, Emotion emotion, string message) ParseMessage(string input)
    {
        var match = Regex.Match(input, @"^(?<sister>茜|葵) \[(?<emotion>.+?)\]: (?<message>.+)$");
        var sister = match.Groups["sister"].Value;
        var emotion = match.Groups["emotion"].Value;
        var message = match.Groups["message"].Value;

        var sisterType = sister switch
        {
            "茜" => Kotonoha.Akane,
            "葵" => Kotonoha.Aoi,
            _ => Kotonoha.Akane // 不明な場合は一旦茜ちゃん
        };

        var emotionType = emotion switch
        {
            "平常心" => Emotion.Calm,
            "喜び" => Emotion.Joy,
            "怒り" => Emotion.Anger,
            "悲しみ" => Emotion.Sadness,
            _ => Emotion.Calm
        };

        return (sisterType, emotionType, message);
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


    private bool ShouldBeLazy(ThreadRun run)
    {
        if (run.RequiredActions is [])
        {
            return false;
        }

        var targetFunctions = run.RequiredActions
            .Where(action => _functions.ContainsKey(action.FunctionName))
            .Select(action => _functions[action.FunctionName]);
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

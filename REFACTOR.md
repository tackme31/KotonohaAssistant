# Stateless Refactoring - 修正すべき点

このドキュメントは、state-refactor ブランチのステートレスリファクタリングに対するコードレビュー結果をまとめたものです。

## 本番リリース判定: ⚠️ Not Production Ready

**見積もり工数**: Critical 修正 + テスト = 3-5日

---

## 🔴 Critical Issues (修正必須)

### Issue #1: 姉妹切り替え時の状態不整合

**問題箇所**: `ConversationService.cs:431-446`

**問題内容**:
- 姉妹切り替えが発生しても `Instruction` メッセージが会話履歴に追加されていない
- CLAUDE.md のガイドライン違反: "Sister changes must add an Instruction message to conversation history"
- 状態の一貫性が保証されていない

**現在のコード**:
```csharp
public ConversationState TrySwitchSister(string userInput, ConversationState state)
{
    var nextSister = GuessTargetSister(userInput);
    if (nextSister == null || nextSister == state.CurrentSister)
    {
        return state;
    }

    _logger.LogInformation($"{LogPrefix} Sister switch detected: {state.CurrentSister} -> {nextSister.Value}");
    return state with
    {
        CurrentSister = nextSister.Value
    };
}
```

**修正案**:
```csharp
public ConversationState TrySwitchSister(string userInput, ConversationState state)
{
    var nextSister = GuessTargetSister(userInput);
    if (nextSister == null || nextSister == state.CurrentSister)
    {
        return state;
    }

    _logger.LogInformation($"{LogPrefix} Sister switch detected: {state.CurrentSister} -> {nextSister.Value}");

    // Atomic state update with instruction
    var instruction = Instruction.SwitchSisterTo(nextSister.Value);
    return state
        .AddInstruction(instruction, DateTime.Now)
        .with
        {
            CurrentSister = nextSister.Value
        };
}
```

**または Extension Method を追加**:
```csharp
// ConversationStateExtensions.cs
public static ConversationState SwitchToSister(
    this ConversationState state,
    Kotonoha sister,
    DateTime dateTime)
{
    if (state.CurrentSister == sister)
        return state;

    var instruction = Instruction.SwitchSisterTo(sister);
    return state
        .AddInstruction(instruction, dateTime)
        with
        {
            CurrentSister = sister,
            PatienceCount = 0
        };
}
```

---

### Issue #2: ImmutableList のパフォーマンス問題

**問題箇所**: `ConversationState.cs:23` / `ConversationStateExtensions.cs` の全メソッド

**問題内容**:
- `ImmutableList<T>.Add()` は O(log n) のコストがかかる
- 会話が長くなるほど追加コストが累積
- 頻繁な追加操作には `ImmutableArray<T>` の方が適切

**現在のコード**:
```csharp
public record ConversationState
{
    public ImmutableList<ChatMessage> ChatMessages { get; set; } = [];
    // ...
}
```

**修正案 Option 1: ImmutableArray への変更（推奨）**:
```csharp
public record ConversationState
{
    public ImmutableArray<ChatMessage> ChatMessages { get; init; }
        = ImmutableArray<ChatMessage>.Empty;
    // ...
}

// Extension methods も同様に変更
public static ConversationState AddUserMessage(this ConversationState state, string text, DateTime dateTime)
{
    var message = CreateUserMessage(ChatInputType.User, text, dateTime);
    return state with
    {
        ChatMessages = state.ChatMessages.Add(message)
    };
}
```

**修正案 Option 2: Builder パターン使用**:
```csharp
public static ConversationState AddMessages(
    this ConversationState state,
    params ChatMessage[] messages)
{
    var builder = state.ChatMessages.ToBuilder();
    builder.AddRange(messages);
    return state with
    {
        ChatMessages = builder.ToImmutable()
    };
}
```

**推奨**: Option 1 (ImmutableArray への変更)

---

### Issue #3: メモリリークの可能性

**問題箇所**: `ConversationState.cs` 全体

**問題内容**:
- 会話履歴が無制限に成長
- OpenAI API のトークン制限超過リスク
- メモリ使用量の増大

**修正案: Message Window パターンの実装**:

```csharp
// ConversationStateExtensions.cs に追加
private const int MaxHistoryLength = 50;
private const int InitialConversationCount = 6; // InitialConversation.Messages.Count

public static ConversationState TrimMessages(this ConversationState state)
{
    // 初期会話 + 最新メッセージを保持
    if (state.ChatMessages.Length <= MaxHistoryLength)
        return state;

    // 初期会話メッセージを保持
    var initialMessages = state.ChatMessages
        .Take(InitialConversationCount)
        .ToList();

    // 最新のメッセージを取得
    var recentMessages = state.ChatMessages
        .Skip(InitialConversationCount)
        .TakeLast(MaxHistoryLength - InitialConversationCount)
        .ToList();

    return state with
    {
        ChatMessages = initialMessages
            .Concat(recentMessages)
            .ToImmutableArray()
    };
}

// ConversationService.cs の SaveState メソッド内で呼び出す
private async Task<ConversationState> SaveState(ConversationState state)
{
    // ... 既存の保存処理 ...

    // Trim old messages after saving
    state = state.TrimMessages();

    return state;
}
```

**または、CompleteChatAsync で制限**:
```csharp
private async Task<ChatCompletion?> CompleteChatAsync(ConversationState state)
{
    try
    {
        // ToolCallを要求されていない状態でToolChatMessageを送信すると400エラーになるのでスキップ
        // さらに、最新20件のみを送信してトークン制限を回避
        var recentMessages = state.FullChatMessages
            .TakeLast(20)
            .SkipWhile(m => m is ToolChatMessage)
            .ToList();

        return await _chatCompletionRepository.CompleteChatAsync(recentMessages, _options);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex);
        return null;
    }
}
```

---

## 🟠 High Priority Issues (要対応)

### Issue #4: エラーリカバリの不完全性

**問題箇所**: `ConversationService.cs:260-302` (TalkAsync メソッド)

**問題内容**:
- Exception 発生時に state が破棄される
- エラー前の会話コンテキストが失われる
- 再試行やロールバック機構がない

**修正案**:
```csharp
public async IAsyncEnumerable<(ConversationState state, ConversationResult? result)> TalkAsync(
    string input,
    ConversationState state)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        yield break;
    }

    _logger.LogInformation($"{LogPrefix} Starting conversation with input: '{input}'");

    try
    {
        state = await EnsureConversationExistsAsync(state);

        // 姉妹切り替え
        state = TrySwitchSister(input, state);

        // 返信を生成
        var now = DateTime.Now;
        state = state.AddUserMessage(input, now);

        var completion = await CompleteChatAsync(state);
        if (completion is null)
        {
            // エラー時も state を保持して返す
            yield return (state, null);
            yield break;
        }

        // ... 残りの処理 ...
    }
    catch (Exception ex)
    {
        _logger.LogError($"{LogPrefix} Error in TalkAsync: {ex.Message}", ex);

        // Preserve state and return error response
        var errorResponse = new ConversationResult
        {
            Message = "すまん、ちょっとエラーが出てもうた...",
            Sister = state.CurrentSister,
            Functions = []
        };

        yield return (state, errorResponse);
    }
}
```

---

### Issue #5: クライアント側の Thread-Safety 欠如

**問題箇所**: `Program.cs:119-145` (CLI) / `Chat.razor:247-265` (VUI)

**問題内容**:
- `state` が mutable local variable として扱われている
- Async enumeration 中に state 更新が複数回発生
- 並行リクエスト時に state corruption のリスク

**修正案 (CLI)**:
```csharp
var state = await service.LoadLatestConversation();
foreach (var (sister, message) in service.GetAllMessages(state))
{
    var name = sister?.ToDisplayName() ?? "私";
    Console.WriteLine($"{name}: {message}");
}

while (true)
{
    Console.Write("私: ");
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input))
    {
        continue;
    }

    // Create immutable snapshot for this conversation turn
    ConversationState currentState = state;
    ConversationState? latestState = null;

    await foreach (var item in service.TalkAsync(input, currentState))
    {
        var result = item.result;
        if (result is null)
        {
            continue;
        }

        // Accumulate latest state
        latestState = item.state;

        // Display results...
        if (result.Functions is not null)
        {
            foreach (var function in result.Functions)
            {
                Console.WriteLine($"[Function] {function.Name}: {function.Result}");
            }
        }

        Console.WriteLine($"{result.Sister.ToDisplayName()}: {result.Message}");
    }

    // Atomic update after enumeration completes
    if (latestState is not null)
    {
        state = latestState;
    }
}
```

**修正案 (VUI - Chat.razor)**:
```csharp
private async Task OnRecognitionResult(string message)
{
    if (string.IsNullOrWhiteSpace(message))
        return;

    _messages.Add((message, null, null));
    await InvokeAsync(StateHasChanged);
    ScrollToEnd();

    var forgotten = false;

    // Create immutable snapshot
    ConversationState currentState = _state!;
    ConversationState? latestState = null;

    await foreach (var item in ConversationService.TalkAsync(message, currentState))
    {
        var result = item.result;
        if (result is null)
        {
            continue;
        }

        // Accumulate latest state
        latestState = item.state;

        // Display results...
        if (result.Functions is not null)
        {
            foreach (var function in result.Functions)
            {
                if (function.Name == nameof(ForgetMemory) &&
                    function.Result == ForgetMemory.SuccessMessage)
                {
                    forgotten = true;
                }

                _messages.Add((function.Result, null, function.Name));
            }
        }

        _messages.Add((result.Message, result.Sister, null));
        await InvokeAsync(StateHasChanged);
        ScrollToEnd();
    }

    // Atomic update after enumeration completes
    if (latestState is not null)
    {
        _state = latestState;
    }

    if (forgotten)
    {
        _messages.Clear();
    }

    RestartConversationTimer();
}
```

---

### Issue #6: 関数呼び出しループの状態一貫性問題

**問題箇所**: `ConversationService.cs:377-427` (InvokeFunctions メソッド)

**問題内容**:
- Loop 内で state が複数回更新される
- Loop の途中で continue した場合、部分的な state 更新が残る
- Tool call 失敗時の rollback 機構がない

**修正案**:
```csharp
private async Task<(ConversationState state, ChatCompletion result, List<ConversationFunction> functions)>
    InvokeFunctions(ChatCompletion completion, ConversationState state)
{
    var invokedFunctions = new List<ConversationFunction>();

    while (completion.FinishReason == ChatFinishReason.ToolCalls)
    {
        var toolCalls = completion.ToolCalls?.ToList();
        if (toolCalls is null || toolCalls.Count == 0)
        {
            break;
        }

        // Accumulate state updates in transaction
        ConversationState transactionState = state;
        bool shouldBreak = false;

        foreach (var toolCall in toolCalls)
        {
            var doc = JsonDocument.Parse(toolCall.FunctionArguments);
            if (!_functions.TryGetValue(toolCall.FunctionName, out var function) || function is null)
            {
                _logger.LogWarning($"{LogPrefix} Function '{toolCall.FunctionName}' does not exist.");
                transactionState = transactionState.AddToolMessage(
                    toolCall.Id,
                    $"Function '{toolCall.FunctionName} does not exist.'");
                continue;
            }

            if (!function.TryParseArguments(doc, out var arguments))
            {
                _logger.LogWarning($"{LogPrefix} Failed to parse arguments of '{toolCall.FunctionName}'.");
                transactionState = transactionState.AddToolMessage(
                    toolCall.Id,
                    $"Failed to parse arguments of '{toolCall.FunctionName}'.");
                continue;
            }

            _logger.LogInformation($"{LogPrefix} Executing function: {toolCall.FunctionName}");

            try
            {
                var result = await function.Invoke(arguments, transactionState);
                invokedFunctions.Add(new ConversationFunction
                {
                    Name = toolCall.FunctionName,
                    Arguments = toolCall.FunctionArguments.ToString(),
                    Result = result
                });

                transactionState = transactionState.AddToolMessage(toolCall.Id, result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"{LogPrefix} Error executing function {toolCall.FunctionName}: {ex.Message}", ex);

                // Add error message to transaction
                transactionState = transactionState.AddToolMessage(
                    toolCall.Id,
                    $"Error executing function: {ex.Message}");

                shouldBreak = true;
                break;
            }
        }

        // Commit transaction only if no critical errors
        if (!shouldBreak)
        {
            state = transactionState;
        }

        var nextCompletion = await CompleteChatAsync(state);
        if (nextCompletion is null)
        {
            break;
        }

        completion = nextCompletion;
        state = state.AddAssistantMessage(completion);
    }

    return (state, completion, invokedFunctions);
}
```

---

## 🟡 Recommendations (改善推奨)

### Recommendation #7: Lazy Mode の Builder パターン適用

**問題箇所**: `LazyModeHandler.cs:63-135`

**改善案**:

```csharp
// 新しいヘルパークラスを追加
public class ConversationStateBuilder
{
    private ConversationState _state;

    public ConversationStateBuilder(ConversationState state)
    {
        _state = state;
    }

    public ConversationStateBuilder AddInstruction(string instruction, DateTime dateTime)
    {
        _state = _state.AddInstruction(instruction, dateTime);
        return this;
    }

    public ConversationStateBuilder AddAssistantMessage(ChatCompletion completion)
    {
        _state = _state.AddAssistantMessage(completion);
        return this;
    }

    public ConversationStateBuilder SwitchToAnotherSister()
    {
        _state = _state.SwitchToAnotherSister();
        return this;
    }

    public ConversationState Build() => _state;
}

// LazyModeHandler.cs の HandleLazyModeAsync を書き換え
public async Task<(LazyModeResult result, ConversationState state)> HandleLazyModeAsync(
    ChatCompletion completion,
    ConversationState state,
    DateTime dateTime,
    Func<ConversationState, Task<ChatCompletion?>> regenerateCompletionAsync)
{
    if (!ShouldBeLazy(completion, state))
    {
        return (
            new LazyModeResult
            {
                FinalCompletion = completion,
                WasLazy = false,
                LazyResponse = null
            },
            state);
    }

    _logger.LogInformation($"{LogPrefix} Lazy mode activated for {state.CurrentSister}.");

    var builder = new ConversationStateBuilder(state);

    // Step 1: Current sister refuses
    builder.AddBeginLazyModeInstruction(dateTime);

    var lazyCompletion = await regenerateCompletionAsync(builder.Build());
    if (lazyCompletion is null || lazyCompletion.FinishReason == ChatFinishReason.ToolCalls)
    {
        _logger.LogWarning($"{LogPrefix} Lazy mode cancelled: still received tool calls.");
        builder.AddInstruction(Prompts.Instruction.CancelLazyMode, dateTime);
        return (
            new LazyModeResult
            {
                FinalCompletion = completion,
                WasLazy = false,
                LazyResponse = null
            },
            builder.Build());
    }

    builder.AddAssistantMessage(lazyCompletion);

    // Save lazy response
    ConversationResult? lazyResponse = null;
    if (ChatResponse.TryParse(lazyCompletion.Content[0].Text, out var response) && response is not null)
    {
        lazyResponse = new ConversationResult
        {
            Message = response.Text ?? string.Empty,
            Sister = state.CurrentSister,
            Functions = []
        };
    }

    // Step 2: Switch to other sister
    var previousSister = state.CurrentSister;
    builder
        .SwitchToAnotherSister()
        .AddEndLazyModeInstruction(dateTime);

    _logger.LogInformation($"{LogPrefix} Switching sister: {previousSister} -> {builder.Build().CurrentSister}");

    var acceptCompletion = await regenerateCompletionAsync(builder.Build());
    if (acceptCompletion is null)
    {
        _logger.LogWarning($"{LogPrefix} Failed to generate acceptance response.");
        return (
            new LazyModeResult
            {
                FinalCompletion = completion,
                WasLazy = false,
                LazyResponse = null
            },
            builder.Build());
    }

    _logger.LogInformation($"{LogPrefix} Lazy mode completed successfully.");

    return (
        new LazyModeResult
        {
            FinalCompletion = acceptCompletion,
            WasLazy = true,
            LazyResponse = lazyResponse
        },
        builder.Build());
}
```

---

### Recommendation #8: Version 番号の修正

**問題箇所**: `KotonohaAssistant.AI/KotonohaAssistant.AI.csproj:7`

**問題内容**:
- Breaking change (stateless refactoring) なのにバージョンダウン (0.3.0 → 0.2.2)
- Semantic Versioning に反する

**修正案**:
```xml
<Version>1.0.0</Version>
```

**理由**: Major architectural change であり、API の互換性が破壊されているため、Major version を上げるべき

---

### Recommendation #9: CLAUDE.md の更新

**問題箇所**: `CLAUDE.md:206-238` (Key Architectural Patterns セクション)

**追加すべき内容**:

```markdown
### 0. Stateless Service Architecture (v1.0.0~)

**Design Philosophy**:
`ConversationService` は **ステートレス** な設計となっており、会話状態はクライアントアプリケーションが管理します。

**Key Concepts**:
- **ConversationState** - Immutable record として会話コンテキストを保持
- **State Management** - クライアントが state を保存し、サービスメソッドに渡す
- **State Updates** - 全てのメソッドが新しい state を返す（関数型スタイル）
- **Thread-Safety** - Immutable state により並行性の問題を排除

**State Flow**:
```
Client holds state → Pass to service → Service returns new state → Client updates reference
```

**Benefits**:
- **Testability** - Pure functions として実装されテストが容易
- **Thread-Safety** - Immutable data により競合状態を排除
- **Predictability** - 明示的な state flow により動作が予測可能
- **Scalability** - 共有状態がないため水平スケールが容易

**Migration from v0.x**:
v0.x では `ConversationService` が内部に `_state` を保持していましたが、v1.0.0 からは：
- `LoadLatestConversation()` が `ConversationState` を返すようになりました
- `TalkAsync()` が state を引数として受け取り、`(state, result)` のタプルを返します
- クライアントコードで state を保持・更新する必要があります

**Example (CLI)**:
```csharp
var service = new ConversationService(...);
var state = await service.LoadLatestConversation();

while (true)
{
    var input = Console.ReadLine();
    ConversationState? latestState = null;

    await foreach (var (newState, result) in service.TalkAsync(input, state))
    {
        latestState = newState;
        Console.WriteLine($"{result.Sister}: {result.Message}");
    }

    if (latestState is not null)
        state = latestState;
}
```
```

---

### Recommendation #10: Unit Test の追加

**追加すべきテストケース**:

```csharp
// KotonohaAssistant.AI.Tests/Services/ConversationServiceTests.cs

using Xunit;
using KotonohaAssistant.AI.Services;

public class ConversationServiceTests
{
    [Fact]
    public async Task TalkAsync_PreservesStateImmutability()
    {
        // Arrange
        var initialState = CreateTestState();
        var service = CreateTestService();

        // Act
        await foreach (var (newState, _) in service.TalkAsync("Hello", initialState))
        {
            // Assert - Original state should not be modified
            Assert.Empty(initialState.ChatMessages);
            Assert.NotSame(initialState, newState);
        }
    }

    [Fact]
    public async Task TalkAsync_ReturnsStateOnError()
    {
        // Arrange
        var state = CreateTestState();
        var service = CreateTestServiceWithBrokenCompletion();

        // Act
        ConversationState? finalState = null;
        await foreach (var (newState, result) in service.TalkAsync("Hello", state))
        {
            finalState = newState;
        }

        // Assert - State should be preserved despite error
        Assert.NotNull(finalState);
    }

    [Fact]
    public async Task InvokeFunctions_RollsBackOnError()
    {
        // Arrange
        var state = CreateTestState();
        var service = CreateTestServiceWithBrokenFunction();

        // Act
        var (finalState, _, functions) = await service.InvokeFunctionsAsync(
            CreateCompletionWithToolCalls(),
            state);

        // Assert - State should be consistent
        Assert.NotNull(finalState);
        Assert.NotEmpty(finalState.ChatMessages);
    }

    [Fact]
    public void TrySwitchSister_AddsInstructionMessage()
    {
        // Arrange
        var state = CreateTestState(Kotonoha.Akane);
        var service = CreateTestService();

        // Act
        var newState = service.TrySwitchSister("葵ちゃん、お願い", state);

        // Assert
        Assert.Equal(Kotonoha.Aoi, newState.CurrentSister);

        // Last message should be an instruction
        var lastMessage = newState.ChatMessages.Last();
        Assert.IsType<UserChatMessage>(lastMessage);

        var content = JsonSerializer.Deserialize<ChatRequest>(lastMessage.Content);
        Assert.Equal(ChatInputType.Instruction, content.InputType);
    }

    [Fact]
    public void ConversationState_TrimMessages_PreservesInitialConversation()
    {
        // Arrange
        var state = CreateStateWithManyMessages(100);

        // Act
        var trimmedState = state.TrimMessages();

        // Assert
        Assert.True(trimmedState.ChatMessages.Length <= 50);
        Assert.Contains(trimmedState.ChatMessages, m =>
            m.Content.Contains("初期会話メッセージ"));
    }
}
```

---

## 作業優先順位と見積もり

### Phase 1: Critical Issues (1-2日)
1. ✅ Issue #1: 姉妹切り替え時の Instruction 追加 (2時間)
2. ✅ Issue #2: ImmutableArray への変更 (4時間)
3. ✅ Issue #3: メッセージ履歴の上限設定 (2時間)

### Phase 2: High Priority Issues (1-2日)
4. ✅ Issue #4: エラーリカバリの改善 (3時間)
5. ✅ Issue #5: クライアント側 Thread-Safety (4時間)
6. ✅ Issue #6: 関数呼び出しループの改善 (4時間)

### Phase 3: Recommendations (1日)
7. 🔹 Recommendation #8: Version 番号修正 (10分)
8. 🔹 Recommendation #9: CLAUDE.md 更新 (1時間)
9. 🔹 Recommendation #10: Unit Test 追加 (4時間)
10. 🔹 Recommendation #7: Lazy Mode Builder (Optional, 2時間)

### Phase 4: Testing & Verification (半日)
- 統合テスト実行
- CLI アプリでの動作確認
- VUI アプリでの動作確認
- パフォーマンステスト

---

## チェックリスト

### Critical Issues
- [ ] Issue #1: 姉妹切り替えの Instruction 追加実装
- [ ] Issue #2: ImmutableArray への変更
- [ ] Issue #3: メッセージ履歴の上限設定

### High Priority Issues
- [ ] Issue #4: エラーリカバリ機構の追加
- [ ] Issue #5: CLI/VUI のスレッドセーフな実装
- [ ] Issue #6: 関数呼び出しループの改善

### Recommendations
- [ ] Recommendation #8: Version を 1.0.0 に変更
- [ ] Recommendation #9: CLAUDE.md のアーキテクチャセクション更新
- [ ] Recommendation #10: Unit Test 追加
- [ ] Recommendation #7: Lazy Mode Builder (Optional)

### Testing
- [ ] Core, AI プロジェクトのビルド確認
- [ ] CLI アプリの動作確認
- [ ] VUI アプリの動作確認
- [ ] 全 Unit Test 通過確認
- [ ] パフォーマンステスト実行

---

## 参考資料

- [Immutable Collections (Microsoft Docs)](https://docs.microsoft.com/en-us/dotnet/api/system.collections.immutable)
- [C# Records (Microsoft Docs)](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [Semantic Versioning](https://semver.org/)
- [Functional Programming in C#](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/functional-programming-introduction)

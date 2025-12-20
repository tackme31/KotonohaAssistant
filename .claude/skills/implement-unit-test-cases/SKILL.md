---
name: implement-unit-test-cases
description: Implements unit test cases incrementally for specified test files in C#. Use when implementing test logic, filling in NotImplementedException tests, or writing test code for existing test skeletons. Works on test files with NotImplementedException placeholders.
allowed-tools: Read, Grep, Glob, Edit, Bash
---

# Implement Unit Test Cases

This skill helps implement unit test cases incrementally for C# test files in the KotonohaAssistant project.

## What This Skill Does

1. **Analyzes test file** to identify NotImplementedException test cases
2. **Groups tests logically** by region or related functionality
3. **Implements tests incrementally** - one meaningful group at a time
4. **Verifies each implementation** by building and running tests after each group
5. **Uses helper classes** from the Helpers folder when applicable
6. **Tracks failures** and stops after 3 consecutive build/test failures
7. **Reports progress** regularly to keep the user informed

## Important Constraints

- **Incremental implementation**: NEVER implement all tests at once
- **Verify after each group**: Build and run tests after implementing each logical group
- **Failure limit**: Stop immediately after 3 consecutive failures and report progress
- **Comment thoroughly**: Add clear comments explaining what each section does
- **Use helpers**: Leverage TestStateFactory, ChatCompletionFactory, Mocks, and Extensions

## Instructions

### Step 0: Initialize Failure Counter

Set up a failure counter that tracks consecutive build/test failures:
- Initial value: 0
- Increment on each build or test failure
- Reset to 0 on successful build and test run
- **CRITICAL**: If counter reaches 3, STOP immediately and report progress

### Step 1: Analyze the Test File

Read the specified test file to:

1. **Identify NotImplementedException tests**:
   - Find all methods that throw NotImplementedException
   - Group them by `#region` tags
   - Note any XML comments documenting expected behavior

2. **Understand test structure**:
   - Identify the class under test
   - Note dependencies and setup requirements
   - Check if test uses any specific patterns or helpers

3. **Plan implementation order**:
   - Group related tests together (by region or functionality)
   - Prioritize simple cases first, then complex scenarios
   - Aim for 3-5 tests per group

Example groups:
- All tests for one specific method
- All happy path tests
- All error handling tests
- All boundary condition tests

### Step 2: Review Existing Test Patterns

Read similar test files to understand project conventions:

**Reference files**:
- `ConversationStateExtensionsTests.cs` - Extension method testing patterns
- `LazyModeHandlerTests.cs` - Async method testing, complex scenarios

**Key patterns to follow**:

1. **AAA Pattern** (Arrange, Act, Assert):
```csharp
[Fact]
public void メソッド名_条件_期待される動作()
{
    // Arrange: テストデータとモックを準備
    var state = TestStateFactory.CreateTestState();
    var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

    // Act: テスト対象メソッドを実行
    var result = state.AddUserMessage("こんにちは", dateTime);

    // Assert: 結果を検証
    result.ChatMessages.Should().HaveCount(1);
    result.ChatMessages[0].Should().BeOfType<UserChatMessage>();
}
```

2. **FluentAssertions**:
```csharp
result.Should().NotBeNull();
result.Should().Be(expectedValue);
result.Should().BeOfType<SomeType>();
collection.Should().HaveCount(3);
collection.Should().Contain(item);
value.Should().BeTrue();
```

3. **JSON Property Assertions** (using HelperExtensions):
```csharp
var json = message.Content.AsJson();
json.Should().NotBeNull();
json.Should().HavePropertyWithStringValue("InputType", "User");
json.Should().HavePropertyWithStringValue("Text", "expected text");
```

4. **Async Testing**:
```csharp
[Fact]
public async Task HandleAsync_条件_期待される動作()
{
    // Arrange
    var handler = new Handler();

    // Act
    var result = await handler.HandleAsync(input);

    // Assert
    result.Should().NotBeNull();
}
```

### Step 3: Review Available Helpers

Check the Helpers folder to see what utilities are available:

**TestStateFactory**:
- `CreateTestState(currentSister, patienceCount, conversationId, ...)` - Create ConversationState
- `CreateFunctionDictionary(canBeLazy, logger)` - Create function dictionary for tests

**ChatCompletionFactory**:
- `CreateTextCompletion(sister, text, finishReason)` - Normal text response
- `CreateRawTextCompletion(text, finishReason)` - Raw JSON text response
- `CreateToolCallsCompletion(sister, text, toolCalls)` - Response with tool calls
- `CreateStopCompletion()` - Simple stop response
- `CreateSimpleToolCallsCompletion(functionName)` - Simple tool call
- `CreateMultipleToolCallsCompletion(functionNames)` - Multiple tool calls

**Test Mocks** (in TestMocks.cs):
- `MockLogger` - Logger with log capture
- `MockDateTimeProvider` - Controllable time
- `MockRandomGenerator` - Predictable random values
- `MockToolFunction` - Test tool function
- `MockChatMessageRepository` - Repository mock
- `MockChatCompletionRepository` - Completion repository mock
- `MockPromptRepository` - Prompt repository mock
- `MockLineMessagingRepository` - LINE messaging mock

**HelperExtensions**:
- `content.AsJson()` - Parse ChatMessageContent as JSON
- `json.Should().HavePropertyWithStringValue(name, value)` - Assert JSON property

### Step 4: Implement First Test Group

Select the first logical group of tests (3-5 tests) and implement them:

1. **Read the XML comments** to understand expected behavior
2. **Write clear comments** for each section:
   - Arrange section: What data/mocks are being prepared
   - Act section: What method is being called
   - Assert section: What is being verified
3. **Use helpers** when possible instead of manual object creation
4. **Follow existing patterns** from reference test files

**Comment Guidelines**:
```csharp
[Fact]
public void AddUserMessage_ユーザーメッセージを追加できること()
{
    // Arrange: テスト用のConversationStateと日時を準備
    var state = TestStateFactory.CreateTestState();
    var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

    // Act: ユーザーメッセージを追加
    var result = state.AddUserMessage("こんにちは", dateTime);

    // Assert: ChatMessagesに1件のUserChatMessageが追加されることを確認
    result.ChatMessages.Should().HaveCount(1);
    result.ChatMessages[0].Should().BeOfType<UserChatMessage>();

    // Assert: メッセージの内容が正しいことを確認
    var userMessage = result.ChatMessages[0].Content.AsJson();
    userMessage.Should().NotBeNull();
    userMessage.Should().HavePropertyWithStringValue("InputType", "User");
    userMessage.Should().HavePropertyWithStringValue("Text", "こんにちは");
}
```

**Important**:
- Don't over-comment obvious code, but explain non-obvious logic
- Group related assertions with a comment explaining what they verify
- Use Japanese comments matching the test name convention

### Step 5: Build and Run Tests

After implementing each group:

1. **Build the test project**:
```bash
dotnet build KotonohaAssistant.AI.Tests/KotonohaAssistant.AI.Tests.csproj
```

2. **Check build result**:
   - If build succeeds: Reset failure counter to 0, proceed to run tests
   - If build fails: Increment failure counter, analyze error, fix if possible
   - If failure counter >= 3: STOP and report progress

3. **Run the implemented tests**:
```bash
dotnet test KotonohaAssistant.AI.Tests/KotonohaAssistant.AI.Tests.csproj --filter "FullyQualifiedName~[TestClassName]"
```

4. **Check test results**:
   - If all tests pass: Reset failure counter to 0, report success
   - If tests fail: Increment failure counter, analyze failures, fix if possible
   - If failure counter >= 3: STOP and report progress

5. **Report progress**:
```
✓ Implemented and verified: [GroupName] ([N] tests)
  - Test1: PASS
  - Test2: PASS
  - Test3: PASS

Progress: [M]/[Total] tests completed
Remaining: [List of remaining test groups]
```

### Step 6: Iterate for Next Group

If failure counter < 3 and tests remain:

1. **Select next logical group** of NotImplementedException tests
2. **Repeat Steps 4-5** for the new group
3. **Continue until**:
   - All tests are implemented and passing, OR
   - Failure counter reaches 3

### Step 7: Handle Failures

When build or test fails:

1. **Increment failure counter**
2. **Analyze the error**:
   - Compilation errors: Fix syntax, missing imports, type mismatches
   - Test failures: Check expected vs actual values, review test logic
3. **Attempt to fix** if the issue is clear
4. **Re-run build/test**
5. **If counter reaches 3**:

```
⚠ Stopping: 3 consecutive failures detected

Progress Summary:
✓ Completed: [list of successfully implemented test groups]
✗ Failed: [current test group]

Remaining NotImplementedException tests:
- [list of remaining tests]

Last error:
[error message]

Please review the failures and provide guidance on how to proceed.
```

## Failure Scenarios

### Build Failure Example
```
Build failed: Missing using directive for 'FluentAssertions'

Action: Add missing using statement
Failure count: 1 → Continue
```

### Test Failure Example
```
Test failed: Expected 2 but found 1

Action: Review test logic, check if expectation is correct
Failure count: 2 → Continue (one more failure allowed)
```

### Stop After 3 Failures
```
⚠ Third consecutive failure - STOPPING

Implemented successfully:
✓ AddUserMessage テスト (3 tests)
✓ AddAssistantMessage テスト (2 tests)

Failed during:
✗ AddToolMessage テスト (0/3 implemented)

Remaining:
- SwitchToSister テスト (5 tests)
- RecordToolCall テスト (3 tests)

Last error: Expected property 'ToolCallId' not found in object

Recommendation: Review how ToolChatMessage objects are created in the codebase
```

## Best Practices

1. **Start simple**: Implement straightforward tests first to build confidence
2. **Verify frequently**: Don't implement too many tests before verifying
3. **Use helpers**: Don't reinvent the wheel - leverage existing test utilities
4. **Comment meaningfully**: Explain what's being tested and why, not just what the code does
5. **Follow patterns**: Consistency with existing tests makes the codebase maintainable
6. **Handle async properly**: Use `async Task` for async methods, `await` all async calls
7. **Check XML docs**: They often contain important hints about expected behavior

## Example Workflow

1. **Initial analysis**:
   - Found 20 NotImplementedException tests
   - Grouped into 5 regions: AddUserMessage (3), AddAssistantMessage (2), AddToolMessage (3), SwitchToSister (5), RecordToolCall (7)

2. **First iteration** (AddUserMessage - 3 tests):
   - Implement all 3 tests
   - Build: ✓ Success
   - Run tests: ✓ All pass
   - Failure counter: 0
   - Report: "Completed AddUserMessage テスト (3/3 tests pass)"

3. **Second iteration** (AddAssistantMessage - 2 tests):
   - Implement both tests
   - Build: ✓ Success
   - Run tests: ✗ 1 test fails
   - Failure counter: 1
   - Fix issue: Wrong Kotonoha enum value
   - Build: ✓ Success
   - Run tests: ✓ All pass
   - Failure counter: 0 (reset on success)
   - Report: "Completed AddAssistantMessage テスト (2/2 tests pass)"

4. **Continue** until all tests implemented or failure counter reaches 3

## Important Notes

- **NEVER implement all tests at once** - this violates the incremental requirement
- **ALWAYS build and test after each group** - early verification prevents wasted effort
- **STOP immediately at 3 failures** - don't try to push through repeated failures
- **Report progress clearly** - user needs to know what's done and what's left
- **Use Japanese comments** - match the project's bilingual code style (Japanese test names, Japanese comments)

## When NOT to Use This Skill

- When test file has no NotImplementedException tests (all implemented)
- For creating new test files (use `create-unit-test-cases` instead)
- When you need to refactor existing test implementations (use regular editing)
- For non-C# or non-xUnit test frameworks

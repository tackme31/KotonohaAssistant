---
name: create-unit-test-cases
description: Creates comprehensive unit test case skeletons for specified C# files. Use when creating test cases, generating test templates, or scaffolding unit tests for C# code. Does NOT implement tests - only creates test method signatures with descriptive comments and NotImplementedException.
allowed-tools: Read, Grep, Glob, Bash
---

# Create Unit Test Cases

This skill helps create comprehensive unit test case skeletons for C# files in the KotonohaAssistant project.

## What This Skill Does

1. **Analyzes the target file** to identify testable methods, classes, and scenarios
2. **Examines existing tests** to understand project conventions and naming patterns
3. **Creates test method skeletons** with:
   - Descriptive Japanese test names following project conventions
   - XML comments documenting test purpose and expected behavior
   - `NotImplementedException` placeholder
   - Proper region organization
4. **Validates completeness** of the generated test cases
5. **Verifies all tests fail** as expected (NotImplementedException)

## Important Constraints

- **Does NOT implement tests** - only creates skeletons
- **Does NOT write test logic** - only creates method signatures with comments
- **Focus on test planning** - ensures comprehensive coverage through proper case identification

## Instructions

### Step 1: Understand the Target File

Read the specified source file to analyze:
- Public methods and their parameters
- Edge cases and boundary conditions
- Dependencies and interactions
- Error scenarios and exception handling

### Step 2: Review Existing Test Conventions

Examine existing test files (especially `ConversationStateExtensionsTests.cs` and `LazyModeHandlerTests.cs`) to understand:

**Naming Convention**:
```csharp
メソッド名_条件_期待される動作
```

Examples:
- `AddUserMessage_ユーザーメッセージを追加できること`
- `HandleLazyModeAsync_怠け癖発動_正常に完了すること`
- `SwitchToSister_同じ姉妹の場合_何もしないこと`

**Test Structure**:
```csharp
#region テスト対象メソッド名 テスト

[Fact]
public void メソッド名_条件_期待される動作()
{
    // テストの目的: [ここに詳細を記述]
    // 期待される動作: [ここに詳細を記述]
    throw new NotImplementedException();
}

#endregion
```

**Project Patterns**:
- Use `#region` and `#endregion` to group related tests
- Follow AAA pattern (Arrange, Act, Assert) - document this in comments
- Use xUnit `[Fact]` or `[Theory]` attributes
- Use FluentAssertions for assertions (document expected assertions)
- Use helper classes like `TestStateFactory` when available

### Step 3: Identify Test Cases

For each public method in the target file, identify:

1. **Normal cases** (happy path)
2. **Boundary conditions** (null, empty, min, max values)
3. **Error scenarios** (exceptions, invalid input)
4. **State transitions** (if applicable)
5. **Edge cases** specific to the method's logic

### Step 4: Generate Test Skeleton File

Create a new test file with:

**File structure**:
```csharp
using FluentAssertions;
using KotonohaAssistant.AI.Tests.Helpers;
// [other necessary usings based on target file]

namespace KotonohaAssistant.AI.Tests.[appropriate namespace];

public class [TargetClass]Tests
{
    #region [メソッド名] テスト

    /// <summary>
    /// テストの目的: [メソッド名]が[条件]の場合に[期待される結果]となること
    /// テストする内容:
    /// - [具体的なテスト項目1]
    /// - [具体的なテスト項目2]
    /// 期待される動作: [期待される結果の詳細]
    /// </summary>
    [Fact]
    public void メソッド名_条件_期待される動作()
    {
        // Arrange: [必要なオブジェクトやデータの準備]
        // Act: [テスト対象メソッドの実行]
        // Assert: [期待される結果の検証]
        throw new NotImplementedException();
    }

    #endregion
}
```

**Important**:
- Group tests by target method using `#region`
- Include comprehensive XML doc comments explaining:
  - Test purpose
  - What is being tested
  - Expected behavior
  - Specific scenarios or conditions
- Add inline comments for AAA pattern steps
- Use descriptive Japanese test names

### Step 5: Validate Test Case Completeness

Review the generated test cases and verify:

1. **Coverage**:
   - All public methods have corresponding tests
   - All identified scenarios are covered
   - Edge cases are included
   - Error scenarios are addressed

2. **Naming consistency**:
   - Follows project conventions
   - Descriptive and clear
   - Properly grouped by regions

3. **Documentation quality**:
   - XML comments explain test purpose
   - Expected behavior is clear
   - AAA pattern steps are documented

Ask the user if the test cases are comprehensive or if additional scenarios should be added.

### Step 6: Verify Tests Fail

Build and run the test file to ensure:
- All tests are discovered by the test runner
- All tests throw `NotImplementedException`
- No compilation errors

```bash
# Build the test project
dotnet build KotonohaAssistant.AI.Tests/KotonohaAssistant.AI.Tests.csproj

# Run the tests (they should all fail with NotImplementedException)
dotnet test KotonohaAssistant.AI.Tests/KotonohaAssistant.AI.Tests.csproj --filter "FullyQualifiedName~[TestClassName]"
```

Verify output shows all tests failing as expected.

## Example Output

For a method like `AddUserMessage(string message, DateTime dateTime)`, generate:

```csharp
#region AddUserMessage テスト

/// <summary>
/// テストの目的: AddUserMessageが通常のメッセージを正しく追加できること
/// テストする内容:
/// - ユーザーメッセージがChatMessagesに追加される
/// - メッセージの形式が正しい(InputType=User)
/// - 日時情報が正しく設定される
/// 期待される動作: ChatMessagesに1件のUserChatMessageが追加され、適切な形式でメッセージが保存される
/// </summary>
[Fact]
public void AddUserMessage_通常のメッセージ_正しく追加されること()
{
    // Arrange: テスト用のConversationStateとメッセージを準備
    // Act: AddUserMessageを呼び出す
    // Assert:
    //   - ChatMessages.Count == 1
    //   - ChatMessages[0] is UserChatMessage
    //   - Content.InputType == "User"
    //   - Content.Text == 期待されるメッセージ
    throw new NotImplementedException();
}

/// <summary>
/// テストの目的: AddUserMessageが空文字列を処理できること
/// テストする内容:
/// - 空文字列のメッセージが追加される
/// - 例外が発生しない
/// 期待される動作: 空文字列でもUserChatMessageが正しく追加される
/// </summary>
[Fact]
public void AddUserMessage_空文字列_正しく追加されること()
{
    // Arrange: テスト用のConversationStateと空文字列を準備
    // Act: AddUserMessageに空文字列を渡す
    // Assert: ChatMessagesに空文字列のメッセージが追加される
    throw new NotImplementedException();
}

/// <summary>
/// テストの目的: AddUserMessageがnullの場合の動作を確認
/// テストする内容:
/// - null文字列が渡された場合の処理
/// 期待される動作: ArgumentNullExceptionがスローされる、またはnullが適切に処理される
/// </summary>
[Fact]
public void AddUserMessage_nullメッセージ_適切に処理されること()
{
    // Arrange: テスト用のConversationStateとnullを準備
    // Act & Assert: AddUserMessageにnullを渡して適切な例外がスローされるか確認
    throw new NotImplementedException();
}

#endregion
```

## Tips

1. **Be thorough**: Think about all possible scenarios, not just the obvious ones
2. **Consider context**: Look at how the method is used in the codebase
3. **Follow patterns**: Maintain consistency with existing tests
4. **Document well**: Future developers should understand the test purpose without reading the implementation
5. **Group logically**: Use regions to organize related test cases

## When NOT to Use This Skill

- When implementing actual test logic (this skill only creates skeletons)
- For non-C# projects
- When tests already exist and need modification (use regular editing)

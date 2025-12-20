using System.Text.Json.Nodes;
using FluentAssertions;
using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.AI.Services;
using KotonohaAssistant.AI.Tests.Helpers;
using KotonohaAssistant.Core;
using OpenAI.Chat;

namespace KotonohaAssistant.AI.Tests.Services;

public class ConversationStateExtensionsTests
{

    #region AddUserMessage テスト

    [Fact]
    public void AddUserMessage_ユーザーメッセージを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState();
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

        // Act
        var result = state.AddUserMessage("こんにちは", dateTime);

        // Assert
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();

        var userMessage = result.ChatMessages[0].Content.AsJson();
        userMessage.Should().NotBeNull();
        userMessage.Should().HavePropertyWithStringValue("InputType", "User");
        userMessage.Should().HavePropertyWithStringValue("Text", "こんにちは");
        userMessage.Should().HavePropertyWithStringValue("Today", "2025年1月1日 (水曜日)");
        userMessage.Should().HavePropertyWithStringValue("CurrentTime", "12時30分");
    }

    #endregion

    #region AddAssistantMessage テスト

    [Fact]
    public void AddAssistantMessage_Kotonoha版_茜のメッセージを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState();

        // Act
        var result = state.AddAssistantMessage(Kotonoha.Akane, "おはようさん");

        // Assert
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<AssistantChatMessage>();

        var assistantMessage = result.ChatMessages[0].Content.AsJson();
        assistantMessage.Should().NotBeNull();
        assistantMessage.Should().HavePropertyWithStringValue("Assistant", "Akane");
        assistantMessage.Should().HavePropertyWithStringValue("Text", "おはようさん");
    }

    [Fact]
    public void AddAssistantMessage_Kotonoha版_葵のメッセージを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState();

        // Act
        var result = state.AddAssistantMessage(Kotonoha.Aoi, "おはようございます");

        // Assert
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<AssistantChatMessage>();

        var assistantMessage = result.ChatMessages[0].Content.AsJson();
        assistantMessage.Should().NotBeNull();
        assistantMessage.Should().HavePropertyWithStringValue("Assistant", "Aoi");
        assistantMessage.Should().HavePropertyWithStringValue("Text", "おはようございます");
    }

    #endregion

    #region AddInstruction テスト

    [Fact]
    public void AddInstruction_インストラクションを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState();
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

        // Act
        var result = state.AddInstruction("テスト指示", dateTime);

        // Assert
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();

        var userMessage = result.ChatMessages[0].Content.AsJson();
        userMessage.Should().NotBeNull();
        userMessage.Should().HavePropertyWithStringValue("InputType", "Instruction");
        userMessage.Should().HavePropertyWithStringValue("Text", "テスト指示");
        userMessage.Should().HavePropertyWithStringValue("Today", "2025年1月1日 (水曜日)");
        userMessage.Should().HavePropertyWithStringValue("CurrentTime", "12時30分");
    }

    #endregion

    #region AddBeginLazyModeInstruction テスト

    [Fact]
    public void AddBeginLazyModeInstruction_茜の場合_Lazyモード開始インストラクションを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Akane);
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

        // Act
        var result = state.AddBeginLazyModeInstruction(dateTime);

        // Assert
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();

        var userMessage = result.ChatMessages[0].Content.AsJson();
        userMessage.Should().NotBeNull();
        userMessage.Should().HavePropertyWithStringValue("InputType", "Instruction");
        userMessage.Should().HavePropertyWithStringValue("Text", Instruction.BeginLazyModeAkane);
    }

    [Fact]
    public void AddBeginLazyModeInstruction_葵の場合_Lazyモード開始インストラクションを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Aoi);
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

        // Act
        var result = state.AddBeginLazyModeInstruction(dateTime);

        // Assert
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();

        var userMessage = result.ChatMessages[0].Content.AsJson();
        userMessage.Should().NotBeNull();
        userMessage.Should().HavePropertyWithStringValue("InputType", "Instruction");
        userMessage.Should().HavePropertyWithStringValue("Text", Instruction.BeginLazyModeAoi);
    }

    #endregion

    #region AddEndLazyModeInstruction テスト

    [Fact]
    public void AddEndLazyModeInstruction_茜の場合_Lazyモード終了インストラクションを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Akane);
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

        // Act
        var result = state.AddEndLazyModeInstruction(dateTime);

        // Assert
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();

        var userMessage = result.ChatMessages[0].Content.AsJson();
        userMessage.Should().NotBeNull();
        userMessage.Should().HavePropertyWithStringValue("InputType", "Instruction");
        userMessage.Should().HavePropertyWithStringValue("Text", Instruction.EndLazyModeAkane);
    }

    [Fact]
    public void AddEndLazyModeInstruction_葵の場合_Lazyモード終了インストラクションを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Aoi);
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

        // Act
        var result = state.AddEndLazyModeInstruction(dateTime);

        // Assert
        result.ChatMessages.Should().HaveCount(1);
        result.ChatMessages[0].Should().BeOfType<UserChatMessage>();

        var userMessage = result.ChatMessages[0].Content.AsJson();
        userMessage.Should().NotBeNull();
        userMessage.Should().HavePropertyWithStringValue("InputType", "Instruction");
        userMessage.Should().HavePropertyWithStringValue("Text", Instruction.EndLazyModeAoi);
    }

    #endregion

    #region AddToolMessage テスト

    [Fact]
    public void AddToolMessage_ツールメッセージを追加できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState();
        var toolCallId = "call_abc123";
        var result = "処理が完了しました";

        // Act
        var newState = state.AddToolMessage(toolCallId, result);

        // Assert
        newState.ChatMessages.Should().HaveCount(1);
        newState.ChatMessages[0].Should().BeOfType<ToolChatMessage>();

        var toolMessage = newState.ChatMessages[0] as ToolChatMessage;
        toolMessage!.ToolCallId.Should().Be(toolCallId);
        toolMessage.Content.Should().NotBeEmpty();
        toolMessage.Content[0].Text.Should().Be(result);
    }

    #endregion

    #region SwitchToAnotherSister テスト

    [Fact]
    public void SwitchToAnotherSister_茜から葵に切り替わること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Akane);

        // Act
        var result = state.SwitchToAnotherSister();

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Aoi);
    }

    [Fact]
    public void SwitchToAnotherSister_葵から茜に切り替わること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Aoi);

        // Act
        var result = state.SwitchToAnotherSister();

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Akane);
    }

    #endregion

    #region SwitchToSister テスト

    [Fact]
    public void SwitchToSister_同じ姉妹の場合_何もしないこと()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Akane);
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

        // Act
        var result = state.SwitchToSister(Kotonoha.Akane, dateTime);

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Akane);
        result.ChatMessages.Should().BeEmpty();
    }

    [Fact]
    public void SwitchToSister_異なる姉妹の場合_切り替えること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Akane);
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);

        // Act
        var result = state.SwitchToSister(Kotonoha.Aoi, dateTime);

        // Assert
        result.CurrentSister.Should().Be(Kotonoha.Aoi);
        result.PatienceCount.Should().Be(0);
        result.ChatMessages.Should().HaveCount(1);

        var userMessage = result.ChatMessages[0].Content.AsJson();
        userMessage.Should().NotBeNull();
        userMessage.Should().HavePropertyWithStringValue("InputType", "Instruction");
        userMessage.Should().HavePropertyWithStringValue("Text", Instruction.SwitchSisterTo(Kotonoha.Aoi));
    }

    #endregion

    #region RecordToolCall テスト

    [Fact]
    public void RecordToolCall_初回のツール呼び出しを記録できること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Akane);

        // Act
        var result = state.RecordToolCall();

        // Assert
        result.PatienceCount.Should().Be(1);
        result.LastToolCallSister.Should().Be(Kotonoha.Akane);
    }

    [Fact]
    public void RecordToolCall_同じ姉妹の連続呼び出しでPatienceCountが増加すること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Akane) with
        {
            LastToolCallSister = Kotonoha.Akane,
            PatienceCount = 2
        };

        // Act
        var result = state.RecordToolCall();

        // Assert
        result.PatienceCount.Should().Be(3);
        result.LastToolCallSister.Should().Be(Kotonoha.Akane);
    }

    [Fact]
    public void RecordToolCall_異なる姉妹の呼び出しでPatienceCountがリセットされること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Aoi) with
        {
            LastToolCallSister = Kotonoha.Akane,
            PatienceCount = 5
        };

        // Act
        var result = state.RecordToolCall();

        // Assert
        result.PatienceCount.Should().Be(1);
        result.LastToolCallSister.Should().Be(Kotonoha.Aoi);
    }

    #endregion

    #region FullChatMessages テスト

    [Fact]
    public void FullChatMessages_茜の場合_茜のシステムメッセージが先頭に追加されること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Akane);
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);
        state = state.AddUserMessage("テスト", dateTime);

        // Act
        var fullMessages = state.FullChatMessages;

        // Assert
        fullMessages.Should().HaveCount(2);
        fullMessages[0].Should().BeOfType<SystemChatMessage>();

        var systemMessage = fullMessages[0] as SystemChatMessage;
        systemMessage!.Content.Should().NotBeEmpty();
        systemMessage.Content[0].Text.Should().Be("System message for Akane");
    }

    [Fact]
    public void FullChatMessages_葵の場合_葵のシステムメッセージが先頭に追加されること()
    {
        // Arrange
        var state = TestStateFactory.CreateTestState(Kotonoha.Aoi);
        var dateTime = new DateTime(2025, 1, 1, 12, 30, 0);
        state = state.AddUserMessage("テスト", dateTime);

        // Act
        var fullMessages = state.FullChatMessages;

        // Assert
        fullMessages.Should().HaveCount(2);
        fullMessages[0].Should().BeOfType<SystemChatMessage>();

        var systemMessage = fullMessages[0] as SystemChatMessage;
        systemMessage!.Content.Should().NotBeEmpty();
        systemMessage.Content[0].Text.Should().Be("System message for Aoi");
    }

    #endregion
}

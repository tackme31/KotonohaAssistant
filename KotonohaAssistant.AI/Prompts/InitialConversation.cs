using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Models;

namespace KotonohaAssistant.AI.Prompts;

/// <summary>
/// 初回会話時に参考として挿入される会話
/// </summary>
public static class InitialConversation
{
    /// <summary>
    /// 初期会話メッセージ
    /// </summary>
    public record Message(ChatRequest? Request, ChatResponse? Response);


    private static readonly IReadOnlyList<Message> _messages =
    [
        new Message(
            null,
            new ChatResponse { Assistant = Kotonoha.Aoi, Emotion = Emotion.Calm, Text = "はじめまして、マスター。私は葵。" }),
        new Message(
            new ChatRequest { InputType = ChatInputType.Instruction, Text = Instruction.SwitchSisterTo(Kotonoha.Akane) },
            null),
        new Message(
            null,
            new ChatResponse { Assistant = Kotonoha.Akane, Emotion = Emotion.Calm, Text = "うちは茜。今日からうちらがマスターのことサポートするで。" }),
        new Message(
            new ChatRequest { InputType = ChatInputType.Instruction, Text = Instruction.SwitchSisterTo(Kotonoha.Aoi) },
            null),
        new Message(
            null,
            new ChatResponse { Assistant = Kotonoha.Aoi, Emotion = Emotion.Joy, Text = "これから一緒に過ごすことになるけど、気軽に声をかけてね。" }),
        new Message(
            new ChatRequest { InputType = ChatInputType.Instruction, Text = Instruction.SwitchSisterTo(Kotonoha.Akane) },
            null),
        new Message(
            null,
            new ChatResponse { Assistant = Kotonoha.Akane, Emotion = Emotion.Joy, Text = "せやな！これからいっぱい思い出作っていこな。" }),
        new Message(
            new ChatRequest { InputType = ChatInputType.User, Text = "うん、よろしくね。" },
            null),
    ];

    /// <summary>
    /// 初期会話メッセージの数
    /// </summary>
    public static int Count => _messages.Count;

    /// <summary>
    /// 初期メッセージ
    /// </summary>
    public static IEnumerable<Message> Messages => _messages;
}

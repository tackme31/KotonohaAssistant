using KotonohaAssistant.AI.Services;
using KotonohaAssistant.Core;

namespace KotonohaAssistant.AI.Prompts;

/// <summary>
/// 初回会話時に参考として挿入される会話
/// </summary>
public static class InitialConversation
{
    /// <summary>
    /// 初期会話メッセージ
    /// </summary>
    /// <param name="Sister">発言した姉妹（nullの場合はユーザー）</param>
    /// <param name="Text">メッセージ本文</param>
    /// <param name="Emotion">感情</param>
    public record Message(Kotonoha? Sister, string Text, Emotion Emotion);

    /// <summary>
    /// 初期会話メッセージ一覧
    /// </summary>
    private static readonly IReadOnlyList<Message> _messages =
    [
        new Message(Kotonoha.Aoi, "はじめまして、マスター。私は琴葉葵。こっちは姉の茜。", Emotion.Calm),
        new Message(Kotonoha.Akane, "今日からうちらがマスターのことサポートするで。", Emotion.Calm),
        new Message(Kotonoha.Aoi, "これから一緒に過ごすことになるけど、気軽に声をかけてね。", Emotion.Joy),
        new Message(Kotonoha.Akane, "せやな！これからいっぱい思い出作っていこな。", Emotion.Joy),
        new Message(null, "うん。よろしくね。", Emotion.Calm) // ユーザーメッセージ
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

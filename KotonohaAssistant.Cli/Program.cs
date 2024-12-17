using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using OpenAI.Chat;
using KotonohaAssistant.Functions;
using System.Linq;
using System.ClientModel;
using System.Text.RegularExpressions;
using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant;

internal class Program
{
    private static readonly Random R = new();

    // 呼び出し可能な関数の一覧
    private static readonly Dictionary<string, ToolFunction> Functions = new()
    {
        [nameof(SetAlarm)] = new SetAlarm(),
        [nameof(StartTimer)] = new StartTimer(),
        [nameof(CreateCalendarEvent)] = new CreateCalendarEvent(),
        [nameof(GetCalendarEvent)] = new GetCalendarEvent(),
        [nameof(GetWeather)] = new GetWeather(),
        [nameof(TurnOnHeater)] = new TurnOnHeater(),
        [nameof(ForgetMemory)] = new ForgetMemory(),
    };

    // 怠け癖の対象外の関数
    private static readonly HashSet<string> ForceCalledFunctions = new()
    {
        nameof(StartTimer),
        nameof(ForgetMemory)
    };

    static async Task Main(string[] args)
    {
        var (chatClient, options) = Setup();

        using (var voiceClient = new VoiceClient())
        {
            try
            {
                await StartConversationAsync(voiceClient, chatClient, options);
            }
            catch (Exception e)
            {
                await voiceClient.SpeakAsync(Kotonoha.Akane, $"おっ、エラーやな。えっと、「{e.Message}」らしいで。");
                await Task.Delay(750);
                await voiceClient.SpeakAsync(Kotonoha.Aoi, $"詳しくはログに書いておいたよ。マスター、早く直してね。");

                // TODO: ログ出力
            }
        }
    }

    private static bool ShouldBeLazy(ChatCompletion completionValue, int againCounter)
    {
        // 関数呼び出し以外は怠けない
        if (completionValue.FinishReason != ChatFinishReason.ToolCalls)
        {
            return false;
        }

        // 怠け癖対象外の関数なら怠けない
        if (completionValue.ToolCalls.Any(toolCall => ForceCalledFunctions.Contains(toolCall.FunctionName)))
        {
            return false;
        }

        // 4回以上同じ方にお願いすると怠ける
        if (againCounter > 3)
        {
            return true;
        }

        // 1/10の確率で怠け癖発動
        var lazy = R.NextDouble() < 1d / 10d;
        return lazy;
    }

    private static (ChatClient client, ChatCompletionOptions options) Setup()
    {
        DotNetEnv.Env.TraversePath().Load();

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var client = new ChatClient("gpt-4o-mini", apiKey);
        var options = new ChatCompletionOptions();

        foreach (var function in Functions.Values)
        {
            options.Tools.Add(function.CreateChatTool());
        }

        return (client, options);
    }

    private static async Task StartConversationAsync(VoiceClient voiceClient, ChatClient chatClient, ChatCompletionOptions options)
    {
        var manager = new ChatMessageManager(Kotonoha.Akane);
        manager.AddAssistantMessage("葵: はじめまして、マスター。私は琴葉葵。こっちは姉の茜。");
        manager.AddAssistantMessage("茜: 今日からうちらがマスターのことサポートするで。");
        manager.AddUserMessage("私: うん。よろしくね。");
        manager.AddUserMessage("======= LazyMode: OFF =======");

        // 同じ方に連続してお願いすると怠ける
        var againCounter = 0;
        var prevSister = Kotonoha.Akane;

        while (true)
        {
            Console.Write("私: ");
            var stdin = Console.ReadLine();
            if (string.IsNullOrEmpty(stdin))
            {
                continue;
            }

            if (manager.CurrentSister == Kotonoha.Aoi &&
                (stdin.Contains("茜") || stdin.Contains("あかね")))
            {
                manager.CurrentSister = Kotonoha.Akane;
                manager.AddUserMessage($"[Hint]: 姉妹が切り替わりました(葵 => 茜)");
            }
            if (manager.CurrentSister == Kotonoha.Akane &&
                (stdin.Contains("葵") || stdin.Contains("あおい")))
            {
                manager.CurrentSister = Kotonoha.Aoi;
                manager.AddUserMessage($"[Hint]: 姉妹が切り替わりました(茜 => 葵)");
            }

            manager.AddUserMessage($"私: {stdin}");

            var completion = await chatClient.CompleteChatAsync(manager.ChatMessages, options);

            // 連続してお願いした回数をカウント
            if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                if (prevSister == manager.CurrentSister)
                {
                    againCounter++;
                }
                else
                {
                    againCounter = 1;
                    prevSister = manager.CurrentSister;
                }
            }

            // 怠け癖発動
            if (ShouldBeLazy(completion.Value, againCounter))
            {
                completion = await PassTaskToAnotherSisterAsync(chatClient, options, manager, voiceClient);

                // 怠けると姉妹が入れ替わるのでカウンターをリセット
                againCounter = 1;
            }

            manager.AddAssistantMessage(completion.Value);

            // Function calling
            // gpt-4o-miniはparallelに対応していなさそうなので逐次処理
            while (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                foreach (var toolCall in completion.Value.ToolCalls)
                {
                    using var arguments = JsonDocument.Parse(toolCall.FunctionArguments);
                    if (!Functions.TryGetValue(toolCall.FunctionName, out var function) || function is null)
                    {
                        continue;
                    }

                    var result = function.Invoke(arguments);
                    manager.AddToolMessage(toolCall.Id, result);
                }

                completion = await chatClient.CompleteChatAsync(manager.ChatMessages, options);
                manager.AddAssistantMessage(completion.Value);
            }

            await SpeakCompletionAsync(completion, manager.CurrentSister, voiceClient);
        }
    }

    private static async Task<ClientResult<ChatCompletion>> PassTaskToAnotherSisterAsync(ChatClient client, ChatCompletionOptions options, ChatMessageManager manager, VoiceClient voiceClient)
    {
        // 怠け者モードをONにして、再度呼び出し。
        manager.AddUserMessage("[Hint]: LazyMode=ON: 以降、関数を呼び出さないでください。");
        var completion = await client.CompleteChatAsync(manager.ChatMessages, options);

        // それでも関数呼び出しされることがあるのでチェック
        if (completion.Value.FinishReason != ChatFinishReason.Stop)
        {
            // 怠け者モードをOFF
            manager.AddUserMessage("[Hint]: LazyMode=OFF: 以降、通常通り関数を呼び出してください。");

            return completion;
        }


        manager.AddAssistantMessage(completion.Value);

        await SpeakCompletionAsync(completion, manager.CurrentSister, voiceClient);

        // 怠け者モードをOFF
        manager.AddUserMessage("[Hint]: LazyMode=OFF: 以降、通常通り関数を呼び出してください。");

        // 姉妹を切り替えて、再度呼び出し
        var nextSister = manager.CurrentSister switch
        {
            Kotonoha.Akane => Kotonoha.Aoi,
            Kotonoha.Aoi => Kotonoha.Akane,
            _ => manager.CurrentSister
        };

        var prev = manager.CurrentSister switch
        {
            Kotonoha.Akane => "茜",
            Kotonoha.Aoi => "葵",
            _ => string.Empty
        };
        var next = nextSister switch
        {
            Kotonoha.Akane => "茜",
            Kotonoha.Aoi => "葵",
            _ => string.Empty
        };
        manager.AddUserMessage($"[Hint]: 姉妹が切り替わりました({prev} => {next})");

        manager.CurrentSister = nextSister;
        return await client.CompleteChatAsync(manager.ChatMessages, options);
    }

    private static Task SpeakCompletionAsync(ClientResult<ChatCompletion> completion, Kotonoha sister, VoiceClient voiceClient)
    {
        var message = completion.Value.Content[0].Text;
        Console.WriteLine(message);

        var messageWithoutName = Regex.Replace(message, @"^(茜|葵):", string.Empty);
        return voiceClient.SpeakAsync(sister, messageWithoutName);
    }
}

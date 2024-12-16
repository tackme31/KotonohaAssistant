using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using OpenAI.Chat;
using KotonohaAssistant.Functions;
using KotonohaAssistant.Utils;

namespace KotonohaAssistant;

internal class Program
{
    private static readonly Random R = new();
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

    static async Task Main(string[] args)
    {
        var (client, options) = Setup();

        await StartConversation(client, options);
    }

    private static bool ShouldBeLazy() => R.NextDouble() < 1d/10d; // 1/10の確率で怠け癖発動

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

    private static async Task StartConversation(ChatClient client, ChatCompletionOptions options)
    {
        var editorController = new EditorController();
        editorController.InitializeHost();

        var manager = new ChatMessageManager(SisterType.KotonohaAkane);
        manager.AddAssistantMessage("[葵] はじめまして、マスター。私は琴葉葵。こっちは姉の茜。");
        manager.AddAssistantMessage("[茜] 今日からうちらがマスターのことサポートするで。");
        manager.AddUserMessage("[私] うん。よろしくね。");
        manager.AddUserMessage("======= LazyMode: OFF =======");

        while (true)
        {
            Console.Write("[私] ");
            var stdin = Console.ReadLine();
            if (string.IsNullOrEmpty(stdin))
            {
                continue;
            }

            if (stdin.Contains("茜") || stdin.Contains("あかね"))
            {
                manager.CurrentSister = SisterType.KotonohaAkane;
            }
            if (stdin.Contains("葵") || stdin.Contains("あおい"))
            {
                manager.CurrentSister = SisterType.KotonohaAoi;
            }

            manager.AddUserMessage("[私] " + stdin);

            var completion = await client.CompleteChatAsync(manager.ChatMessages, options);
            // 怠け癖発動
            if (ShouldBeLazy() && completion.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                Console.WriteLine("怠け癖発動");

                // 怠け者モードをONにして、再度呼び出し。
                manager.AddUserMessage("[System] LazyMode=ON: 以降、関数を呼び出さないでください。");
                completion = await client.CompleteChatAsync(manager.ChatMessages, options);

                // それでも関数呼び出しされることがあるのでチェック
                if (completion.Value.FinishReason != ChatFinishReason.Stop)
                {
                    // 怠け者モードをOFF
                    manager.AddUserMessage("[System] LazyMode=OFF: 以降、通常通り関数を呼び出してください。");
                }
                else
                {
                    manager.AddAssistantMessage(completion.Value);

                    Console.WriteLine(completion.Value.Content[0].Text);
                    await editorController.SpeakAsync(manager.CurrentSister, completion.Value.Content[0].Text.Replace("[茜]", "").Replace("[葵]", ""));


                    // 怠け者モードをOFF
                    manager.AddUserMessage("[System] LazyMode=OFF: 以降、通常通り関数を呼び出してください。");

                    // 姉妹を切り替えて、再度呼び出し
                    manager.CurrentSister = manager.CurrentSister switch
                    {
                        SisterType.KotonohaAkane => SisterType.KotonohaAoi,
                        SisterType.KotonohaAoi => SisterType.KotonohaAkane,
                        _ => manager.CurrentSister
                    };
                    completion = await client.CompleteChatAsync(manager.ChatMessages, options);

                }
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

                completion = await client.CompleteChatAsync(manager.ChatMessages, options);
                manager.AddAssistantMessage(completion.Value);
            }

            Console.WriteLine(completion.Value.Content[0].Text);
            await editorController.SpeakAsync(manager.CurrentSister, completion.Value.Content[0].Text.Replace("[茜]", "").Replace("[葵]", ""));
        }
    }
}

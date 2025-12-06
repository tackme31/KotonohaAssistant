using KotonohaAssistant.Core;
using KotonohaAssistant.Core.Utils;

namespace KotonohaAssistant.AI.Services;

public interface ISisterSwitchingService
{
    /// <summary>
    /// ユーザー入力を解析し、必要に応じて姉妹を切り替えます
    /// </summary>
    /// <param name="userInput">ユーザーの入力テキスト</param>
    /// <param name="state">会話の状態</param>
    /// <returns>姉妹が切り替わった場合はtrue</returns>
    bool TrySwitchSister(string userInput, ConversationState state);
}

public class SisterSwitchingService : ISisterSwitchingService
{
    private const string LogPrefix = "[SisterSwitch]";

    private readonly ILogger _logger;

    public SisterSwitchingService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// ユーザー入力を解析し、必要に応じて姉妹を切り替えます
    /// </summary>
    /// <param name="userInput">ユーザーの入力テキスト</param>
    /// <param name="state">会話の状態</param>
    /// <returns>姉妹が切り替わった場合はtrue</returns>
    public bool TrySwitchSister(string userInput, ConversationState state)
    {
        var nextSister = GuessTargetSister(userInput);
        if (nextSister == null || nextSister == state.CurrentSister)
        {
            return false;
        }

        _logger.LogInformation($"{LogPrefix} Sister switch detected: {state.CurrentSister} -> {nextSister.Value}");
        state.SwitchToSister(nextSister.Value);
        return true;
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

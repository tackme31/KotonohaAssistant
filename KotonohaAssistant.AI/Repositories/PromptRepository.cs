using KotonohaAssistant.AI.Prompts;
using KotonohaAssistant.Core;
namespace KotonohaAssistant.AI.Repositories;

public interface IPromptRepository
{
    string GetCharacterPrompt(Kotonoha sister);
    string MakeTimeBasedPromise { get; }
    string CreateCalendarEventDescription { get; }
    string ForgetMemoryDescription { get; }
    string GetCalendarEventDescription { get; }
    string GetWeatherDescription { get; }
    string StartTimerDescription { get; }
    string StopAlarmDescription { get; }
    string StopTimerDescription { get; }
    string InactiveNotification { get; }
}

public class PromptRepository(string promptPath) : IPromptRepository
{
    private static readonly string CharacterPromptAkane = "system_akane.md";
    private static readonly string CharacterPromptAoi = "system_aoi.md";
    private static readonly string ToolMakeTimeBasedPromise = "tool_make_time_based_promise.md";
    private static readonly string ToolCreateCalendarEvent = "tool_create_calendar_event.md";
    private static readonly string ToolForgetMemory = "tool_forget_memory.md";
    private static readonly string ToolGetCalendarEvent = "tool_get_calendar_event.md";
    private static readonly string ToolGetWeather = "tool_get_weather.md";
    private static readonly string ToolStartTimer = "tool_start_timer.md";
    private static readonly string ToolStopAlarm = "tool_stop_alarm.md";
    private static readonly string ToolStopTimer = "tool_stop_timer.md";
    private static readonly string InstructionInactiveNotification = "instruction_inactive_notification.md";

    public string GetCharacterPrompt(Kotonoha sister) => sister switch
    {
        Kotonoha.Akane => SystemMessage.KotonohaAkane(GetPrompt(CharacterPromptAkane)),
        Kotonoha.Aoi => SystemMessage.KotonohaAoi(GetPrompt(CharacterPromptAoi)),
        _ => throw new FileNotFoundException()
    };

    public string MakeTimeBasedPromise => GetPrompt(ToolMakeTimeBasedPromise);

    public string CreateCalendarEventDescription => GetPrompt(ToolCreateCalendarEvent);

    public string ForgetMemoryDescription => GetPrompt(ToolForgetMemory);

    public string GetCalendarEventDescription => GetPrompt(ToolGetCalendarEvent);

    public string GetWeatherDescription => GetPrompt(ToolGetWeather);

    public string StartTimerDescription => GetPrompt(ToolStartTimer);

    public string StopAlarmDescription => GetPrompt(ToolStopAlarm);

    public string StopTimerDescription => GetPrompt(ToolStopTimer);

    public string InactiveNotification => GetPrompt(InstructionInactiveNotification);

    private string GetPrompt(string fileName) => File.ReadAllText(Path.Combine(promptPath, fileName));
}

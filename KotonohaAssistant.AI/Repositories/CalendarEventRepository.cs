using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;

namespace KotonohaAssistant.AI.Repositories;

public interface ICalendarEventRepository
{
    Task<IList<Event>> GetEventsAsync(DateTime date);
    Task<Event> CreateEventAsync(string title, DateTime date, TimeSpan? time = null);
}

public class CalendarEventRepository : ICalendarEventRepository
{
    private readonly string _calendarId;
    private readonly CalendarService _calendarService;

    private static readonly string[] Scopes = {
        CalendarService.Scope.Calendar
    };

    public CalendarEventRepository(string credentialsFilePath, string calendarId = "primary")
    {
        _calendarId = calendarId;

        var credential = GoogleCredential
            .FromFile(credentialsFilePath)
            .CreateScoped(Scopes);

        _calendarService = new CalendarService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Kotonoha Assistant",
        });
    }

    public async Task<IList<Event>> GetEventsAsync(DateTime date)
    {
        var timeZone = TimeZoneInfo.Local;
        var start = TimeZoneInfo.ConvertTimeToUtc(date.Date, timeZone);
        var end = TimeZoneInfo.ConvertTimeToUtc(date.Date.AddDays(1), timeZone);

        var request = _calendarService.Events.List(_calendarId);
        request.TimeMinDateTimeOffset = start;
        request.TimeMaxDateTimeOffset = end;
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var result = await request.ExecuteAsync();
        return result.Items;
    }

    public async Task<Event> CreateEventAsync(string title, DateTime date, TimeSpan? time = null)
    {
        var start = time is null
            ? new EventDateTime
            {
                Date = date.ToString("yyyy-MM-dd")
            }
            : new EventDateTime
            {
                DateTimeDateTimeOffset = date.Date + time.Value,
                //TimeZone = TimeZoneInfo.Local.Id
            };
        var end = time is null
            ? new EventDateTime
            {
                Date = date.AddDays(1).ToString("yyyy-MM-dd")
            }
            : new EventDateTime
            {
                DateTimeDateTimeOffset = date.Date + time.Value,
                //TimeZone = TimeZoneInfo.Local.Id
            };
        // 新しいイベントを作成
        var newEvent = new Event
        {
            Summary = title,
            Start = start,
            End = end,
        };

        // Googleカレンダーにイベントを挿入
        var request = _calendarService.Events.Insert(newEvent, _calendarId);
        var createdEvent = await request.ExecuteAsync();
        return createdEvent;
    }
}

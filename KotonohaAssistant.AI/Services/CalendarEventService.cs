using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;

namespace KotonohaAssistant.AI.Services;

public interface ICalendarEventService
{
    Task<IList<Event>> GetEventsAsync(DateTime date);
}

public class CalendarEventService : ICalendarEventService
{
    private readonly string _calendarId;
    private readonly CalendarService _calendarService;

    private static readonly string[] Scopes = {
        CalendarService.Scope.Calendar
    };

    public CalendarEventService(string credentialsFilePath, string calendarId = "primary")
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
        var start = date.Date;
        var end = date.Date.AddDays(1).AddSeconds(-1);

        var request = _calendarService.Events.List(_calendarId);
        request.TimeMinDateTimeOffset = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start));
        request.TimeMaxDateTimeOffset = new DateTimeOffset(end, TimeZoneInfo.Local.GetUtcOffset(end)); 
        request.ShowDeleted = false;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var result = await request.ExecuteAsync();
        return result.Items;
    }
}

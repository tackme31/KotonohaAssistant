using KotonohaAssistant.Alarm;

using var service = new AlarmService();

await service.Start();

Console.WriteLine("Press enter to exit...");
Console.ReadKey();
using KotonohaAssistant.Alarm;
var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kotonoha Assistant");
var dbPath = Path.Combine(appFolder, "alarm.db");

var repository = new AlarmRepository(dbPath);
using var service = new AlarmService(repository);

service.Start();

Console.WriteLine("Press enter to exit...");
Console.ReadKey();
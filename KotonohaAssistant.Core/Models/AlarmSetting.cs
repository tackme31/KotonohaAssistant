using System;

namespace KotonohaAssistant.Core.Models;

public class AlarmSetting
{
    public double TimeInSeconds { get; set; }
    public bool Repeat { get; set; }
    public Kotonoha Sister { get; set; }
    public string? Message { get; set; }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace KotonohaAssistant.Core.Models;

public class AddAlarmRequest
{
    public double TimeInSeconds { get; set; }
    public string? VoicePath { get; set; }
    public bool IsRepeated { get; set; }
}

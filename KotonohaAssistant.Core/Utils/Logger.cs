using System;
using System.IO;

namespace KotonohaAssistant.Core.Utils;

public enum LogLevel
{
    Information,
    Warning,
    Error
}

public interface ILogger
{
    void LogInformation(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(Exception exception);
}

public class Logger(string filePath, bool isConsoleLoggingEnabled = true) : ILogger
{
    private readonly string _filePath = filePath;
    private readonly bool _isConsoleLoggingEnabled = isConsoleLoggingEnabled;

    private void Log(LogLevel level, string message)
    {
        var logMessage = $"{DateTime.Now} [{level}] {message}";

        // コンソールに出力（オプション）
        if (_isConsoleLoggingEnabled)
        {
            Console.WriteLine(logMessage);
        }

        // ファイルに出力
        try
        {
            File.AppendAllText(_filePath, logMessage + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ログファイルへの書き込みに失敗しました: {ex.Message}");
        }
    }

    public void LogInformation(string message)
    {
        Log(LogLevel.Information, message);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    public void LogError(string message)
    {
        Log(LogLevel.Error, message);
    }

    public void LogError(Exception exception)
    {
        Log(LogLevel.Error, exception.ToString());
    }
}

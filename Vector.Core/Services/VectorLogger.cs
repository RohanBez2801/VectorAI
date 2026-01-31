using System;
using System.IO;
using System.Text.Json;

namespace Vector.Core.Services;

public interface IVectorLogger
{
    void LogDecision(string type, string input, string decision, string reason);
    void LogPlan(string userGoal, string plan);
    void LogReflection(float score, string analysis);
    void LogVerification(string status, string details);
    void LogError(string context, string error);
}

public class VectorLogger : IVectorLogger
{
    private readonly string _logDir;
    private readonly string _currentLogFile;

    public VectorLogger()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _logDir = Path.Combine(appData, "VectorAI", "logs");
        if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir);

        _currentLogFile = Path.Combine(_logDir, $"vector_{DateTime.Now:yyyy-MM-dd}.jsonl");
    }

    public void LogDecision(string type, string input, string decision, string reason)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            eventType = "DECISION",
            type,
            input = Truncate(input, 200),
            decision,
            reason
        };
        WriteLog(entry);
    }

    public void LogPlan(string userGoal, string plan)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            eventType = "PLAN",
            userGoal = Truncate(userGoal, 200),
            plan = Truncate(plan, 500)
        };
        WriteLog(entry);
    }

    public void LogReflection(float score, string analysis)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            eventType = "REFLECTION",
            score,
            analysis = Truncate(analysis, 300)
        };
        WriteLog(entry);
    }

    public void LogVerification(string status, string details)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            eventType = "VERIFICATION",
            status,
            details = Truncate(details, 500)
        };
        WriteLog(entry);
    }

    public void LogError(string context, string error)
    {
        var entry = new
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            eventType = "ERROR",
            context,
            error = Truncate(error, 500)
        };
        WriteLog(entry);
    }

    private void WriteLog(object entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry);
            File.AppendAllText(_currentLogFile, json + Environment.NewLine);
        }
        catch { /* Best effort logging */ }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}

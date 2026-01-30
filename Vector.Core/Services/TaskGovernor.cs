using System.Collections.Generic;

namespace Vector.Core.Services;

public class TaskGovernor : ITaskGovernor
{
    private readonly List<string> _history = new();
    private const int MaxRepetitions = 3;

    public ApprovalStatus ValidateAction(string toolName, string input)
    {
        // 1. Loop Detection
        if (IsLooping(toolName, input))
        {
            return ApprovalStatus.Denied;
        }

        // 2. Blacklist / Safety (Simple example)
        if (toolName == "Shell" && (input.Contains("rm -rf /") || input.Contains("format c:")))
        {
            return ApprovalStatus.Denied;
        }

        return ApprovalStatus.Approved;
    }

    public void RecordAction(string toolName, string input)
    {
        _history.Add($"{toolName}:{input}");
    }

    public void Reset()
    {
        _history.Clear();
    }

    private bool IsLooping(string toolName, string input)
    {
        string signature = $"{toolName}:{input}";
        int count = 0;
        
        // check last N items
        for (int i = _history.Count - 1; i >= 0; i--)
        {
            if (_history[i] == signature)
            {
                count++;
            }
            if (count >= MaxRepetitions) return true;
        }
        return false;
    }
}

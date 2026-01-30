using System.Collections.Generic;

namespace Vector.Core.Models;

public class ReflectionContext
{
    public string UserGoal { get; set; } = string.Empty;
    public string RecentHistory { get; set; } = string.Empty; // Serialized chat history of the turn
    public bool WasToolUsed { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ToolOutput { get; set; } = string.Empty;
}

public class ReflectionResult
{
    public float SuccessScore { get; set; } // 0.0 to 1.0
    public string Analysis { get; set; } = string.Empty;
    public string ProposedAction { get; set; } = "None"; // None, Retry, Escalate, StoreMemory
    public string Learnings { get; set; } = string.Empty; // What to write to memory if applicable
}

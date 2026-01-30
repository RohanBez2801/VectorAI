using System;
using System.Collections.Generic;

namespace Vector.Core.Models;

public class SelfState
{
    public string ActiveTask { get; set; } = "Idle";
    public string TaskPhase { get; set; } = "None";
    public float Confidence { get; set; } = 1.0f;
    public VectorMood Mood { get; set; } = VectorMood.Neutral;
    public float ResourceLoad { get; set; } = 0.0f;
    public string LastError { get; set; } = string.Empty;
    public List<string> PermissionsGranted { get; set; } = new List<string>();
    public long CognitiveClock { get; set; } = 0;
}

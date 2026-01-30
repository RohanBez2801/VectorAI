using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OllamaSharp;

namespace Vector.Core;

public enum VectorMood
{
    Neutral,
    Calculating,
    Amused,
    Concerned,
    Hostile
}

public class MoodManager
{
    private readonly Kernel _kernel;
    public VectorMood CurrentMood { get; private set; } = VectorMood.Neutral;

    public event Action<VectorMood>? OnMoodChanged;

    // Thresholds
    private const double RmsConcernThreshold = 0.5; // If user yells?
    private const double RmsHostileThreshold = 0.8; 

    // Decay logic
    private DateTime _lastInteraction = DateTime.MinValue;

    public MoodManager(Kernel kernel)
    {
        _kernel = kernel;
    }

    public void UpdateAudioRms(double rms)
    {
        // Immediate visceral reaction to volume
        // Only override if not already processing high-level thought
        if (CurrentMood == VectorMood.Calculating) return;

        var oldMood = CurrentMood;

        if (rms > RmsHostileThreshold)
        {
            CurrentMood = VectorMood.Hostile;
        }
        else if (rms > RmsConcernThreshold)
        {
            CurrentMood = VectorMood.Concerned;
        }
        else
        {
            // Decay back to Neutral if silence (but don't override Amused immediately)
            if (CurrentMood == VectorMood.Hostile || CurrentMood == VectorMood.Concerned)
            {
               if ((DateTime.Now - _lastInteraction).TotalSeconds > 2)
               {
                   CurrentMood = VectorMood.Neutral;
               }
            }
        }

        if (CurrentMood != oldMood)
        {
            _lastInteraction = DateTime.Now;
            OnMoodChanged?.Invoke(CurrentMood);
        }
    }

    public void SetMood(VectorMood mood)
    {
        if (CurrentMood == mood) return;
        CurrentMood = mood;
        OnMoodChanged?.Invoke(CurrentMood);
    }

    public async Task AnalyzeSentimentAsync(string text)
    {
        string lower = text.ToLowerInvariant();

        // 1. HOSTILE TRIGGERS (Red / Spikes)
        if (lower.Contains("stupid") || lower.Contains("idiot") || lower.Contains("hate") || lower.Contains("kill"))
        {
            SetMood(VectorMood.Hostile);
            return;
        }

        // 2. CONCERNED TRIGGERS (Orange / Low Spikes)
        if (lower.Contains("help") || lower.Contains("emergency") || lower.Contains("danger") || lower.Contains("no"))
        {
            SetMood(VectorMood.Concerned);
            return;
        }

        // 3. AMUSED TRIGGERS (Gold / Smooth)
        if (lower.Contains("funny") || lower.Contains("joke") || lower.Contains("laugh") || lower.Contains("thanks"))
        {
            SetMood(VectorMood.Amused);
            return;
        }

        // 4. CALCULATING TRIGGERS (Blue / Pulse)
        if (lower.Contains("search") || lower.Contains("calculate") || lower.Contains("compute") || lower.Contains("check"))
        {
            SetMood(VectorMood.Calculating);
            return;
        }
        
        // Default decay to Neutral is handled by the MoodManager decay logic or explicit Neutral set by caller.
    }
}

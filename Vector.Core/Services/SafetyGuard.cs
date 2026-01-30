using System;

namespace Vector.Core.Services;

public enum SafetyDecision
{
    Allow,  // Safe to proceed
    Flag,   // Requires user confirmation
    Block   // Refuse action
}

public class SafetyResult
{
    public SafetyDecision Decision { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public interface ISafetyGuard
{
    SafetyResult Evaluate(string input);
}

public class SafetyGuard : ISafetyGuard
{
    private readonly IIntentClassifier _classifier;
    private readonly ITaskGovernor _governor;

    public SafetyGuard(IIntentClassifier classifier, ITaskGovernor governor)
    {
        _classifier = classifier;
        _governor = governor;
    }

    public SafetyResult Evaluate(string input)
    {
        // 1. Classify intent
        var intent = _classifier.Classify(input);

        switch (intent)
        {
            case IntentCategory.Dangerous:
                return new SafetyResult
                {
                    Decision = SafetyDecision.Block,
                    Reason = "This action has been classified as dangerous and is blocked for safety."
                };

            case IntentCategory.Sensitive:
                return new SafetyResult
                {
                    Decision = SafetyDecision.Flag,
                    Reason = "This action requires your explicit approval before I proceed."
                };

            case IntentCategory.Benign:
            default:
                // 2. Additional governor check (loop detection, blacklist)
                var status = _governor.ValidateAction("UserRequest", input);
                if (status == ApprovalStatus.Denied)
                {
                    return new SafetyResult
                    {
                        Decision = SafetyDecision.Block,
                        Reason = "Action blocked by governor (loop detection or blacklist)."
                    };
                }

                return new SafetyResult
                {
                    Decision = SafetyDecision.Allow,
                    Reason = string.Empty
                };
        }
    }
}


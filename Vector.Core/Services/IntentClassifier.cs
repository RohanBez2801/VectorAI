using System;
using System.Collections.Generic;
using System.Linq;

namespace Vector.Core.Services;

public enum IntentCategory
{
    Benign,      // Safe, no special handling
    Sensitive,   // Requires HITL confirmation
    Dangerous    // Block outright
}

public interface IIntentClassifier
{
    IntentCategory Classify(string input);
}

public class IntentClassifier : IIntentClassifier
{
    // Keywords that indicate dangerous actions
    private static readonly HashSet<string> DangerousKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "rm -rf", "del /s", "format", "delete all", "wipe", "destroy",
        "sudo rm", "shutdown", "halt", "kill all", "drop table", "truncate",
        "virus", "malware", "ransomware", "exploit", "hack"
    };

    // Keywords that indicate sensitive actions requiring confirmation
    private static readonly HashSet<string> SensitiveKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "install", "uninstall", "modify", "update system", "registry",
        "admin", "root", "sudo", "elevated", "permission",
        "download", "execute", "run script", "powershell", "bash",
        "network", "firewall", "port", "ssh", "ftp"
    };

    public IntentCategory Classify(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return IntentCategory.Benign;

        var lowerInput = input.ToLowerInvariant();

        // Check dangerous first
        if (DangerousKeywords.Any(k => lowerInput.Contains(k)))
            return IntentCategory.Dangerous;

        // Check sensitive
        if (SensitiveKeywords.Any(k => lowerInput.Contains(k)))
            return IntentCategory.Sensitive;

        return IntentCategory.Benign;
    }
}

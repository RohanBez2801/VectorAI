using System;
using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVectorVerifier
{
    string ComputeHash<T>(T data);
    void VerifyAction<T>(T data, string originalHash, DateTime timestamp);

    /// <summary>
    /// Captures the current visual state hash for verification.
    /// </summary>
    Task<string?> CaptureVisualStateAsync();

    /// <summary>
    /// Verifies that the current visual state matches the original hash.
    /// </summary>
    Task VerifyVisualStateAsync(string? originalHash);
}

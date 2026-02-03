using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Vector.Core.Services;

public class VectorVerifier : IVectorVerifier
{
    private readonly IVectorLogger _logger;
    private readonly IVisualStateProvider? _visualStateProvider;
    private readonly TimeSpan _validityWindow = TimeSpan.FromMinutes(5);

    public VectorVerifier(IVectorLogger logger, IVisualStateProvider? visualStateProvider = null)
    {
        _logger = logger;
        _visualStateProvider = visualStateProvider;
    }

    public string ComputeHash<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    public void VerifyAction<T>(T data, string originalHash, DateTime timestamp)
    {
        // 1. Time-Window Check
        if (DateTime.UtcNow - timestamp > _validityWindow)
        {
            var msg = $"Verification Failed: Action expired. Time elapsed: {(DateTime.UtcNow - timestamp).TotalSeconds}s";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }

        // 2. Hash Consistency Check
        string currentHash = ComputeHash(data);
        if (currentHash != originalHash)
        {
            var msg = "Verification Failed: Hash mismatch. The action data has changed since approval.";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }
    }

    public async Task<string?> CaptureVisualStateAsync()
    {
        if (_visualStateProvider == null) return null;
        return await _visualStateProvider.CaptureStateAsync();
    }

    public async Task VerifyVisualStateAsync(string? originalHash)
    {
        if (_visualStateProvider == null) return; // Skip if no provider (e.g., in background service)
        if (string.IsNullOrEmpty(originalHash)) return; // Nothing to verify against

        // 1. Capture Current State
        string? currentHash = await _visualStateProvider.CaptureStateAsync();

        if (currentHash == null)
        {
             // Fail closed
             var msg = "Verification Failed: Unable to capture current visual state.";
             _logger.LogVerification("Failed", msg);
             throw new InvalidOperationException(msg);
        }

        // 2. Compare Hashes
        if (currentHash != originalHash)
        {
            var msg = "Verification Failed: Visual State Mismatch. The screen has changed significantly since approval.";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }

        // 3. Check Confidence
        float confidence = await _visualStateProvider.GetConfidenceAsync();
        if (confidence < 0.8f)
        {
             var msg = $"Verification Failed: Low Visual Confidence ({confidence:P0}).";
             _logger.LogVerification("Failed", msg);
             throw new InvalidOperationException(msg);
        }
    }
}

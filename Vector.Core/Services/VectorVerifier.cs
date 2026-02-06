using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVectorVerifier
{
    string ComputeHash<T>(T data);
    Task<string> ComputeVisualHashAsync();
    Task VerifyActionAsync<T>(T data, string originalHash, DateTime timestamp);
}

public class VectorVerifier : IVectorVerifier
{
    private readonly IVectorLogger _logger;
    private readonly IVisualStateProvider? _visualProvider;
    private readonly TimeSpan _validityWindow = TimeSpan.FromMinutes(5);

    public VectorVerifier(IVectorLogger logger, IVisualStateProvider? visualProvider = null)
    {
        _logger = logger;
        _visualProvider = visualProvider;
    }

    public string ComputeHash<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    public async Task<string> ComputeVisualHashAsync()
    {
        if (_visualProvider == null) return "NO_VISUAL_PROVIDER";
        return await _visualProvider.CaptureVisualStateHashAsync();
    }

    public async Task VerifyActionAsync<T>(T data, string originalHash, DateTime timestamp)
    {
        // 1. Time-Window Check
        if (DateTime.UtcNow - timestamp > _validityWindow)
        {
            var msg = $"Verification Failed: Action expired. Time elapsed: {(DateTime.UtcNow - timestamp).TotalSeconds}s";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }

        // 2. Composite Check
        // Expect originalHash format: "DATA_HASH|VISUAL_HASH"
        // If no pipe is found, treat as legacy/data-only hash.
        var parts = originalHash.Split('|');
        string originalDataHash = parts[0];
        string? originalVisualHash = parts.Length > 1 ? parts[1] : null;

        // Verify Data Hash
        string currentDataHash = ComputeHash(data);
        if (currentDataHash != originalDataHash)
        {
            var msg = "Verification Failed: Hash mismatch. The action data has changed since approval.";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }

        // Verify Visual Hash (if applicable)
        if (!string.IsNullOrEmpty(originalVisualHash) &&
            originalVisualHash != "NO_VISUAL_PROVIDER" &&
            !originalVisualHash.StartsWith("VISUAL_CAPTURE_FAILED"))
        {
            if (_visualProvider == null)
            {
                // We had a visual hash before, but now we don't have a provider?
                // This implies a configuration change or loss of service integrity.
                var msg = "Verification Failed: Visual provider unavailable but action was signed with visual context.";
                _logger.LogVerification("Failed", msg);
                throw new InvalidOperationException(msg);
            }

            string currentVisualHash = await _visualProvider.CaptureVisualStateHashAsync();
            if (currentVisualHash != originalVisualHash)
            {
                var msg = "Verification Failed: Visual context mismatch. Screen state changed significantly.";
                _logger.LogVerification("Failed", msg);
                throw new InvalidOperationException(msg);
            }
        }
    }
}

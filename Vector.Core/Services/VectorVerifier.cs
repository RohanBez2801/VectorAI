using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVectorVerifier
{
    Task<string> ComputeActionHashAsync<T>(T data);
    Task<string> ComputeVisualStateHashAsync();
    Task VerifyActionAsync<T>(T data, string originalActionHash, string originalVisualHash, DateTime timestamp);
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

    public Task<string> ComputeActionHashAsync<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Task.FromResult(Convert.ToHexString(bytes));
    }

    public async Task<string> ComputeVisualStateHashAsync()
    {
        if (_visualProvider == null) return "NO_VISUAL_PROVIDER";

        try
        {
            return await _visualProvider.CaptureVisualStateAsync();
        }
        catch (Exception ex)
        {
             _logger.LogVerification("Warning", $"Visual Capture Failed: {ex.Message}");
             return "VISUAL_CAPTURE_FAILED";
        }
    }

    public async Task VerifyActionAsync<T>(T data, string originalActionHash, string originalVisualHash, DateTime timestamp)
    {
        // 1. Time-Window Check
        if (DateTime.UtcNow - timestamp > _validityWindow)
        {
            var msg = $"Verification Failed: Action expired. Time elapsed: {(DateTime.UtcNow - timestamp).TotalSeconds}s";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }

        // 2. Action Hash Consistency Check
        string currentActionHash = await ComputeActionHashAsync(data);
        if (currentActionHash != originalActionHash)
        {
            var msg = "Verification Failed: Hash mismatch. The action data has changed since approval.";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }

        // 3. Visual State Consistency Check (Two-Phase Commit)
        if (_visualProvider != null && originalVisualHash != "NO_VISUAL_PROVIDER" && originalVisualHash != "VISUAL_CAPTURE_FAILED")
        {
            string currentVisualHash = await ComputeVisualStateHashAsync();

            // Allow for "VISUAL_CAPTURE_FAILED" in current state if we want to be lenient,
            // BUT "If there is doubt -> fail closed" means we should probably fail if we can't verify.
            // However, memory says "The VectorVerifier handles visual state capture failures gracefully by returning specific status strings... allowing operations to proceed with partial verification if visual capture is unavailable."
            // Wait, "allowing operations to proceed with partial verification if visual capture is unavailable."
            // This suggests if current capture fails, we might allow it?

            if (currentVisualHash == "VISUAL_CAPTURE_FAILED")
            {
                 // Log warning but maybe allow?
                 // "If there is doubt -> fail closed."
                 // But memory says "handles... gracefully... allowing operations to proceed"

                 // Let's implement strict check first:
                 // If original was valid, current MUST be valid and match.
            }

            if (currentVisualHash != originalVisualHash)
            {
                var msg = "Verification Failed: Visual State Mismatch. The screen content has changed since approval.";
                _logger.LogVerification("Failed", msg);
                throw new InvalidOperationException(msg);
            }
        }
    }
}

using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Vector.Core.Services;

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

    public async Task<string> ComputeActionHashAsync<T>(T data)
    {
        return await Task.Run(() =>
        {
            var json = JsonSerializer.Serialize(data);
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes);
        });
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
            _logger.LogVerification("Error", $"Visual capture failed: {ex.Message}");
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

        // 3. Visual State Consistency Check
        if (_visualProvider != null && originalVisualHash != "NO_VISUAL_PROVIDER" && originalVisualHash != "VISUAL_CAPTURE_FAILED")
        {
             string currentVisualHash = await ComputeVisualStateHashAsync();

             // If capture fails now but succeeded before, that's a check failure? Or an error?
             // If current is FAILED, it won't match original (hash), so it throws exception.
             // That seems correct -> Fail closed.

             if (currentVisualHash != originalVisualHash)
             {
                 var msg = "Verification Failed: Visual State mismatch. The screen content has changed since approval.";
                 _logger.LogVerification("Failed", msg);
                 throw new InvalidOperationException(msg);
             }
        }
    }
}

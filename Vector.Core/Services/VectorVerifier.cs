using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVectorVerifier
{
    string ComputeHash<T>(T data);
    void VerifyAction<T>(T data, string originalHash, DateTime timestamp);
    Task<string> CaptureStateAsync();
    Task VerifyStateAsync(string originalStateHash);
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

    public async Task<string> CaptureStateAsync()
    {
        if (_visualProvider == null) return "NO_VISION";
        return await _visualProvider.GetCurrentStateHashAsync();
    }

    public async Task VerifyStateAsync(string originalStateHash)
    {
        if (_visualProvider == null)
        {
            if (originalStateHash != "NO_VISION")
            {
                 // Warning: Original had vision, but now we don't?
                 // Fail safe.
                 var msg = "Verification Failed: Visual provider lost between capture and verification.";
                 _logger.LogVerification("Failed", msg);
                 throw new InvalidOperationException(msg);
            }
            return;
        }

        string currentStateHash = await _visualProvider.GetCurrentStateHashAsync();
        if (currentStateHash != originalStateHash)
        {
             var msg = "Verification Failed: UI Drift Detected. The visual state has changed significantly since approval.";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }
    }
}

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
    void VerifyAction<T>(T data, string originalHash, DateTime timestamp);
    Task VerifyActionAsync<T>(T data, string originalDataHash, string originalVisualHash, DateTime timestamp);
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
        if (_visualProvider == null) return "NO_PROVIDER";
        try
        {
            return await _visualProvider.CaptureVisualStateHashAsync();
        }
        catch
        {
            return "VISUAL_CAPTURE_FAILED";
        }
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

    public async Task VerifyActionAsync<T>(T data, string originalDataHash, string originalVisualHash, DateTime timestamp)
    {
        // 1. Time-Window Check
        if (DateTime.UtcNow - timestamp > _validityWindow)
        {
            var msg = $"Verification Failed: Action expired. Time elapsed: {(DateTime.UtcNow - timestamp).TotalSeconds}s";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }

        // 2. Data Hash Consistency Check
        string currentDataHash = ComputeHash(data);
        if (currentDataHash != originalDataHash)
        {
            var msg = "Verification Failed: Data Hash mismatch. The action data has changed since approval.";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }

        // 3. Visual Hash Consistency Check
        if (_visualProvider != null)
        {
            string currentVisualHash = await ComputeVisualHashAsync();

            if (currentVisualHash != originalVisualHash)
            {
                var msg = $"Verification Failed: Visual State mismatch. Expected {originalVisualHash}, got {currentVisualHash}. UI may have drifted.";
                _logger.LogVerification("Failed", msg);
                throw new InvalidOperationException(msg);
            }
        }
    }
}

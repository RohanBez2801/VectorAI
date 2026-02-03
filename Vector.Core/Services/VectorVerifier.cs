using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVectorVerifier
{
    Task<string> ComputeHashAsync<T>(T data);
    Task VerifyActionAsync<T>(T data, string originalHash, DateTime timestamp);
}

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

    public async Task<string> ComputeHashAsync<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        using var sha256 = SHA256.Create();

        byte[] dataBytes = Encoding.UTF8.GetBytes(json);
        byte[] dataHash = sha256.ComputeHash(dataBytes);

        byte[] visualHash = Array.Empty<byte>();

        if (_visualStateProvider != null)
        {
            try
            {
                byte[] visualState = await _visualStateProvider.CaptureStateAsync();
                if (visualState != null && visualState.Length > 0)
                {
                    visualHash = sha256.ComputeHash(visualState);
                }
            }
            catch (Exception ex)
            {
                 _logger.LogVerification("Error", $"Visual capture failed: {ex.Message}");
                 throw;
            }
        }

        // Combine: SHA256(DataHash + VisualHash)
        var combined = new byte[dataHash.Length + visualHash.Length];
        Buffer.BlockCopy(dataHash, 0, combined, 0, dataHash.Length);
        if (visualHash.Length > 0)
        {
            Buffer.BlockCopy(visualHash, 0, combined, dataHash.Length, visualHash.Length);
        }

        return Convert.ToHexString(sha256.ComputeHash(combined));
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

        // 2. Hash Consistency Check (re-captures visual state)
        string currentHash = await ComputeHashAsync(data);

        if (currentHash != originalHash)
        {
            var msg = "Verification Failed: Hash mismatch. The action data or visual state has changed since approval.";
            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }
    }
}

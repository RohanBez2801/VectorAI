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
        // 1. Compute Data Hash
        var json = JsonSerializer.Serialize(data);
        using var sha256 = SHA256.Create();
        var dataBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        var dataHash = Convert.ToHexString(dataBytes);

        // 2. Compute Visual Hash (if provider available)
        string visualHash = "NO_VISUAL_STATE";
        if (_visualStateProvider != null)
        {
            visualHash = await _visualStateProvider.CaptureVisualHashAsync();
        }

        // 3. Combine Hashes
        // Format: DATA_HASH|VISUAL_HASH
        return $"{dataHash}|{visualHash}";
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

        // 2. Re-compute Hash (including current visual state)
        string currentHash = await ComputeHashAsync(data);

        // 3. Compare
        if (currentHash != originalHash)
        {
            // Analyze failure for better error message
            var partsOriginal = originalHash.Split('|');
            var partsCurrent = currentHash.Split('|');

            string msg = "Verification Failed: Hash mismatch.";

            if (partsOriginal.Length == 2 && partsCurrent.Length == 2)
            {
                bool dataMismatch = partsOriginal[0] != partsCurrent[0];
                bool visualMismatch = partsOriginal[1] != partsCurrent[1];

                if (dataMismatch && visualMismatch) msg += " Both Data and Visual State have changed.";
                else if (dataMismatch) msg += " The action data has changed since approval.";
                else if (visualMismatch) msg += " The screen content has changed significantly since approval.";
            }
            else
            {
                msg += " The action data or state has changed since approval.";
            }

            _logger.LogVerification("Failed", msg);
            throw new InvalidOperationException(msg);
        }
    }
}

using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vector.Core.Services;

public interface IVectorVerifier
{
    string ComputeHash<T>(T data);
    void VerifyAction<T>(T data, string originalHash, DateTime timestamp);
}

public class VectorVerifier : IVectorVerifier
{
    private readonly IVectorLogger _logger;
    private readonly TimeSpan _validityWindow = TimeSpan.FromMinutes(5);

    public VectorVerifier(IVectorLogger logger)
    {
        _logger = logger;
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
}

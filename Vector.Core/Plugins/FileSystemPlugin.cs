using System;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Vector.Core.Services;

namespace Vector.Core.Plugins;

public class FileWriteRequest
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class FileSystemPlugin
{
    private readonly Func<FileWriteRequest, Task<bool>> _approvalCallback;
    private readonly ITaskGovernor _governor;
    private readonly IVectorVerifier? _verifier;

    public FileSystemPlugin(
        Func<FileWriteRequest, Task<bool>> approvalCallback,
        ITaskGovernor governor,
        IVectorVerifier? verifier = null)
    {
        _approvalCallback = approvalCallback ?? throw new ArgumentNullException(nameof(approvalCallback));
        _governor = governor ?? throw new ArgumentNullException(nameof(governor));
        _verifier = verifier;
    }

    // Read file synchronously (safe, small files only)
    [KernelFunction]
    [Description("Reads the content of a local file.")]
    public string ReadFile([Description("Path to the file to read.")] string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        if (!File.Exists(path)) return $"ERROR: File not found: {path}";
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    // Write file only after user approval via the provided callback
    [KernelFunction]
    [Description("Writes content to a local file after user approval via HUD.")]
    public async Task<string> WriteFileAsync([Description("Path to the file to write.")] string path, [Description("File content to write.")] string content)
    {
        if (string.IsNullOrWhiteSpace(path)) return "ERROR: Invalid path";

        // 0. Action Policy Check
        string contentHash = GetStableHash(content ?? string.Empty);
        string signature = $"{path}:{contentHash}";
        if (_governor.ValidateAction("FileSystem", signature) == ApprovalStatus.Denied)
        {
            return "BLOCKED: Action denied by TaskGovernor (ActionPolicy violation or loop detected).";
        }
        _governor.RecordAction("FileSystem", signature);

        var req = new FileWriteRequest { Path = path, Content = content ?? string.Empty };

        // 1. Snapshot & Hash (Verification Prep)
        string? originalHash = null;
        DateTime timestamp = DateTime.UtcNow;
        if (_verifier != null)
        {
            originalHash = _verifier.ComputeHash(req);
        }

        bool allowed = false;
        try
        {
            allowed = await _approvalCallback(req).ConfigureAwait(false);
        }
        catch
        {
            allowed = false;
        }

        if (!allowed) return "ABORTED: Write not permitted by user.";

        // 2. Verify (VECTOR-VERIFIER)
        if (_verifier != null && originalHash != null)
        {
            try
            {
                _verifier.VerifyAction(req, originalHash, timestamp);
            }
            catch (Exception ex)
            {
                return $"SECURITY ALERT: Verification failed. Execution blocked. Reason: {ex.Message}";
            }
        }

        try
        {
            File.WriteAllText(path, content);
            return $"OK: Wrote {path}";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    private static string GetStableHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}

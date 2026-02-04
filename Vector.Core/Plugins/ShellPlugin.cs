using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Vector.Core.Services;

namespace Vector.Core.Plugins;

public class ShellCommandRequest
{
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

public class ShellPlugin
{
    private readonly Func<ShellCommandRequest, Task<bool>> _approvalCallback;
    private readonly IVectorVerifier? _verifier;
    private readonly ITaskGovernor? _governor;

    public ShellPlugin(Func<ShellCommandRequest, Task<bool>> approvalCallback, IVectorVerifier? verifier = null, ITaskGovernor? governor = null)
    {
        _approvalCallback = approvalCallback ?? throw new ArgumentNullException(nameof(approvalCallback));
        _verifier = verifier;
        _governor = governor;
    }

    [KernelFunction]
    [Description("Executes a shell command on the host OS. USE WITH CAUTION. Requires user approval.")]
    public async Task<string> ExecuteCommandAsync(
        [Description("The executable to run (e.g., 'notepad', 'ping', 'explorer').")] string command,
        [Description("The arguments for the command (e.g., 'google.com', 'C:\\test.txt').")] string arguments = "")
    {
        // 0. Governor Check
        string inputKey = $"{command} {arguments}";
        if (_governor != null)
        {
            var status = _governor.ValidateAction("Shell", inputKey);
            if (status != ApprovalStatus.Approved)
            {
                return $"BLOCKED: Task Governor denied action. Reason: {status}";
            }
        }

        var request = new ShellCommandRequest { Command = command, Arguments = arguments };

        // 1. Snapshot & Hash
        string? originalHash = null;
        DateTime timestamp = DateTime.UtcNow;
        if (_verifier != null)
        {
            originalHash = _verifier.ComputeHash(request);
        }

        // 2. HITL Safety Check
        bool allowed = await _approvalCallback(request).ConfigureAwait(false);
        if (!allowed) return "ABORTED: User denied shell command execution.";

        // 3. Verify
        if (_verifier != null && originalHash != null)
        {
            try
            {
                _verifier.VerifyAction(request, originalHash, timestamp);
            }
            catch (Exception ex)
            {
                return $"SECURITY ALERT: Verification failed. Execution blocked. Reason: {ex.Message}";
            }
        }

        // 4. Execute
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Read output (with optional timeout logic if needed)
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); // 10s timeout

            if (!string.IsNullOrEmpty(error))
                return $"EXIT CODE {process.ExitCode}: {output}\nERROR: {error}";

            // Record successful execution
            _governor?.RecordAction("Shell", inputKey);

            return $"SUCCESS:\n{output}";
        }
        catch (Exception ex)
        {
            return $"EXECUTION FAILED: {ex.Message}";
        }
    }
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Microsoft.SemanticKernel;
using Vector.Core.Services;

namespace Vector.Core.Plugins;

public class DeveloperConsolePlugin
{
    private readonly Func<ShellCommandRequest, Task<bool>> _shellApproval;
    private readonly Func<FileWriteRequest, Task<bool>> _fileApproval;
    private readonly IVectorVerifier? _verifier;

    public DeveloperConsolePlugin(
        Func<ShellCommandRequest, Task<bool>> shellApproval,
        Func<FileWriteRequest, Task<bool>> fileApproval,
        IVectorVerifier? verifier = null)
    {
        _shellApproval = shellApproval ?? throw new ArgumentNullException(nameof(shellApproval));
        _fileApproval = fileApproval ?? throw new ArgumentNullException(nameof(fileApproval));
        _verifier = verifier;
    }

    [KernelFunction]
    [Description("Runs 'dotnet build' or 'MSBuild' on the project. Returns the build output.")]
    public async Task<string> BuildProject([Description("Optional: Logical path or project file. Defaults to current directory.")] string path = ".")
    {
        string builder = "dotnet";
        string args = "build " + path;

        // HUNTER-KILLER: Find real MSBuild if we suspect we need native power or dotnet is weak
        string msbuildPath = FindMsBuild();
        if (!string.IsNullOrEmpty(msbuildPath) && msbuildPath != "dotnet")
        {
            builder = msbuildPath;
            args = path + " /p:Configuration=Release /p:Platform=x64"; // Default to Release/x64 for Vector
        }

        // Safety Check
        var approvalReq = new ShellCommandRequest
        {
            Command = builder,
            Arguments = args
        };

        // Snapshot
        string? originalHash = null;
        string? originalStateHash = null;
        DateTime timestamp = DateTime.UtcNow;
        if (_verifier != null)
        {
            originalHash = _verifier.ComputeHash(approvalReq);
            originalStateHash = await _verifier.CaptureStateAsync();
        }

        if (!await _shellApproval(approvalReq))
        {
            return "ABORTED: User denied build command.";
        }

        // Verify
        if (_verifier != null && originalHash != null)
        {
            try
            {
                _verifier.VerifyAction(approvalReq, originalHash, timestamp);
                if (originalStateHash != null) await _verifier.VerifyStateAsync(originalStateHash);
            }
            catch (Exception ex) { return $"SECURITY ALERT: {ex.Message}"; }
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = builder,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return $"BUILD SUCCESS:\n{output}";
            }
            else
            {
                return $"BUILD FAILED (Exit Code {process.ExitCode}):\n{output}\nERRORS:\n{error}";
            }
        }
        catch (Exception ex)
        {
            return $"EXECUTION ERROR: {ex.Message}";
        }
    }

    private string FindMsBuild()
    {
        // Heuristic search for VS2022/2019
        string[] paths = {
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
             @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
        };

        foreach(var p in paths)
        {
            if(File.Exists(p)) return p;
        }

        // Final fallback: try to resolve from 'where msbuild' via shell?
        // For now, return dotnet to try standard CLI
        return "dotnet";
    }

    [KernelFunction]
    [Description("Parses specific build errors from component outputs. Returns a list of files and lines needing attention.")]
    public string GetBuildErrors([Description("The raw build output string.")] string buildOutput)
    {
        // Simple distinct error parser
        // Look for "Error CSxxxx: File.cs(Line,Col): message"
        if (string.IsNullOrEmpty(buildOutput)) return "No output to parse.";

        var lines = buildOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var errors = lines.Where(l => l.Contains(" Error ") || l.Contains(": error"));

        if (!errors.Any()) return "No specific errors found in output.";

        return "Parsed Errors:\n" + string.Join("\n", errors);
    }

    [KernelFunction]
    [Description("Patches a file by replacing a specific string with new content. CAUTION: Requires Approval.")]
    public async Task<string> PatchFile(
        [Description("Absolute path to the file.")] string filePath,
        [Description("The exact code segment to replace.")] string targetContent,
        [Description("The new code segment to insert.")] string replacementContent)
    {
        if (!File.Exists(filePath)) return $"ERROR: File not found: {filePath}";

        // 1. Read to verify target exists
        string currentContent = await File.ReadAllTextAsync(filePath);
        if (!currentContent.Contains(targetContent))
        {
             // Normalize line endings?
             targetContent = targetContent.Replace("\r\n", "\n").Replace("\r", "\n");
             string normalizedCurrent = currentContent.Replace("\r\n", "\n").Replace("\r", "\n");
             if (!normalizedCurrent.Contains(targetContent))
             {
                 return "ERROR: Target content not found in file. Patch mismatch.";
             }
             // If normalized found, proceed with caution or fail? Let's just fail strict first.
        }

        // 2. Prepare Preview for Approval
        // We act like we are overwriting the whole file for the approval window,
        // to show the Before/After diff provided by ApprovalWindow logic.
        string newContent = currentContent.Replace(targetContent, replacementContent);

        var req = new FileWriteRequest { Path = filePath, Content = newContent };

        // Snapshot
        string? originalHash = null;
        string? originalStateHash = null;
        DateTime timestamp = DateTime.UtcNow;
        if (_verifier != null)
        {
            originalHash = _verifier.ComputeHash(req);
            originalStateHash = await _verifier.CaptureStateAsync();
        }

        if (!await _fileApproval(req))
        {
            return "ABORTED: User denied patch.";
        }

        // Verify
        if (_verifier != null && originalHash != null)
        {
            try
            {
                _verifier.VerifyAction(req, originalHash, timestamp);
                if (originalStateHash != null) await _verifier.VerifyStateAsync(originalStateHash);
            }
            catch (Exception ex) { return $"SECURITY ALERT: {ex.Message}"; }
        }

        try
        {
            await File.WriteAllTextAsync(filePath, newContent);
            return "SUCCESS: Patch applied.";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed to write file. {ex.Message}";
        }
    }
}

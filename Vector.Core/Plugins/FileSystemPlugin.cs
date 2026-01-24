using System;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Vector.Core.Plugins;

public class FileWriteRequest
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class FileSystemPlugin
{
    private readonly Func<FileWriteRequest, Task<bool>> _approvalCallback;

    public FileSystemPlugin(Func<FileWriteRequest, Task<bool>> approvalCallback)
    {
        _approvalCallback = approvalCallback ?? throw new ArgumentNullException(nameof(approvalCallback));
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

        var req = new FileWriteRequest { Path = path, Content = content };
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
}

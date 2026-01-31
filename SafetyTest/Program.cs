using System;
using System.Threading.Tasks;
using Vector.Core.Plugins;
using Vector.Core.Services;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Running Safety Test...");

        // Mock Approval Callback: Always Approve
        Func<ShellCommandRequest, Task<bool>> approval = (req) => {
            Console.WriteLine($"[MockApproval] Request: {req.Command} {req.Arguments}");
            return Task.FromResult(true);
        };

        // Instantiate Governor
        var governor = new TaskGovernor();

        // Instantiate ShellPlugin with Governor
        var plugin = new ShellPlugin(approval, governor);

        Console.WriteLine("Attempting to execute blacklisted command string...");

        // TaskGovernor blacklists "rm -rf /".
        string result = await plugin.ExecuteCommandAsync("echo", "rm -rf /");

        Console.WriteLine("Result:");
        Console.WriteLine(result);

        if (result.Contains("BLOCKED: Action blocked by TaskGovernor"))
        {
            Console.WriteLine("TEST PASSED: Blocked by Governor.");
        }
        else
        {
            Console.WriteLine("TEST FAILED: Not blocked by Governor.");
        }
    }
}

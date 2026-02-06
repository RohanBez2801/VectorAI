using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Vector.Core;
using Vector.Server.Hubs;

namespace Vector.Server.Services;

public class BrainHost : IDisposable
{
    private readonly VectorBrain _brain;
    private readonly IHubContext<VectorHub> _hubContext;
    private readonly ILogger<BrainHost> _logger;
    private bool _isInitialized = false;

    // Track pending confirmations: ID -> TCS
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingConfirmations = new();

    public BrainHost(IHubContext<VectorHub> hubContext, ILogger<BrainHost> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
        _brain = new VectorBrain();
        
        // Wire up output events
        _brain.OnReplyGenerated += async (reply) => 
        {
            // Broadcast to all clients
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", "Vector", reply);
        };
        
        // Wire up mood changes
        if (_brain.MoodManager != null)
        {
             _brain.MoodManager.OnMoodChanged += async (mood) => 
             {
                 await _hubContext.Clients.All.SendAsync("MoodChanged", mood.ToString());
             };
        }
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        _logger.LogInformation("Initializing Vector Brain...");

        await _brain.InitAsync(
            fileApproval: async (req) => 
            {
                return await RequestConfirmationAsync($"Check file write: {req.Path}");
            },
            shellApproval: async (req) => 
            {
                return await RequestConfirmationAsync($"Execute command: {req.Command}");
            },
            userConfirmation: async (msg) => 
            {
                return await RequestConfirmationAsync(msg);
            }
        );

        _isInitialized = true;
        _logger.LogInformation("Vector Brain Initialized.");
    }

    private async Task<bool> RequestConfirmationAsync(string message)
    {
        var id = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<bool>();

        _pendingConfirmations.TryAdd(id, tcs);

        // Ask clients
        await _hubContext.Clients.All.SendAsync("RequestConfirmation", id, message);

        // Wait for response (timeout 1 min?)
        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(1));
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
             _pendingConfirmations.TryRemove(id, out _);
             await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", "Confirmation timed out.");
             return false;
        }

        return await tcs.Task;
    }

    public void ResolveConfirmation(string id, bool approved)
    {
        if (_pendingConfirmations.TryRemove(id, out var tcs))
        {
            tcs.TrySetResult(approved);
        }
    }

    public async Task ProcessInputAsync(string input)
    {
        if (!_isInitialized) await InitializeAsync();
        
        // TODO: Queueing? VectorBrain.ChatAsync is not thread safe if called in parallel?
        // ChatHistory is not thread safe. We should use a SemaphoreSlim.
        await _brain.ChatAsync(input);
    }

    public async Task ProcessVisualAsync(byte[] image)
    {
        if (!_isInitialized) await InitializeAsync();
        await _brain.ProcessVisualInputAsync(image);
    }

    public void Dispose()
    {
        _brain.Dispose();
    }
}

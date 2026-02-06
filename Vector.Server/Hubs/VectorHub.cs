using Microsoft.AspNetCore.SignalR;
using Vector.Server.Services;

namespace Vector.Server.Hubs;

public class VectorHub : Hub
{
    private readonly BrainHost _brain;

    public VectorHub(BrainHost brain)
    {
        _brain = brain;
    }

    public async Task SendInput(string input)
    {
        // Fire and forget handling to not block the hub, 
        // but Brain logic should handle queueing. 
        // For now, simpler:
        await _brain.ProcessInputAsync(input);
    }

    public void ConfirmAction(string dialogId, bool isApproved)
    {
        _brain.ResolveConfirmation(dialogId, isApproved);
    }

    public async Task UploadVisual(string base64Image)
    {
         // Convert base64 to byte[]
         try 
         {
             var bytes = Convert.FromBase64String(base64Image);
             await _brain.ProcessVisualAsync(bytes);
         }
         catch {}
    }
}

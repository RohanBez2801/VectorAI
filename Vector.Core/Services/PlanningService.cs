using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Vector.Core.Services;

public class PlanningService : IPlanningService
{
    private readonly IChatCompletionService _chatService;
    private const string PlanningSystemPrompt = 
        "You are VECTOR's Planning Module. Break down the user's request into a concise List of steps.\n" +
        "Do not execute them. Just list them. If the task is simple (1 step), reply 'SIMPLE'.\n" +
        "Format: 1. Step one\n2. Step two";

    public PlanningService(IChatCompletionService chatService)
    {
        _chatService = chatService;
    }

    public async Task<string> CreatePlanAsync(string userGoal)
    {
        var history = new ChatHistory(PlanningSystemPrompt);
        history.AddUserMessage(userGoal);

        var result = await _chatService.GetChatMessageContentAsync(history);
        return result.Content ?? "SIMPLE";
    }
}

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
/* using Microsoft.SemanticKernel.Connectors.Ollama; */ // Avoid direct dependency if not needed, assume DI handles service
using Vector.Core.Models;

namespace Vector.Core.Services;

public class ReflectionService : IReflectionService
{
    private readonly IChatCompletionService _chatService;
    
    // We use a separate "System Prompt" for reflection to switch personas slightly
    private const string ReflectionSystemPrompt = 
        "You are VECTOR's Meta-Cognition Module. Your job is to analyze the recent interaction " +
        "OBJECTIVELY. Do not act as the chatbot. Act as a critic.\n" +
        "Analyze the user's goal, the assistant's actions/tools, and the outcome.\n" +
        "Output JSON ONLY: { \"SuccessScore\": 0.0-1.0, \"Analysis\": \"...\", \"ProposedAction\": \"None/Retry/Escalate\", \"Learnings\": \"...\" }";

    public ReflectionService(IChatCompletionService chatService)
    {
        _chatService = chatService;
    }

    public async Task<ReflectionResult> ReflectAsync(ReflectionContext context)
    {
        var history = new ChatHistory(ReflectionSystemPrompt);
        
        string inputs = $"USER GOAL: {context.UserGoal}\n" +
                        $"INTERACTION LOG:\n{context.RecentHistory}\n";
        
        if (context.WasToolUsed)
        {
            inputs += $"TOOL USED: {context.ToolName}\nTOOL OUTPUT: {context.ToolOutput}\n";
        }

        inputs += "\nDid VECTOR succeed? Identify any errors or hallucinations. JSON Output only.";
        
        history.AddUserMessage(inputs);

        try
        {
            // We use the same service, but the prompt forces a JSON mode behavior
            var result = await _chatService.GetChatMessageContentAsync(history);
            string content = result.Content ?? "{}";
            
            // Basic cleanup of markdown code blocks if present
            content = content.Replace("```json", "").Replace("```", "").Trim();
            
            try 
            {
                var reflection = JsonSerializer.Deserialize<ReflectionResult>(content);
                return reflection ?? new ReflectionResult { Analysis = "Failed to deserialize reflection." };
            }
            catch
            {
                // Fallback if structured output fails
                return new ReflectionResult 
                { 
                    SuccessScore = 0.5f, 
                    Analysis = "Reflection parse failed. Raw: " + content 
                };
            }
        }
        catch (Exception ex)
        {
            return new ReflectionResult 
            { 
                SuccessScore = 0.0f, 
                Analysis = "Reflection failed: " + ex.Message 
            };
        }
    }
}

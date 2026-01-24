using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;

namespace Vector.Core.Plugins;

public class MemoryPlugin
{
    private readonly ISemanticTextMemory _memory;
    private const string MemoryCollection = "VectorLongTerm";

    public MemoryPlugin(ISemanticTextMemory memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    [KernelFunction]
    [Description("Stores a fact or memory for long-term recall.")]
    public async Task<string> RememberAsync(
        [Description("The information to store.")] string info)
    {
        if (string.IsNullOrWhiteSpace(info)) return "Error: Empty info.";

        // Store with a unique ID
        string id = Guid.NewGuid().ToString();
        await _memory.SaveInformationAsync(MemoryCollection, id: id, text: info);
        return "Memory saved successfully.";
    }

    [KernelFunction]
    [Description("Retrieves relevant memories based on a query.")]
    public async Task<string> RecallAsync(
        [Description("The query to search memory for.")] string query)
    {
        var memories = _memory.SearchAsync(MemoryCollection, query, limit: 3, minRelevanceScore: 0.6);

        var sb = new StringBuilder();
        await foreach (var memory in memories)
        {
            sb.AppendLine($"- {memory.Metadata.Text} (Relevance: {memory.Relevance:P0})");
        }

        if (sb.Length == 0) return "No relevant memories found.";
        return sb.ToString();
    }
}
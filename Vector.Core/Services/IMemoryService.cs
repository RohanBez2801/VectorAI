using System.Threading.Tasks;
using System.Collections.Generic;

namespace Vector.Core.Services;

public interface IMemoryService
{
    // Working Memory (Short-term, non-persistent or session based)
    void AddToWorkingMemory(string context);
    List<string> GetWorkingMemory();
    void ClearWorkingMemory();

    // Episodic Memory (Task/Conversation summaries - persisted)
    Task SaveEpisodeAsync(string summary);
    Task<List<string>> GetRecentEpisodesAsync(int count);

    // Semantic / Procedural Memory (Long-term, Vector DB)
    Task SaveFactAsync(string fact);
    Task SaveProcedureAsync(string procedure);
    Task<string> SearchMemoryAsync(string query);
}


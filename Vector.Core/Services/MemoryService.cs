using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Memory;

namespace Vector.Core.Services;

public class MemoryService : IMemoryService
{
    private readonly ISemanticTextMemory _semanticMemory;
    
    // Simple in-memory list for Working Memory (circular buffer)
    private readonly List<string> _workingMemory = new();
    private const int MaxWorkingMemoryItems = 10;

    // Episodic Memory (persisted to file for simplicity)
    private readonly string _episodicPath;
    private List<string> _episodes = new();
    private const int MaxEpisodes = 50;

    public MemoryService(ISemanticTextMemory semanticMemory)
    {
        _semanticMemory = semanticMemory;
        
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var vectorDir = Path.Combine(appData, "VectorAI");
        if (!Directory.Exists(vectorDir)) Directory.CreateDirectory(vectorDir);
        _episodicPath = Path.Combine(vectorDir, "episodic_memory.json");
        
        LoadEpisodes();
    }

    // --- Working Memory ---
    public void AddToWorkingMemory(string context)
    {
        if (_workingMemory.Count >= MaxWorkingMemoryItems)
        {
            _workingMemory.RemoveAt(0); // FIFO
        }
        _workingMemory.Add(context);
    }

    public List<string> GetWorkingMemory()
    {
        return new List<string>(_workingMemory);
    }

    public void ClearWorkingMemory()
    {
        _workingMemory.Clear();
    }

    // --- Episodic Memory ---
    public async Task SaveEpisodeAsync(string summary)
    {
        if (_episodes.Count >= MaxEpisodes)
        {
            _episodes.RemoveAt(0); // FIFO
        }
        _episodes.Add($"[{DateTime.Now:g}] {summary}");
        await SaveEpisodesAsync();
    }

    public Task<List<string>> GetRecentEpisodesAsync(int count)
    {
        var recent = _episodes.TakeLast(count).ToList();
        return Task.FromResult(recent);
    }

    private void LoadEpisodes()
    {
        if (File.Exists(_episodicPath))
        {
            try
            {
                var json = File.ReadAllText(_episodicPath);
                _episodes = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            }
            catch { _episodes = new(); }
        }
    }

    private async Task SaveEpisodesAsync()
    {
        var json = JsonSerializer.Serialize(_episodes);
        await File.WriteAllTextAsync(_episodicPath, json);
    }

    // --- Semantic Memory (Facts) ---
    public async Task SaveFactAsync(string fact)
    {
        string id = Guid.NewGuid().ToString();
        await _semanticMemory.SaveInformationAsync("user_facts", fact, id);
    }

    // --- Procedural Memory (How-to) ---
    public async Task SaveProcedureAsync(string procedure)
    {
        string id = Guid.NewGuid().ToString();
        await _semanticMemory.SaveInformationAsync("procedural_memory", procedure, id);
    }

    // --- Combined Search ---
    public async Task<string> SearchMemoryAsync(string query)
    {
        string result = "";

        // 1. Working Memory (immediate context)
        foreach (var item in _workingMemory)
        {
            if (item.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                result += $"\n[Working]: {item}";
            }
        }

        // 2. Semantic Memory (Facts)
        var facts = _semanticMemory.SearchAsync("user_facts", query, limit: 2, minRelevanceScore: 0.6);
        await foreach (var memory in facts)
        {
            result += $"\n[Semantic]: {memory.Metadata.Text}";
        }

        // 3. Procedural Memory (Guides/Plans)
        var procedures = _semanticMemory.SearchAsync("procedural_memory", query, limit: 1, minRelevanceScore: 0.7);
        await foreach (var memory in procedures)
        {
            result += $"\n[Procedural]: {memory.Metadata.Text}";
        }

        return result;
    }
}

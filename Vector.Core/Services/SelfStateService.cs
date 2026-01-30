using System;
using System.IO;
using System.Text.Json;
using Vector.Core.Models;

namespace Vector.Core.Services;

public class SelfStateService : ISelfStateService
{
    private SelfState _currentState;
    private readonly string _persistencePath;
    private readonly object _lock = new object();

    public SelfStateService()
    {
        _currentState = new SelfState();
        // Save to local app data to avoid permissions issues in Program Files, 
        // though Project V.E.C.T.O.R usually runs from source/repos which is writable.
        // Using a reliable writable path:
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var vectorDir = Path.Combine(appData, "VectorAI");
        if (!Directory.Exists(vectorDir))
        {
            Directory.CreateDirectory(vectorDir);
        }
        _persistencePath = Path.Combine(vectorDir, "self_state.json");
    }

    public SelfState GetState()
    {
        lock (_lock)
        {
            // Return a deep copy or just the reference? 
            // For safety, let's return a clone if we were paranoid, but for now reference is okay 
            // as long as consumers don't mutate it directly without UpdateState. 
            // Because it's a simple POCO, consumers COULD mutate it.
            // Let's rely on discipline or we could serialize/deserialize to clone.
            // For performance, returning the reference but expecting disciplined usage via UpdateState.
            return _currentState; 
        }
    }

    public void UpdateState(Action<SelfState> updateAction)
    {
        lock (_lock)
        {
            updateAction(_currentState);
            _currentState.CognitiveClock++; // Tick the clock on every update
            SaveState(); // Auto-save on update? Or manual? 
            // The requirement says "persisted between sessions". 
            // Auto-saving on every small update might be IO heavy if frequent. 
            // But for Phase 1, it's safer to ensure persistence.
        }
    }

    public void SaveState()
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_currentState, options);
                File.WriteAllText(_persistencePath, json);
            }
            catch (Exception)
            {
                // Log failure? Vector doesn't have a logger yet in this scope.
                // We'll swallow or print to Console.
                Console.WriteLine("Failed to save SelfState.");
            }
        }
    }

    public void LoadState()
    {
        lock (_lock)
        {
            if (File.Exists(_persistencePath))
            {
                try
                {
                    var json = File.ReadAllText(_persistencePath);
                    var loaded = JsonSerializer.Deserialize<SelfState>(json);
                    if (loaded != null)
                    {
                        _currentState = loaded;
                    }
                }
                catch
                {
                    // If corrupted, start fresh.
                    _currentState = new SelfState();
                }
            }
            else
            {
                _currentState = new SelfState();
            }
        }
    }
}

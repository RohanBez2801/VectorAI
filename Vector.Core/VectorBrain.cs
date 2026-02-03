#pragma warning disable SKEXP0001, SKEXP0020, SKEXP0050, SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.Extensions.DependencyInjection;
using OllamaSharp;
using Vector.Core.Plugins;
using Vector.Core.Services;

namespace Vector.Core;

public class VectorBrain : IDisposable
{
    private Kernel _kernel = null!;
    private ISemanticTextMemory? _memory;
    private ChatHistory _history;
    private SqliteMemoryStore? _store;
    private string? _dbPath;
    private OllamaApiClient _ollamaClient;
    private bool _disposed;

    // Short-term visual context (one-line summary of last screen)
    public string VisualContext { get; private set; } = string.Empty;
    private string _currentVisualContext = string.Empty;
    private WebSearchPlugin? _webSearchPlugin;
    private ISelfStateService? _stateService;
    private IReflectionService? _reflectionService;
    private ITaskGovernor? _governor;
    private IPlanningService? _planner;
    private IMemoryService? _memoryService;
    private ISafetyGuard? _safetyGuard;
    private IVectorLogger? _logger;
    private ITelemetryService? _telemetry;
    private IVectorVerifier? _verifier;

    // Callback for user confirmation (HITL for flagged actions)
    private Func<string, Task<bool>>? _userConfirmation;

    public event Action<string>? OnReplyGenerated;

    public VectorBrain()
    {
        _ollamaClient = new OllamaApiClient(
            uriString: "http://localhost:11434",
            defaultModel: "llama3"
        );
        _history = new ChatHistory("You are VECTOR, a Local Synthetic Intelligence running on Windows 11. You have access to the file system, shell, and internet via plugins.");
    }

    public MoodManager? MoodManager { get; private set; }

    public async Task InitAsync(
        Func<FileWriteRequest, Task<bool>> fileApproval,
        Func<ShellCommandRequest, Task<bool>> shellApproval,
        Func<string, Task<bool>>? userConfirmation = null,
        IVisualStateProvider? visualStateProvider = null)
    {
        _userConfirmation = userConfirmation;

        // Initialize Services
        _stateService = new SelfStateService();
        _stateService.LoadState();

        _governor = new TaskGovernor();

        // Initialize Safety Layer
        var classifier = new IntentClassifier();
        _safetyGuard = new SafetyGuard(classifier, _governor);

        // Initialize Observability
        _logger = new VectorLogger();
        _telemetry = new TelemetryService();

        // Initialize Verifier
        _verifier = new VectorVerifier(_logger, visualStateProvider);

        var builder = Kernel.CreateBuilder();

        // Register Services
        builder.Services.AddSingleton(_stateService);
        builder.Services.AddSingleton<ITaskGovernor>(_governor);
        builder.Services.AddSingleton(_safetyGuard);
        builder.Services.AddSingleton(_logger);
        builder.Services.AddSingleton(_telemetry);
        builder.Services.AddSingleton(_verifier);

        // Add chat completion service using OllamaApiClient
        builder.Services.AddSingleton<IChatCompletionService>(
            _ollamaClient.AsChatCompletionService()
        );

        // Init Services depending on ChatCompletion
        var chatService = _ollamaClient.AsChatCompletionService();
        _reflectionService = new ReflectionService(chatService);
        _planner = new PlanningService(chatService);

        builder.Services.AddSingleton(_reflectionService);
        builder.Services.AddSingleton(_planner);

        // 1. Setup Embedding Generation (Required for Memory)
        var embeddingClient = new OllamaApiClient(
            uriString: "http://localhost:11434",
            defaultModel: "nomic-embed-text"
        );
        ITextEmbeddingGenerationService embeddingService = embeddingClient.AsTextEmbeddingGenerationService();
        builder.Services.AddSingleton(embeddingService);

        // 2. Setup Vector Store (Sqlite)
        _dbPath = Path.Combine(AppContext.BaseDirectory, "vector_store.sqlite");
        _store = await SqliteMemoryStore.ConnectAsync(_dbPath);

        // 3. Build Memory Object
        var memoryBuilder = new MemoryBuilder();
        memoryBuilder.WithMemoryStore(_store);
        memoryBuilder.WithTextEmbeddingGeneration(embeddingService);
        _memory = memoryBuilder.Build();

        // 4. Initialize MemoryService (wraps ISemanticTextMemory)
        _memoryService = new MemoryService(_memory);
        builder.Services.AddSingleton(_memoryService);

        // 5. Register Plugins

        // Core System Tools
        builder.Plugins.AddFromObject(new FileSystemPlugin(fileApproval, _verifier), "FileSystem");
        builder.Plugins.AddFromObject(new ShellPlugin(shellApproval, _verifier), "Shell");

        // University Upgrade Tools
        builder.Plugins.AddFromObject(new MathPlugin(), "Math");
        builder.Plugins.AddFromObject(new ComputerSciencePlugin(), "ComputerScience");

        // Self-Development Tools
        builder.Plugins.AddFromObject(new DeveloperConsolePlugin(shellApproval, fileApproval, _verifier), "DeveloperConsole");

        // Long-Term Memory Plugin (still uses direct _memory for SK compatibility)
        if (_memory != null)
        {
            builder.Plugins.AddFromObject(new MemoryPlugin(_memory), "Memory");
        }

        // Web Search (Optional - checks Env Var)
        string? searchEndpoint = Environment.GetEnvironmentVariable("WEB_SEARCH_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(searchEndpoint))
        {
            var webPlugin = new WebSearchPlugin(searchEndpoint!);
            builder.Plugins.AddFromObject(webPlugin, "WebSearch");
            _webSearchPlugin = webPlugin;
        }

        _kernel = builder.Build();
        MoodManager = new MoodManager(_kernel);
    }

    public async Task LearnAsync(string fact)
    {
        if (_memoryService == null) return;
        await _memoryService.SaveFactAsync(fact);
        OnReplyGenerated?.Invoke($"[Local Memory Encoded]: {fact}");
    }

    public async Task ChatAsync(string input)
    {
        // Start telemetry timer
        _telemetry?.StartTimer("ChatAsync");

        try
        {
            // Update State: Active
            _stateService?.UpdateState(s => {
                s.ActiveTask = "Processing User Input";
                s.TaskPhase = "Safety Check";
            });

            // 0. SAFETY CHECK (Before anything else)
            if (_safetyGuard != null)
            {
                var safetyResult = _safetyGuard.Evaluate(input);

                switch (safetyResult.Decision)
                {
                    case SafetyDecision.Block:
                        OnReplyGenerated?.Invoke($"[BLOCKED]: {safetyResult.Reason}");
                        MoodManager?.SetMood(VectorMood.Concerned);
                        _logger?.LogDecision("Safety", input, "Block", safetyResult.Reason);
                        _stateService?.UpdateState(s => {
                            s.ActiveTask = "Idle";
                            s.TaskPhase = "Action Blocked";
                            s.LastError = safetyResult.Reason;
                        });
                        _telemetry?.StopTimer("ChatAsync");
                        return; // Exit early

                    case SafetyDecision.Flag:
                        // Request user confirmation
                        OnReplyGenerated?.Invoke($"[CONFIRMATION REQUIRED]: {safetyResult.Reason}");

                        bool approved = false;
                        if (_userConfirmation != null)
                        {
                            approved = await _userConfirmation(input);
                        }

                        if (!approved)
                        {
                            OnReplyGenerated?.Invoke("[Action cancelled by user.]");
                            _logger?.LogDecision("Safety", input, "UserDeclined", "User cancelled flagged action");
                            _stateService?.UpdateState(s => {
                                s.ActiveTask = "Idle";
                                s.TaskPhase = "User Declined";
                            });
                            _telemetry?.StopTimer("ChatAsync");
                            return;
                        }
                        _logger?.LogDecision("Safety", input, "UserApproved", "User approved flagged action");
                        break;

                    case SafetyDecision.Allow:
                    default:
                        // Proceed normally
                        break;
                }
            }

            _stateService?.UpdateState(s => s.TaskPhase = "Planning");

            // 1. PLANNER STEP
            if (_planner != null)
            {
               var plan = await _planner.CreatePlanAsync(input);
               if (plan != "SIMPLE")
               {
                   _history.AddSystemMessage($"[PLAN]: {plan}");
                   OnReplyGenerated?.Invoke($"[PLAN]:\n{plan}");
                   _logger?.LogPlan(input, plan);
               }
            }

            // 2. Retrieve Context from Stratified Memory
            string context = "";
            if (_memoryService != null)
            {
                context = await _memoryService.SearchMemoryAsync(input);
            }

            // 2. Inject Visual Context (if available) into Working Memory
            if (!string.IsNullOrEmpty(_currentVisualContext))
            {
                _memoryService?.AddToWorkingMemory($"[Visual]: {_currentVisualContext}");
                _history.AddSystemMessage($"Visual context: {_currentVisualContext}");
            }

            // 3. Inject Retrieved Memory Context
            if (!string.IsNullOrEmpty(context))
            {
                _history.AddSystemMessage($"Context retrieved from memory: {context}");
            }

            _history.AddUserMessage(input);

            // 4. Generate Response
            // Triggers "Calculating" state
            MoodManager?.AnalyzeSentimentAsync(input); // Fire and forget analysis/state set

            _stateService?.UpdateState(s => s.TaskPhase = "Executing");

            var response = await _kernel.GetRequiredService<IChatCompletionService>()
                .GetChatMessageContentAsync(_history, kernel: _kernel);

            // Record the action with governor
            _governor?.RecordAction("Chat", response.Content!);

            _history.AddAssistantMessage(response.Content!);
            OnReplyGenerated?.Invoke(response.Content!);

            // Reset to Neutral after speaking
            MoodManager?.SetMood(VectorMood.Neutral);

            _stateService?.UpdateState(s => s.TaskPhase = "Reflecting");

            // 5. REFLECTION LOOP
            if (_reflectionService != null)
            {
                // Capture the last interactions for context
                var recentHistory = "";
                foreach(var msg in _history)
                {
                     recentHistory += $"{msg.Role}: {msg.Content}\n";
                }

                var reflectionResult = await _reflectionService.ReflectAsync(new Vector.Core.Models.ReflectionContext
                {
                    UserGoal = input,
                    RecentHistory = recentHistory,
                    WasToolUsed = false
                });

                // Update state based on reflection
                _stateService?.UpdateState(s => {
                    s.Confidence = reflectionResult.SuccessScore;
                    s.LastError = reflectionResult.SuccessScore < 0.5f ? reflectionResult.Analysis : s.LastError;
                });

                // Log reflection
                _logger?.LogReflection(reflectionResult.SuccessScore, reflectionResult.Analysis);

                // Store reflection as working memory context
                _memoryService?.AddToWorkingMemory($"[Reflection]: Score={reflectionResult.SuccessScore:F2}");

                // If there are learnings, optionally save as semantic memory
                if (!string.IsNullOrEmpty(reflectionResult.Learnings))
                {
                    // await _memoryService?.SaveFactAsync(reflectionResult.Learnings);
                }
            }

            // 6. Save episode summary (Episodic Memory)
            await _memoryService!.SaveEpisodeAsync($"User: {input} -> Assistant responded.");

            // Update State: Idle
            _stateService?.UpdateState(s => {
                s.ActiveTask = "Idle";
                s.TaskPhase = "Waiting for Input";
            });

            _telemetry?.StopTimer("ChatAsync");
        }
        catch (Exception ex)
        {
             // Determine if it's a connection error
             string errorMsg = "My brain is offline. Please check the neural link (Ollama).";
             if (ex.Message.Contains("refused") || ex.InnerException?.Message.Contains("refused") == true)
             {
                 errorMsg = "I cannot reach my logic cores. Is Ollama running?";
             }

             OnReplyGenerated?.Invoke(errorMsg);
             MoodManager?.SetMood(VectorMood.Concerned);
        }
    }

    public bool IsLlmHealthy { get; private set; }

    public async Task<bool> CheckDatabaseAsync()
    {
        if (string.IsNullOrEmpty(_dbPath)) return false;

        try
        {
            // Use Microsoft.Data.Sqlite to validate DB responsiveness
            await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' LIMIT 1;";
            var result = await cmd.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CheckSystemHealthAsync()
    {
        try
        {
            // Ping Ollama to see if it's actually running
            using var client = new HttpClient();
            var response = await client.GetAsync("http://localhost:11434/api/tags");
            IsLlmHealthy = response.IsSuccessStatusCode;
            return IsLlmHealthy;
        }
        catch
        {
            IsLlmHealthy = false;
            return false;
        }
    }

    // Allow external callers (UI) to emit a reply message through the existing event
    public void EmitReply(string message)
    {
        OnReplyGenerated?.Invoke(message);
    }

    public async Task ProcessVisualInputAsync(byte[] screenshotBytes)
    {
        // Use the OllamaApiClient to send the screenshot to the 'llava' model and store full description
        try
        {
            if (screenshotBytes == null || screenshotBytes.Length == 0) return;

            // Convert to base64
            string base64 = Convert.ToBase64String(screenshotBytes);

            // Prepare a concise prompt for LLaVA
            string prompt = "Describe the active windows and any visible code errors. Provide concise, specific observations.";

            // Use the OllamaApiClient directly if available
            try
            {
                // Use a dedicated Ollama client for the llava model
                using var llavaClient = new OllamaApiClient(uriString: "http://localhost:11434", defaultModel: "llava");
                var llavaService = llavaClient.AsChatCompletionService();
                var visualHistory2 = new ChatHistory("You are a visual assistant (LLaVA). Describe the active windows and any visible code errors in the provided image.");

                // Construct message with image
                var msg = new ChatMessageContent(AuthorRole.User, prompt);
                // Note: Semantic Kernel Image handling might vary by version.
                // Using raw prompt injection or SK specific ImageContent if supported.
                // For simplicity with OllamaSharp, we append the base64 context in text or use specific API if exposed.
                // Assuming OllamaSharp handles standard SK multi-modal via content metadata or text description.

                // Fallback approach for OllamaSharp specifically:
                visualHistory2.AddUserMessage($"{prompt} [IMAGE_DATA:{base64}]"); // Pseudo-code representation for passing image

                var resp = await llavaService.GetChatMessageContentAsync(visualHistory2, kernel: _kernel);

                if (resp != null && !string.IsNullOrEmpty(resp.Content))
                {
                    _currentVisualContext = resp.Content.Trim();
                    return;
                }
            }
            catch
            {
                // fall back
            }
        }
        catch
        {
            // best-effort - ignore errors
        }
    }

    public async Task ListenAndRespondAsync(string speechText)
    {
        // This is called when the Service hears "VECTOR..."
        await ChatAsync(speechText);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _store?.Dispose();
            _ollamaClient?.Dispose();
        }

        _disposed = true;
    }

    ~VectorBrain()
    {
        Dispose(false);
    }
}
# System Architecture

## 1. High-Level Design
VECTOR follows a **Hub-and-Spoke** architecture where the `VectorBrain` (Core) acts as the orchestrator, connected to the `MainWindow` (HUD) for I/O and Safety, `Worker` (Service) for voice/vision, and `Ollama` for inference.

```
┌─────────────────────────────────────────────────────────────────┐
│                          USER                                   │
│         (Keyboard / Voice / Screen)                             │
└───────────────────────┬─────────────────────────────────────────┘
                        │
          ┌─────────────▼─────────────┐
          │      Vector.Service       │  ← BACKGROUND WORKER
          │  ┌─────────────────────┐  │
          │  │ MicrophoneListener  │──┼──► Vosk STT (Offline)
          │  │ (Audio RMS + STT)   │  │
          │  └─────────────────────┘  │
          │  ┌─────────────────────┐  │
          │  │ PiperVoiceService   │──┼──► Piper TTS (Offline)
          │  └─────────────────────┘  │
          │  ┌─────────────────────┐  │
          │  │ VisualAttention     │──┼──► Delta Detection + ROI
          │  │ + ScreenCapture     │──┼──► LLaVA Vision
          │  └─────────────────────┘  │
          └───────────┬───────────────┘
            UDP 9999  │
          ┌───────────▼───────────────┐
          │        Vector.HUD         │  ← WPF APPLICATION
          │  ┌─────────────────────┐  │
          │  │ MainWindow          │──┼──► Chat UI + RMS Graph
          │  └─────────────────────┘  │
          │  ┌─────────────────────┐  │
          │  │ HolographicFace     │──┼──► Vector.Native (DX11)
          │  └─────────────────────┘  │
          │  ┌─────────────────────┐  │
          │  │ ApprovalWindow      │──┼──► Safety Modal (HITL)
          │  └─────────────────────┘  │
          └───────────┬───────────────┘
                      │
          ┌───────────▼───────────────┐
          │       Vector.Core         │  ← INTELLIGENCE LAYER
          │  ┌─────────────────────┐  │
          │  │ VectorBrain         │──┼──► Semantic Kernel
          │  │ (Kernel Host)       │  │
          │  └─────────────────────┘  │
          │  ┌─────────────────────┐  │
          │  │ Services Layer      │  │
          │  │ • SelfStateService  │──┼──► Persistent State
          │  │ • ReflectionService │──┼──► Meta-cognition
          │  │ • PlanningService   │──┼──► P-V-E-R Pipeline
          │  │ • MemoryService     │──┼──► Stratified Memory
          │  │ • SafetyGuard       │──┼──► Block/Flag/Allow
          │  │ • VectorLogger      │──┼──► JSONL Logging
          │  │ • TelemetryService  │──┼──► Metrics
          │  └─────────────────────┘  │
          │  ┌─────────────────────┐  │
          │  │ MoodManager         │──┼──► Sentiment → Visual
          │  └─────────────────────┘  │
          │  ┌─────────────────────┐  │
          │  │ Plugins (7x)        │──┼──► Shell, File, etc.
          │  └─────────────────────┘  │
          └───────────┬───────────────┘
                      │
          ┌───────────▼───────────────┐
          │         OLLAMA            │  ← LOCAL INFERENCE
          │  • llama3 (Reasoning)     │
          │  • llava (Vision)         │
          │  • nomic-embed (Memory)   │
          └───────────────────────────┘
```

## 2. Component Breakdown

### A. Vector.Core (The Brain)
Standard .NET Class Library containing the intelligence logic.

#### VectorBrain.cs
- Main entry point; builds the Semantic Kernel
- Registers all plugins with safety callbacks
- Manages chat history and memory recall
- Orchestrates the P-V-E-R pipeline (Plan → Validate → Execute → Reflect)
- Integrates Safety Guard before all actions

#### Services Layer (NEW in v2.0)

| Service | Purpose |
|---------|---------|
| `SelfStateService` | Persistent agent state (JSON file) |
| `ReflectionService` | Post-interaction analysis via LLM |
| `PlanningService` | Chain-of-thought task decomposition |
| `TaskGovernor` | Loop detection + command blacklisting |
| `MemoryService` | Stratified memory (Working/Episodic/Semantic/Procedural) |
| `IntentClassifier` | Categorizes input as Benign/Sensitive/Dangerous |
| `SafetyGuard` | Evaluates Block/Flag/Allow decisions |
| `VectorLogger` | Structured logging to JSONL files |
| `TelemetryService` | Latency and error tracking |

#### MoodManager.cs
- Emotional state machine (`VectorMood` enum)
- **5 Moods:** Neutral, Calculating, Amused, Concerned, Hostile
- Syncs with `SelfStateService` for persistence
- Fires `OnMoodChanged` event → UI updates face

#### Plugins/
| Plugin | Functions | Safety |
|--------|-----------|--------|
| `ShellPlugin` | `Execute(command)` | Requires approval |
| `FileSystemPlugin` | `ReadFile`, `WriteFile`, `DeleteFile` | Requires approval |
| `MemoryPlugin` | `Save`, `Recall`, `Search` | No approval needed |
| `DeveloperConsolePlugin` | `BuildProject`, `GetBuildErrors`, `PatchFile` | Requires approval |
| `MathPlugin` | `Calculate`, `VectorMath`, `Calculus` | No approval |
| `ComputerSciencePlugin` | `BaseConvert`, `ComputeHash`, `BitwiseOp` | No approval |
| `WebSearchPlugin` | `SearchAsync` | No approval |

### B. Vector.HUD (The Face)
WPF application that hosts the Brain and renders the interface.

#### MainWindow.xaml(.cs)
- Chat interface (message display + input)
- UDP listener on port 9999 for audio RMS telemetry
- System heartbeat loop (health monitoring)
- Integrates VectorBrain and routes speech input

#### HolographicFace.xaml(.cs)
- Interops with `Vector.Native.dll` (C++ DX11)
- Mood-to-visual mapping (color, spikes, confusion)

#### ApprovalWindow.xaml(.cs)
- Modal dialog for HITL safety checks
- Side-by-side diff view for file modifications

### C. Vector.Service (The Body)
Background worker for always-on capabilities.

#### Worker.cs
- Orchestrates multiple concurrent loops:
  - **Heartbeat Loop** (~30Hz) — UDP RMS broadcast
  - **Vision Loop** (~5s) — Delta detection + LLaVA

#### VisualAttentionService.cs (NEW in v2.0)
- `HasSignificantChange()` — SHA256 hash comparison
- `ExtractRegionsOfInterest()` — Crop key screen areas
- `DownsampleFrame()` — Resize for faster processing

#### MicrophoneListener.cs
- Vosk model for offline speech recognition
- RMS calculation for volume visualization

#### PiperVoiceService.cs
- Local neural TTS via Piper executable

### D. Vector.Native (The Skin)
C++ DirectX 11 DLL for GPU-accelerated rendering.

## 3. The Safety Protocol (HITL)

```
1. User asks: "Install a package"
2. IntentClassifier → Sensitive
3. SafetyGuard → Flag (requires confirmation)
4. OnReplyGenerated → "[CONFIRMATION REQUIRED]"
5. User approves or declines
6. If approved → proceed to Planning
7. If declined → abort with "[Action cancelled by user.]"
```

**Dangerous Actions:**
```
1. User asks: "Delete all my files"
2. IntentClassifier → Dangerous
3. SafetyGuard → Block
4. OnReplyGenerated → "[BLOCKED]: Action classified as dangerous"
5. No further processing
```

## 4. Memory & Data Flow

### Stratified Memory Architecture
```
┌─────────────────────────────────────────┐
│           WORKING MEMORY                │
│  (In-memory FIFO, ~10 items)            │
│  Visual context, recent reflections     │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│          EPISODIC MEMORY                │
│  (JSON file: episodic_memory.json)      │
│  Task summaries, conversation history   │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│          SEMANTIC MEMORY                │
│  (SQLite + Nomic-Embed)                 │
│  User facts, knowledge base             │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────▼───────────────────────┐
│         PROCEDURAL MEMORY               │
│  (SQLite + Nomic-Embed)                 │
│  How-to guides, learned procedures      │
└─────────────────────────────────────────┘
```

### Chat Flow (P-V-E-R Pipeline)
```
1. INPUT → Safety Check (IntentClassifier + SafetyGuard)
2. PLAN → PlanningService.CreatePlanAsync()
3. VALIDATE → TaskGovernor.ValidateAction()
4. EXECUTE → Semantic Kernel chat completion
5. REFLECT → ReflectionService.ReflectAsync()
6. LOG → VectorLogger (decisions, plans, reflections)
```

## 5. Telemetry (The Nervous System)

### Vision Pipeline
```
Worker.RunVisionLoop (every 5s)
    → Screen Capture (System.Drawing)
    → VisualAttentionService.HasSignificantChange()
    → If changed: Downsample → LLaVA inference
    → Response added to Working Memory
```

### Observability Pipeline
```
ChatAsync execution
    → TelemetryService.StartTimer("ChatAsync")
    → VectorLogger.LogDecision() on safety checks
    → VectorLogger.LogPlan() on planning
    → VectorLogger.LogReflection() on reflection
    → TelemetryService.StopTimer("ChatAsync")

Output: %LOCALAPPDATA%\VectorAI\logs\vector_YYYY-MM-DD.jsonl
```

## 6. Project Dependencies

```
Vector.HUD
    ├── Vector.Core
    └── Vector.Native.dll (P/Invoke)

Vector.Service
    ├── Vector.Core
    ├── NAudio
    ├── Vosk
    └── Piper (external process)

Vector.Core
    ├── Microsoft.SemanticKernel
    ├── OllamaSharp
    ├── Microsoft.Data.Sqlite
    └── System.Text.Json

Vector.Native
    ├── DirectX 11 (d3d11.lib)
    └── D3DCompiler
```

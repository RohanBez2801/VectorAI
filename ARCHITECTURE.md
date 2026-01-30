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
          │  │ ScreenCapture Loop  │──┼──► LLaVA Vision
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
          │  │ MoodManager         │──┼──► Sentiment → Visual State
          │  └─────────────────────┘  │
          │  ┌─────────────────────┐  │
          │  │ Plugins (7x)        │──┼──► Shell, File, Memory, etc.
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
- Exposes events for UI binding (`OnReply`, etc.)
- Uses `InitAsync` pattern for async dependency injection

#### MoodManager.cs
- Emotional state machine (`VectorMood` enum)
- **5 Moods:** Neutral, Calculating, Amused, Concerned, Hostile
- Triggers on:
  - Audio RMS thresholds (volume-based reaction)
  - Text sentiment keywords
- Fires `OnMoodChanged` event → UI updates face

#### Plugins/
| Plugin | Functions | Safety |
|--------|-----------|--------|
| `ShellPlugin` | `Execute(command)` | Requires approval |
| `FileSystemPlugin` | `ReadFile`, `WriteFile`, `DeleteFile` | Requires approval |
| `MemoryPlugin` | `Save`, `Recall`, `Search` | No approval needed |
| `DeveloperConsolePlugin` | `BuildProject`, `GetBuildErrors`, `PatchFile` | Requires approval |
| `MathPlugin` | `Calculate`, `VectorMath`, `Calculus` | No approval |
| `ComputerSciencePlugin` | `BaseConvert`, `ComputeHash`, `BitwiseOp`, `DataUnitConvert` | No approval |
| `WebSearchPlugin` | `SearchAsync` | No approval |

### B. Vector.HUD (The Face)
WPF application that hosts the Brain and renders the interface.

#### MainWindow.xaml(.cs)
- Chat interface (message display + input)
- UDP listener on port 9999 for audio RMS telemetry
- System heartbeat loop (health monitoring)
- Integrates VectorBrain and routes speech input
- RMS Graph visualization (Polyline rendering)

#### HolographicFace.xaml(.cs)
- Interops with `Vector.Native.dll` (C++ DX11)
- Calls `InitVectorEngine()`, `RenderFace()`, `UpdateMood()`
- WriteableBitmap for CPU-to-GPU buffer transfer
- Automatic blink timer + lip-sync from audio RMS
- Mood-to-visual mapping (color, spikes, confusion)

#### ApprovalWindow.xaml(.cs)
- Modal dialog for HITL safety checks
- Side-by-side diff view for file modifications
- Clear command display for shell requests
- Returns `true/false` to plugin callback

### C. Vector.Service (The Body)
Background worker for always-on capabilities.

#### Worker.cs
- Orchestrates multiple concurrent loops:
  - **Heartbeat Loop** (~30Hz) — UDP RMS broadcast
  - **Vision Loop** (~3s) — Screen capture → LLaVA
  - **Listen Loop** — Vosk STT with silence detection
- Integrates with MainWindow via Worker attachment

#### MicrophoneListener.cs
- Uses NAudio for audio capture
- Vosk model for offline speech recognition
- Calculates RMS for volume visualization
- Sends transcriptions to VectorBrain for processing

#### PiperVoiceService.cs
- Local neural TTS via Piper executable
- Model: `en_US-ryan-medium.onnx` (22050Hz/16-bit)
- Pipes text → PCM audio → NAudio playback

### D. Vector.Native (The Skin)
C++ DirectX 11 DLL for GPU-accelerated rendering.

#### dllmain.cpp
- Creates D3D11 device and render target
- Generates 900-point spherical mesh (face geometry)
- HLSL vertex/pixel shaders with:
  - Heartbeat pulse animation
  - Eye blink morph (Y-axis squish)
  - Mouth open animation
  - Spike deformation based on mood
  - Confusion wobble effect
  - Mood-based color interpolation
- Exports: `InitVectorEngine`, `UpdateMood`, `RenderFace`

## 3. The Safety Protocol (HITL)
Integration between Core and HUD via **Dependency Injection of Callbacks**.

```
1. User asks: "Delete system32."
2. LLM generates a plan to call ShellPlugin.Execute("rm -rf ...")
3. Kernel invokes the function
4. ShellPlugin triggers _approvalCallback
5. MainWindow intercepts, switches to UI Thread, launches ApprovalWindow
6. User sees the alert → clicks "Cancel"
7. Plugin returns "ABORTED" to the LLM
8. LLM apologizes to the user
```

## 4. Memory & Data Flow
```
1. INPUT: User text → Nomic-Embed embedding
2. SEARCH: SQLite cosine similarity query (threshold > 0.6)
3. CONTEXT: Relevant memories injected into System Prompt
4. INFERENCE: Llama 3 receives prompt + memories → generates response
5. LEARN: Optional save of new facts to vector store
```

## 5. Telemetry (The Nervous System)

### Audio Pipeline
```
MicrophoneListener (NAudio) 
    → RMS Calculation
    → UDP Broadcast to localhost:9999
    → MainWindow UDP Listener
    → Polyline Graph (60fps capped)
    → HolographicFace lip-sync
```

### Vision Pipeline
```
Worker.RunVisionLoop (every 3s)
    → Screen Capture (System.Drawing)
    → Base64 encode
    → LLaVA inference (Ollama)
    → Response routed to MainWindow chat
```

### Mood Pipeline
```
User Input (text or audio) 
    → MoodManager.AnalyzeSentimentAsync
    → Keyword matching (hostile, concerned, amused, calculating)
    → OnMoodChanged event
    → HolographicFace.SetMood(r, g, b, spike, confusion)
    → Native UpdateMood → GPU shader constants
```

## 6. Native Rendering Pipeline
```
1. HolographicFace.OnRendering (WPF CompositionTarget)
2. Lock WriteableBitmap backbuffer
3. Call RenderFace(time, blink, mouth, buffer) via P/Invoke
4. C++ clears render target, updates constant buffer
5. Draw 900 vertices as point cloud
6. Copy staging texture → CPU buffer
7. Unlock bitmap, mark dirty rect
8. WPF renders at vsync
```

## 7. Project Dependencies

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
    └── System.Numerics.Tensors

Vector.Native
    ├── DirectX 11 (d3d11.lib)
    └── D3DCompiler
```

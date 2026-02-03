# VECTOR: Local Synthetic Intelligence (SI)

**Version:** 2.1.0 (Boundary Update)
**Framework:** .NET 10 (WPF + Semantic Kernel)  
**Local Inference:** Ollama (Llama 3, LLaVA, Nomic-Embed)  
**Graphics Engine:** DirectX 11 Native C++ Rendering  
**Voice:** Piper TTS + Vosk STT (Fully Offline)

![Status](https://img.shields.io/badge/System-ONLINE-00FFD1) ![Safety](https://img.shields.io/badge/Safety-Active-green) ![Voice](https://img.shields.io/badge/Voice-Piper%20TTS-purple)

## 👁️ Overview
VECTOR is a **Synthetic Intelligence** designed to run locally on Windows 11. Unlike standard chatbots, VECTOR is deeply integrated into the host operating system. It possesses:
- **Hands** — File I/O, Shell execution, Developer Tools
- **Ears** — Offline Speech-to-Text (Vosk) + UDP Audio Telemetry
- **Voice** — Offline Text-to-Speech (Piper TTS)
- **Eyes** — Screen Analysis with Visual Attention (Delta Detection + ROI)
- **Memory** — Stratified Memory System (Working/Episodic/Semantic/Procedural)
- **Face** — GPU-Accelerated Holographic Head (DirectX 11 C++ DLL)
- **Emotions** — Real-time Mood System with Visual Feedback
- **Self-Model** — Persistent internal state with confidence tracking
- **Reflection** — Meta-cognitive loop for self-improvement
- **Safety Layer** — Intent classification with Block/Flag/Allow decisions

Crucially, VECTOR implements a **Human-in-the-Loop (HITL)** architecture for all high-risk operations. It cannot modify files or execute system commands without explicit, interactive user approval.

## ⚡ Key Capabilities

### 🧠 The Brain (Core Intelligence)
- Powered by **Microsoft Semantic Kernel** with automatic function calling
- Uses **Llama 3** (via Ollama) for reasoning and conversation
- Uses **Nomic-Embed-Text** for long-term memory encoding
- Uses **LLaVA** for visual screen analysis (with delta detection)
- **Planning Service** — Chain-of-thought task decomposition (P-V-E-R pipeline)
- **Mood Manager** — Sentiment analysis influences visual state

### 🧬 Self-Model & Reflection (NEW in v2.0)
- **SelfState** — Tracks `ActiveTask`, `TaskPhase`, `Confidence`, `LastError`
- **Reflection Loop** — Post-interaction analysis with success scoring
- **Working Memory** — Short-term context buffer (visual, reflections)

### 🛡️ The Conscience (Safety System)
VECTOR is capable of dangerous operations. To prevent catastrophe:
- **Intent Classifier** — Categorizes requests as Benign/Sensitive/Dangerous
- **Safety Guard** — Evaluates Block/Flag/Allow decisions
- **Task Governor** — Loop detection and command blacklisting
- **Two-Phase Commit** — Cryptographic verification of Action Data + Visual State
- **Approval Window** — A dedicated WPF modal intercepts all high-risk kernel functions
- **Diff View** — Users see a side-by-side "Old vs New" comparison before allowing file writes
- **User Confirmation** — Flagged actions require explicit approval

### 💾 Stratified Memory System (NEW in v2.0)
| Tier | Purpose | Persistence |
|------|---------|-------------|
| **Working** | Short-term context (visual, reflections) | In-memory (FIFO) |
| **Episodic** | Task/conversation summaries | JSON file |
| **Semantic** | User facts and knowledge | SQLite + Nomic-Embed |
| **Procedural** | How-to guides and procedures | SQLite + Nomic-Embed |

### �️ Visual Attention (NEW in v2.0)
- **Delta Detection** — Skips unchanged frames (SHA256 hash comparison)
- **ROI Extraction** — Focuses on key screen regions
- **Downsampling** — Resizes frames for faster LLaVA processing

### 📊 Observability (NEW in v2.0)
- **Structured Logging** — JSON Lines format to `%LOCALAPPDATA%\VectorAI\logs\`
- **Telemetry Metrics** — Latency tracking, error counts, request aggregation
- **Decision Logging** — Safety decisions, plans, reflections all recorded

### 🎛️ The HUD (Heads-Up Display)
- **GPU-Rendered Face:** DirectX 11 holographic head with particle/sphere rendering
- **Emotional States:** Visual mood indicators (color, spikes, confusion effects)
- **Real-time Visualization:** Rolling graph of audio RMS levels via UDP (Port 9999)
- **System Health:** Autonomic monitoring of the LLM, Database, and Sentinel sensors
- **Lip-Sync:** Mouth movement responds to audio input RMS

### 🗣️ Voice System (Fully Offline)
- **Piper TTS:** Local neural text-to-speech synthesis (22kHz/16-bit)
- **Vosk STT:** Offline speech recognition with silence detection
- **Lip-Sync Integration:** Speech output triggers mouth animations

### 🔧 Plugins (Semantic Kernel Functions)

| Plugin | Purpose |
|--------|---------|
| `ShellPlugin` | Execute terminal commands (cmd/powershell) |
| `FileSystemPlugin` | Read/Write/Delete files |
| `MemoryPlugin` | Long-term fact storage & recall |
| `DeveloperConsolePlugin` | Build projects, parse errors, patch code |
| `MathPlugin` | Advanced math (scalar, vector algebra, calculus) |
| `ComputerSciencePlugin` | Base conversion, hashing, bitwise ops, unit conversion |
| `WebSearchPlugin` | Query local search endpoint |

---

## 🚀 Getting Started

### Prerequisites
1. **Ollama** installed and running (`localhost:11434`)
2. **Models Pulled:**
    ```powershell
    ollama pull llama3
    ollama pull llava
    ollama pull nomic-embed-text
    ```
3. **.NET 10 SDK**
4. **Visual Studio 2022** (with C++/Desktop workload for Vector.Native)
5. **Piper TTS** (auto-downloaded to `Vector.Service/piper/`)
6. **Vosk Model** (place in `Vector.Service/vosk-model/`)

### Installation
1. Clone the repository
2. Build the solution:
    ```powershell
    dotnet build Vector.slnx
    ```
3. Build the Native DLL (if not using MSBuild auto-build):
    ```powershell
    msbuild Vector.Native\Vector.Native.vcxproj /p:Configuration=Release /p:Platform=x64
    ```
4. Run the HUD:
    ```powershell
    dotnet run --project Vector.HUD
    ```
5. (Optional) Run the Service for voice + vision:
    ```powershell
    dotnet run --project Vector.Service
    ```

---

## 📂 Project Structure

| Project | Description |
|---------|-------------|
| **`Vector.Core`** | The "Brain" — Semantic Kernel, Plugins, Services, Safety Layer |
| **`Vector.HUD`** | The "Face" — WPF application, Holographic Face, Safety Dialogs |
| **`Vector.Service`** | The "Body" — Background worker, Voice, Vision, Visual Attention |
| **`Vector.Native`** | The "Skin" — DirectX 11 C++ DLL for GPU-accelerated rendering |

### Key Files
```
Vector.Core/
├── VectorBrain.cs           # Main orchestrator (Semantic Kernel host)
├── MoodManager.cs           # Emotional state machine + sentiment analysis
├── Models/
│   ├── SelfState.cs         # Agent internal state model
│   └── ReflectionModels.cs  # Reflection context and results
├── Services/
│   ├── SelfStateService.cs  # Persistent state management
│   ├── ReflectionService.cs # Post-interaction analysis
│   ├── PlanningService.cs   # Chain-of-thought planning
│   ├── TaskGovernor.cs      # Loop detection + safety limits
│   ├── MemoryService.cs     # Stratified memory management
│   ├── IntentClassifier.cs  # Intent categorization
│   ├── SafetyGuard.cs       # Block/Flag/Allow decisions
│   ├── VectorLogger.cs      # Structured JSON logging
│   └── TelemetryService.cs  # Latency and error tracking
├── Plugins/
│   ├── ShellPlugin.cs       # Terminal command execution
│   ├── FileSystemPlugin.cs  # File I/O operations
│   ├── MemoryPlugin.cs      # RAG memory interface
│   ├── DeveloperConsolePlugin.cs  # Build, patch, error parsing
│   ├── MathPlugin.cs        # Advanced math + calculus
│   ├── ComputerSciencePlugin.cs   # Conversions, hashing
│   └── WebSearchPlugin.cs   # Local search endpoint

Vector.HUD/
├── MainWindow.xaml(.cs)     # Main UI + UDP listener
├── HolographicFace.xaml(.cs)# Native interop for GPU face
├── ApprovalWindow.xaml(.cs) # Safety modal for HITL

Vector.Service/
├── Worker.cs                # Background service orchestrator
├── MicrophoneListener.cs    # Vosk STT + audio processing
├── PiperVoiceService.cs     # Local neural TTS
├── VisualAttentionService.cs# Delta detection + ROI extraction

Vector.Native/
├── dllmain.cpp              # DirectX 11 holographic sphere
```

## ⚠️ Security Notice
This software allows an LLM to execute code on your machine. The **Safety Layer** (IntentClassifier + SafetyGuard + ApprovalWindow) provides multi-tier protection. Never disable the safety callbacks in `VectorBrain.cs` unless you are running in a sandboxed environment.

---

## 🎨 Mood System
VECTOR's face changes based on emotional state:

| Mood | Color | Effects |
|------|-------|---------|
| Neutral | Cyan | Calm, minimal animation |
| Calculating | Indigo/Purple | Fast pulse, low spikes |
| Amused | Gold | Smooth, slight confusion wobble |
| Concerned | Orange/Red | Medium spikes |
| Hostile | Red | High spikes, confusion distortion |

---

*Property of the User. Do not distribute without authorization.*

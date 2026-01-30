# Changelog

All notable changes to VECTOR are documented in this file.

---

## [v2.0.0] - 2026-01-30 (The "Evolution" Update)

### ✨ New Features

#### Phase 1: Self-Model & State
- **SelfState Model:** Tracks `ActiveTask`, `TaskPhase`, `Confidence`, `LastError`, `Mood`
- **SelfStateService:** Persistent JSON-based state management
- **MoodManager Integration:** Mood changes sync to SelfState

#### Phase 2: Reflection Loop
- **ReflectionService:** Post-interaction analysis via LLM
- **ReflectionModels:** `ReflectionContext` and `ReflectionResult` for structured analysis
- **Meta-cognition:** Agent evaluates its own responses with success scoring

#### Phase 3: Task Planner & Governor
- **PlanningService:** Chain-of-thought task decomposition
- **TaskGovernor:** Loop detection (max 3 repetitions) and command blacklisting
- **P-V-E-R Pipeline:** Plan → Validate → Execute → Reflect workflow

#### Phase 4: Memory Stratification
- **MemoryService:** Unified interface for all memory tiers
- **Working Memory:** In-memory FIFO buffer (~10 items)
- **Episodic Memory:** JSON-persisted task/conversation summaries
- **Semantic Memory:** SQLite + Nomic-Embed for user facts
- **Procedural Memory:** SQLite + Nomic-Embed for how-to guides

#### Phase 5: Visual Attention
- **VisualAttentionService:** Optimized vision processing
- **Delta Detection:** SHA256 hash comparison skips unchanged frames
- **ROI Extraction:** Focuses on key screen regions (center, top-right)
- **Downsampling:** Resizes frames to 800px for faster LLaVA

#### Phase 6: Safety & Intent
- **IntentClassifier:** Categorizes input as Benign/Sensitive/Dangerous
- **SafetyGuard:** Evaluates Block/Flag/Allow decisions
- **HITL Flow:** Flagged actions require explicit user confirmation
- **Dangerous Keywords:** Blocks `rm -rf`, `format`, `delete all`, etc.

#### Phase 7: Observability
- **VectorLogger:** Structured JSON logging to `%LOCALAPPDATA%\VectorAI\logs\`
- **TelemetryService:** Latency tracking, error counts, request aggregation
- **Decision Logging:** Safety decisions, plans, reflections all recorded

### 🛠️ Technical Improvements
- **VectorBrain overhaul:** Integrated all 7 services with proper DI
- **Safety-first architecture:** Safety check runs BEFORE planning
- **Optimized vision loop:** 5s intervals with delta detection
- **Improved logging:** All key decisions are logged in JSONL format

### 📊 New Metrics
- Request latency (average, max)
- Error rate tracking
- Structured event types: DECISION, PLAN, REFLECTION, ERROR

---

## [v1.1.0] - 2026-01-28 (The "Embodiment" Update)

### ✨ New Features
- **Native GPU Rendering:** Replaced software renderer with DirectX 11 C++ DLL (`Vector.Native`)
  - Hardware-accelerated holographic sphere with 900 vertices
  - HLSL shaders with heartbeat animation, blink, and mouth morph
  - Spike deformation and confusion wobble effects for emotional states
- **Piper TTS Integration:** Added fully offline text-to-speech via Piper
  - Neural voice model (`en_US-ryan-medium.onnx`)
  - 22kHz/16-bit audio output with NAudio playback
- **Vosk STT Integration:** Offline speech recognition via Vosk
  - Silence detection for natural turn-taking
  - Device enumeration and selection support
- **Mood System:** Complete emotional state machine
  - 5 moods: Neutral, Calculating, Amused, Concerned, Hostile
  - Audio RMS and text sentiment triggers
  - Real-time visual feedback via color/spike/confusion parameters
- **Developer Console Plugin:** New plugin for coding assistance
  - `BuildProject` — Runs MSBuild/dotnet build with output parsing
  - `GetBuildErrors` — Extracts structured error information
  - `PatchFile` — Search-and-replace with diff approval
- **Advanced Math Plugin:** Extended mathematical capabilities
  - Vector algebra (dot product, cross product, magnitude)
  - Numerical calculus (derivative, integral)
  - Full expression parser with variables support
- **Computer Science Plugin:** New utility functions
  - Base conversion (binary, octal, decimal, hex)
  - Cryptographic hashing (MD5, SHA1, SHA256, SHA512)
  - Bitwise operations (AND, OR, XOR, NOT, shifts)
  - Data unit conversion (B, KB, MB, GB, TB)

### 🛠️ Technical Improvements
- **Dual-Loop Architecture:** Separated fast heartbeat loop (~30Hz) from slow vision loop (~3s)
- **Worker Attachment:** Service can now attach to MainWindow for integrated STT routing
- **P/Invoke Interop:** Clean C#/C++ boundary via `Vector.Native.dll` exports
- **Mood Event System:** `OnMoodChanged` event propagates emotional state across components

### 🐛 Bug Fixes
- Fixed DirectX 11 compatibility issues with DX12-only systems
- Fixed Piper binary path resolution at runtime
- Fixed native buffer copy race condition in rendering loop
- Fixed cross-thread exception in mood update dispatch

---

## [v1.0.0] - 2026-01-22 (The "Sentience" Update)

### ✨ New Features
- **Long-Term Memory:** Implemented RAG (Retrieval-Augmented Generation) using SQLite and Semantic Kernel Memory. VECTOR now remembers facts across sessions.
- **System Control:** Added `ShellPlugin` allowing the AI to execute terminal commands (cmd/powershell).
- **Visual Context:** Restored LLaVA integration. The system can now "see" screens or images provided via Base64.
- **Safety Overhaul:**
  - Implemented `ApprovalWindow.xaml` for File Writes and Shell Commands.
  - Added "Old vs New" Diff View for file modifications.
  - Added Sarcastic/Personality headers to safety dialogs.

### 🛠️ Technical Improvements
- **Refactored Brain:** `VectorBrain.cs` now uses `InitAsync` pattern to accept safety callbacks cleanly.
- **Debounced Health Checks:** System status indicators (Brain, Sentinel, Core) now resist flickering via a failure threshold counter.
- **Thread Safety:** Fixed cross-thread exception risks in the RMS Graph rendering and Approval Dialogs.

### 🐛 Bug Fixes
- Fixed issue where `ProcessVisualInputAsync` was overwritten during refactoring.
- Fixed database connection string handling for SQLite.
- Resolved UDP Listener race conditions by locking the history queue.

---

## [v0.9.0] - 2026-01-15 (Initial Alpha)

### ✨ New Features
- Core Semantic Kernel integration with Ollama
- Basic chat interface via WPF
- UDP audio telemetry visualization
- Initial plugin architecture (Shell, File, Memory)

---

## Roadmap

### Completed ✅
- [x] Self-Model & State (Phase 1)
- [x] Reflection Loop (Phase 2)
- [x] Task Planner & Governor (Phase 3)
- [x] Memory Stratification (Phase 4)
- [x] Visual Attention (Phase 5)
- [x] Safety & Intent (Phase 6)
- [x] Observability (Phase 7)

### Planned Features
- [ ] Multi-model orchestration (specialized models per task)
- [ ] Plugin hot-reload system
- [ ] Voice activity detection improvements
- [ ] Custom wake word detection
- [ ] Facial expression presets (animated transitions)
- [ ] Local web search via SearXNG integration
- [ ] Code execution sandbox (safe Python/JS runtime)

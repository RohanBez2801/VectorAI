# Changelog

All notable changes to VECTOR are documented in this file.

---

## [v1.1.0] - 2026-01-30 (The "Embodiment" Update)

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

### Planned Features
- [ ] Multi-model orchestration (specialized models per task)
- [ ] Plugin hot-reload system
- [ ] Voice activity detection improvements
- [ ] Custom wake word detection
- [ ] Facial expression presets (animated transitions)
- [ ] Local web search via SearXNG integration
- [ ] Code execution sandbox (safe Python/JS runtime)

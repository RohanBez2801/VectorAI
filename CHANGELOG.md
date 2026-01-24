# Changelog

## [v1.0.0] - 2026-01-22 (The "Sentience" Update)

### ✨ New Features
- **Long-Term Memory:** Implemented RAG (Retrieval-Augmented Generation) using SQLite and Semantic Kernel Memory. VECTOR now remembers facts across sessions.
- **System Control:** Added `ShellPlugin` allowing the AI to execute terminal commands (cmd/powershell).
- **Visual Context:** Restored LLaVA integration. The system can now "see" screens or images provided via Base64.
- **Safety Overhaul:** - Implemented `ApprovalWindow.xaml` for File Writes and Shell Commands.
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

# System Architecture

## 1. High-Level Design
VECTOR follows a **Hub-and-Spoke** architecture where the `VectorBrain` (Core) acts as the orchestrator, connected to the `MainWindow` (HUD) for I/O and Safety, and `Ollama` for inference.



## 2. Component Breakdown

### A. Vector.Core (The Logic)
This is a standard .NET Class Library containing the intelligence logic.
- **`VectorBrain.cs`**: The main entry point. It builds the Semantic Kernel and registers plugins. It does *not* depend on UI libraries directly.
- **`Plugins/`**:
    - `ShellPlugin.cs`: Wrapper for `System.Diagnostics.Process`. Requires `Func<ShellCommandRequest, Task<bool>>` callback.
    - `FileSystemPlugin.cs`: Wrapper for `System.IO`. Requires `Func<FileWriteRequest, Task<bool>>` callback.
    - `MemoryPlugin.cs`: Interface for `ISemanticTextMemory`. Stores embeddings in `vector_memory.sqlite`.

### B. Vector.HUD (The Interface)
A WPF application that hosts the Brain.
- **`MainWindow.xaml`**: Handles the event loop, UDP listening (Port 9999), and System Heartbeats (checking if Ollama/DB are alive).
- **`ApprovalWindow.xaml`**: A modal dialog that pauses the Thread execution of the Brain to await user input (Confirm/Cancel).

### C. The Safety Protocol (HITL)
The integration between Core and HUD is achieved via **Dependency Injection of Callbacks**.

1.  User asks: *"Delete system32."*
2.  **LLM** generates a plan to call `ShellPlugin.Execute("rm -rf ...")`.
3.  **Kernel** invokes the function.
4.  **ShellPlugin** triggers `_approvalCallback`.
5.  **MainWindow** intercepts the callback, switches to the UI Thread, and launches `ApprovalWindow`.
6.  **User** sees the alert. If they click "Cancel", the plugin returns "ABORTED" to the LLM.
7.  **LLM** apologizes to the user.

## 3. Memory & Data Flow
1.  **Input:** User text is converted to an embedding (vector) using `nomic-embed-text`.
2.  **Search:** SQLite is queried for vectors with Cosine Similarity > 0.6.
3.  **Context Injection:** Relevant memories are pasted into the System Prompt.
4.  **Inference:** Llama 3 receives the prompt + memories and generates a response.

## 4. Telemetry (The Nervous System)
- **Audio:** `MicrophoneListener` (in Service) calculates RMS.
- **Transport:** RMS is sent via UDP to `localhost:9999` as `TIMESTAMP|RMS`.
- **Visuals:** HUD receives UDP packets and renders a `Polyline` graph at 60fps (clamped to monitor refresh rate).

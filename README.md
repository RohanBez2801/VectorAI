# VECTOR: Local Synthetic Intelligence (SI)

**Version:** 1.0.0 (Alpha)  
**Framework:** .NET 10 (WPF + Semantic Kernel)  
**Local Inference:** Ollama (Llama 3, LLaVA, Nomic-Embed)

![Status](https://img.shields.io/badge/System-ONLINE-00FFD1) ![Safety](https://img.shields.io/badge/Safety-Active-green)

## 👁️ Overview
VECTOR is a **Synthetic Intelligence** designed to run locally on Windows 11. Unlike standard chatbots, VECTOR is integrated into the host operating system. It possesses "Hands" (File I/O, Shell execution), "Ears" (UDP Audio Telemetry), "Eyes" (Screen Analysis), and "Memory" (RAG/Vector Database).

Crucially, VECTOR implements a **Human-in-the-Loop (HITL)** architecture for all high-risk operations. It cannot modify files or execute system commands without explicit, sarcastic, interactive user approval.

## ⚡ Key Capabilities

### 🧠 The Brain (Core Intelligence)
- Powered by **Microsoft Semantic Kernel**.
- Uses **Llama 3** (via Ollama) for reasoning and conversation.
- Uses **Nomic-Embed-Text** for long-term memory encoding.
- Uses **LLaVA** for visual screen analysis.

### 🛡️ The Conscience (Safety System)
VECTOR is capable of dangerous operations (e.g., executing shell commands, overwriting files). To prevent catastrophe:
- **Approval Window:** A dedicated WPF modal intercepts all high-risk kernel functions.
- **Diff View:** Users see a side-by-side "Old vs New" comparison before allowing file writes.
- **Command Vetting:** Shell commands are presented clearly for authorization.
- **Debounce Logic:** Prevents accidental double-confirmations.

### 💾 Long-Term Memory (RAG)
- **SQLite Vector Store:** Stores user facts and conversations locally.
- **Recall:** Automatically queries the database for relevant context before answering questions (e.g., "Vector, what is my API key?" or "Vector, what did we discuss about Project X?").

### 🎛️ The HUD (Heads-Up Display)
- **Real-time Visualization:** Rolling graph of audio RMS levels received via UDP (Port 9999).
- **System Health:** Autonomic monitoring of the LLM, Database, and Sentinel sensors.
- **Transparent Overlay:** WPF window with click-through transparency support (optional).

---

## 🚀 Getting Started

### Prerequisites
1.  **Ollama** installed and running (`localhost:11434`).
2.  **Models Pulled:**
    ```powershell
    ollama pull llama3
    ollama pull llava
    ollama pull nomic-embed-text
    ```
3.  **.NET 8 SDK**.
4.  **Visual Studio 2022** (Recommended).

### Installation
1.  Clone the repository.
2.  Build the solution:
    ```powershell
    dotnet build Vector.sln
    ```
3.  Run the HUD:
    ```powershell
    dotnet run --project Vector.HUD
    ```

### Usage
- **Talk:** Type in the chat window (or use the microphone service if running) to interact.
- **Commands:** - *"Create a file called plan.txt with a list of world domination steps."* (Triggers File Plugin)
    - *"Open Notepad."* (Triggers Shell Plugin)
    - *"Remember that I prefer dark mode."* (Triggers Memory Plugin)

---

## 📂 Project Structure
- **`Vector.Core`**: The "Brain." Contains the Semantic Kernel configuration, Plugins (File, Shell, Memory), and LLM connectors.
- **`Vector.HUD`**: The "Face." WPF application handling the UI, Graphing, and Safety Dialogs.
- **`Vector.Service`**: The "Body." Background worker for Microphone listening and UDP broadcasting.

## ⚠️ Security Notice
This software allows an LLM to execute code on your machine. While the **ApprovalWindow** is a robust gatekeeper, never disable the safety callbacks in `VectorBrain.cs` unless you are running in a sandboxed environment.

---
*Property of the User. Do not distribute without authorization.*

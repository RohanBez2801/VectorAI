# VECTOR-STEWARD JOURNAL

This journal tracks safety-critical decisions, edge cases, and trust risks encountered during V2.1 development.

## 2024-05-22 – [Visual Verification Gap]

**Learning:** The Two-Phase Commit protocol for sensitive actions (shell execution) was missing a critical component: Visual State Verification. The system verified *what* was being executed (command hash) but not *context* (visual state). This allowed for potential 'UI spoofing' or context switching attacks where a user might approve an action based on stale or misleading visual information.

**Risk:** A malicious or confused user/process could trigger a shell command while the visual context changes (e.g., a benign window is replaced by a sensitive one, or the user switches context), leading to unintended execution.

**Mitigation:** Implementing `IVisualStateProvider` and `WindowsVisualStateProvider` to capture a cryptographic hash of the primary screen at the moment of request. This hash is verified again at the moment of execution. If the screen has changed significantly (different hash), the action is aborted. This ensures the user is approving exactly what they see.

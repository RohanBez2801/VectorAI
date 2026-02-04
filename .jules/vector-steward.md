## 2025-02-17 – ActionPolicy Bypass in Sensitive Plugins

**Learning:** We implemented `TaskGovernor` with blacklists (e.g., `rm -rf`), but `ShellPlugin` bypasses it by going straight to User Approval. Users might blindly approve a complex command that contains a dangerous flag.
**Risk:** A "Two-Phase Commit" failure where the automated check (Phase 1) is skipped, relying 100% on the human (Phase 2), which increases cognitive load and risk of social engineering (e.g., "Just run this cleanup script").
**Mitigation:** Enforce `ITaskGovernor.ValidateAction()` inside `ShellPlugin.ExecuteCommandAsync()` before triggering the user approval callback.

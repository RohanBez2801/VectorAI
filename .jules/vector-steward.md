# VECTOR-STEWARD JOURNAL 📓

## 2026-01-30 – [ActionPolicy Enforcement Gap in ShellPlugin]

**Learning:** We discovered that `ShellPlugin` was relying solely on user confirmation (HITL) without pre-validating the command against the `TaskGovernor` or `ActionPolicy`. This meant a user could accidentally approve a blacklisted command (like `rm -rf /`) if they weren't paying attention. Trust requires that VECTOR never even *asks* to do something it knows is dangerous.

**Risk:** A fatigue-induced "Yes" click could have wiped the file system. The `SafetyGuard` only checked the user's natural language input, but the Planner could theoretically hallucinate a dangerous command even from a benign request, or the user might be tricked into running a dangerous command that the Planner generates.

**Mitigation:** We are injecting `ITaskGovernor` directly into `ShellPlugin`. The plugin now validates the *actual generated command string* against the Governor's policy before the user is ever prompted. If the Governor denies it, the action is blocked immediately.

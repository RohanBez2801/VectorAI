## 2026-02-12 – [ShellPlugin Governor Bypass]

**Learning:** The  relied solely on Human-in-the-Loop (HITL) approval, bypassing the automated  policies (blacklist/loop detection). This meant a user could be tricked into approving a blacklisted command, or the agent could spam commands without automated throttling.

**Risk:** High. A malicious prompt could bypass the  blacklist if the user blindly approves the action.

**Mitigation:** Injected  into  and enforced  before requesting user approval. This ensures automated policies are applied first.

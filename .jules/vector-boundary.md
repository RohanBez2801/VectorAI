# VECTOR-BOUNDARY JOURNAL

## 2024-05-22: Scope Expansion Detected

### Trigger 1: Scope Expansion / Mismatch
- **Observation**: Found unauthorized project `Vector.Server` containing a SignalR/OpenAPI application.
- **Analysis**: This project exposes `Vector.Core` functionality over the network via HTTP/WebSockets. This directly contradicts the "Local Synthetic Intelligence" designation in `README.md` and `ARCHITECTURE.md`.
- **Risk**:
    - **Remote Execution**: Allows remote users to trigger `ChatAsync` and execute commands.
    - **Bypass of Presence**: Circumvents the physical "Approval Window" guarantee (WPF modal).
    - **Insecure Default**: Initializes `VectorBrain` without a visual state provider.
- **Action**: FLAGGED FOR REMOVAL. The project is not part of the `Vector.slnx` solution and represents dead code that poses a security risk.

### Trigger 2: Safety Downgrade / Mismatch
- **Observation**: `Vector.Core/Services/VectorVerifier.cs` does not implement `ComputeVisualHashAsync` or verify visual state, contrary to memory/documentation claims.
- **Analysis**: The `IVectorVerifier` implementation only checks the action data hash, not the screen context.
- **Risk**: "Two-Phase Commit" (Visual Verification) is effectively disabled, allowing actions to proceed without verifying the user is seeing the correct context.
- **Action**: FLAGGED. This regression degrades the safety guarantees of V2.1. Immediate action is to remove the primary threat (`Vector.Server`), but `Vector.Core` requires future remediation to restore Visual Verification.

using System.Threading.Tasks;

namespace Vector.Core.Services;

/// <summary>
/// Provides visual state verification capabilities by capturing and hashing the screen content.
/// </summary>
public interface IVisualStateProvider
{
    /// <summary>
    /// Captures the current visual state (e.g., primary screen) and returns a cryptographic hash.
    /// If capture fails, it should return a specific error code string (e.g., "VISUAL_CAPTURE_FAILED").
    /// </summary>
    /// <returns>A SHA256 hash of the screen state.</returns>
    Task<string> CaptureVisualStateHashAsync();
}

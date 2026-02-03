using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVisualStateProvider
{
    /// <summary>
    /// Captures the current visual state (e.g., screen content) and returns a hash.
    /// Returns null if capture is not possible.
    /// </summary>
    Task<string?> CaptureStateAsync();

    /// <summary>
    /// Returns the confidence level of the visual state provider (0.0 to 1.0).
    /// </summary>
    Task<float> GetConfidenceAsync();
}

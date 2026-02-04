using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVisualStateProvider
{
    /// <summary>
    /// Captures the current visual state of the system (e.g., screenshot hash)
    /// to anchor actions to what the user is seeing.
    /// </summary>
    /// <returns>A SHA256 hash string of the visual state.</returns>
    Task<string> CaptureVisualHashAsync();
}

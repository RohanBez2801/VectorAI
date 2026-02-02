using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVisualStateProvider
{
    /// <summary>
    /// Captures the current state of the visual environment (e.g. screen hash).
    /// </summary>
    Task<string> GetCurrentStateHashAsync();
}

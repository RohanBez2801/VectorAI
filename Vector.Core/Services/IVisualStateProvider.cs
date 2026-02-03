using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVisualStateProvider
{
    /// <summary>
    /// Captures the current visual state (e.g., screenshot) as a byte array.
    /// </summary>
    Task<byte[]> CaptureStateAsync();
}

using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVisualStateProvider
{
    Task<string> CaptureVisualStateAsync();
}

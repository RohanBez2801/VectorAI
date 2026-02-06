using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVisualStateProvider
{
    Task<string> CaptureVisualStateAsync();
    Task<byte[]> CaptureScreenAsync();
}

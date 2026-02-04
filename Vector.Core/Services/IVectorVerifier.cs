using System;
using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IVectorVerifier
{
    Task<string> ComputeActionHashAsync<T>(T data);
    Task<string> ComputeVisualStateHashAsync();
    Task VerifyActionAsync<T>(T data, string originalActionHash, string originalVisualHash, DateTime timestamp);
}

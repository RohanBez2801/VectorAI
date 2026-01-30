using System.Threading.Tasks;
using Vector.Core.Models;

namespace Vector.Core.Services;

public interface IReflectionService
{
    Task<ReflectionResult> ReflectAsync(ReflectionContext context);
}

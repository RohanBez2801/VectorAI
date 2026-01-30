using System.Threading.Tasks;

namespace Vector.Core.Services;

public interface IPlanningService
{
    Task<string> CreatePlanAsync(string userGoal);
}

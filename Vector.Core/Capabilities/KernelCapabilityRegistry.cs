using System.Reflection;
using Vector.Core.Attributes;

namespace Vector.Core.Capabilities;

public sealed class KernelCapabilityRegistry
{
    private readonly Dictionary<string, KernelFunctionDescriptor> _functions = new();

    public KernelCapabilityRegistry(Assembly assembly)
    {
        RegisterFromAssembly(assembly);
    }

    private void RegisterFromAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = method.GetCustomAttribute<KernelFunctionAttribute>();
                if (attr == null) continue;

                var key = $"{type.Name}.{method.Name}";

                _functions[key] = new KernelFunctionDescriptor
                {
                    Name = attr.Name,
                    Description = attr.Description,
                    Category = attr.Category,
                    Risk = attr.Risk,
                    Cost = attr.Cost,
                    RequiresUserApproval = attr.RequiresUserApproval,
                    IsReversible = attr.IsReversible,
                    Preconditions = attr.Preconditions,
                    Method = method
                };
            }
        }
    }

    public IReadOnlyDictionary<string, KernelFunctionDescriptor> Functions => _functions;
}

public sealed class KernelFunctionDescriptor
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public CapabilityCategory Category { get; init; }
    public RiskLevel Risk { get; init; }
    public int Cost { get; init; }
    public bool RequiresUserApproval { get; init; }
    public bool IsReversible { get; init; }
    public string[] Preconditions { get; init; } = Array.Empty<string>();
    public MethodInfo Method { get; init; } = null!;
}

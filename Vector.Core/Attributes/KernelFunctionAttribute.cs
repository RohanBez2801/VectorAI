using System;

namespace Vector.Core.Attributes
{
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Class,
        Inherited = false,
        AllowMultiple = false)]
    public sealed class KernelFunctionAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }

        public CapabilityCategory Category { get; init; } = CapabilityCategory.General;
        public RiskLevel Risk { get; init; } = RiskLevel.Low;
        public int Cost { get; init; } = 1;

        public bool RequiresUserApproval { get; init; } = false;
        public bool IsReversible { get; init; } = true;

        public string[] Preconditions { get; init; } = Array.Empty<string>();

        public KernelFunctionAttribute(
            string name,
            string description)
        {
            Name = name;
            Description = description;
        }
    }

    public enum CapabilityCategory
    {
        General,
        FileSystem,
        Shell,
        Network,
        Memory,
        Vision,
        Audio,
        Math,
        Development
    }

    public enum RiskLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }
}

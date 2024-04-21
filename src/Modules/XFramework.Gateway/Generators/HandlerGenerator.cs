using XFramework.Core.Attributes;

namespace XFramework.Gateway.Generators;

[GenerateApiFromNamespace("XFramework.Domain.Shared.Contracts",new[] {
    nameof(Tenant)
})]
public static partial class XFrameworkApiGenerator;
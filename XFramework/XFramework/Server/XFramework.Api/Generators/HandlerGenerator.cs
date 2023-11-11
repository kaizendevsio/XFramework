using XFramework.Core.Attributes;

namespace XFramework.Api.Generators;

[GenerateApiFromNamespace("XFramework.Domain.Generic.Contracts",new[] {
    nameof(Tenant),
    nameof(IdentityCredential)
})]
public static partial class MinimalApiGenerator;
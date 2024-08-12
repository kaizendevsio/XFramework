using XFramework.Core.Attributes;

namespace Gateway.Endpoints;

[GenerateEndpoints("XFramework.Domain.Shared.Contracts",new[] {
    nameof(Tenant)
})]
public static partial class GatewayEndpoints;
using XFramework.Core.Attributes;

namespace Inventario.Api.Endpoints;

[GenerateEndpoints("XFramework.Domain.Shared.Contracts",new[] {
    
    nameof(Tenant),
    nameof(IdentityContact),
    nameof(IdentityInformation)
})]
public static partial class InventarioEndpoints;
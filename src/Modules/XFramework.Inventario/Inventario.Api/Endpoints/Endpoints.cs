using XFramework.Core.Attributes;

namespace Inventario.Api.Endpoints;

[GenerateEndpoints("XFramework.Domain.Shared.Contracts",new[] {
    
    nameof(Tenant)
})]
public static partial class InventarioEndpoints;
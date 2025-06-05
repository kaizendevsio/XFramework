using Inventario.Domain.Shared.Contracts;
using XFramework.Core.Attributes;

namespace Inventario.Api.Endpoints;

[GenerateEndpoints("Inventario.Domain.Shared.Contracts",new[] {
    nameof(Service)
})]
public static partial class InventarioEndpoints;
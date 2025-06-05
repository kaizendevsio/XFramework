using Inventario.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Inventario.Integration.Generators;

[StreamFlowWrapper("Inventario.Domain.Shared.Contracts",new[] {
   nameof(Service)
})]
public static class InventarioServiceWrapper;
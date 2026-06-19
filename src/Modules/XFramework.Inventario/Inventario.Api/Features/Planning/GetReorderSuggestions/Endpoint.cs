using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;
using XFramework.Inventario.Api.Services;
using XFramework.Inventario.Domain.Shared.Contracts.Requests.Planning;
using XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

namespace Inventario.Api.Features.Planning.GetReorderSuggestions;

public static class GetReorderSuggestionsEndpoint
{
    [BoltHandler]
    [MapGet("/api/inventario/planning/reorder-suggestions", Tags = ["Inventario Planning"],
        Summary = "Get reorder suggestions",
        Description = "Returns reorder suggestions from active product, warehouse, and location rules.")]
    public static async Task<Result<List<ReorderSuggestionRow>>> Handle(
        GetReorderSuggestionsRequest request,
        InventoryPlanningService planningService,
        CancellationToken ct)
    {
        return await planningService.GetReorderSuggestionsAsync(request, ct);
    }
}

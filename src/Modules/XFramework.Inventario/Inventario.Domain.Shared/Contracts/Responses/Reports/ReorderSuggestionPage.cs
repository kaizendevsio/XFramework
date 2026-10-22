namespace XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

[MemoryPackable]
public partial record ReorderSuggestionPage(int TotalCount, decimal TotalSuggestedQuantity, List<ReorderSuggestionRow> Items);

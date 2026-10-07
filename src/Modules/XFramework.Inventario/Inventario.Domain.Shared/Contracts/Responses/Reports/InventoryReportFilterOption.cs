namespace XFramework.Inventario.Domain.Shared.Contracts.Responses.Reports;

[MemoryPackable]
public partial record InventoryReportFilterOption(Guid Id, string Label, Guid? ParentId = null);

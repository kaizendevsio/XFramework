using XFramework.Domain.Shared.BusinessObjects;

namespace XFramework.Domain.Shared.DataContext;

[MemoryPackable]
public partial class QueryDescriptor
{
    [MemoryPackOrder(0)]  public string EntityTypeName { get; set; } = string.Empty;
    [MemoryPackOrder(1)]  public List<QueryFilter> Filters { get; set; } = [];
    [MemoryPackOrder(2)]  public List<SortDescriptor> Sorting { get; set; } = [];
    [MemoryPackOrder(3)]  public int? Skip { get; set; }
    [MemoryPackOrder(4)]  public int? Take { get; set; }
    [MemoryPackOrder(5)]  public List<string> Includes { get; set; } = [];
    [MemoryPackOrder(6)]  public QueryExecutionMode Mode { get; set; }
    [MemoryPackOrder(7)]  public bool NoCache { get; set; }
    [MemoryPackOrder(8)]  public bool ApplyDistinct { get; set; }
    [MemoryPackOrder(9)]  public string? DistinctByProperty { get; set; }
    [MemoryPackOrder(10)] public string? AggregateProperty { get; set; }
    [MemoryPackOrder(11)] public string? GroupByProperty { get; set; }
    [MemoryPackOrder(12)] public List<QueryFilter>? PredicateFilters { get; set; }
}

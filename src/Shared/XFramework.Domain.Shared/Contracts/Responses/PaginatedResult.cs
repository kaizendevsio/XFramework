namespace XFramework.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record PaginatedResult<T>
(
    long TotalItems,
    int PageIndex,
    int PageSize,
    IEnumerable<T> Items)
{
    [MemoryPackConstructor]
    public PaginatedResult() : this(0, 0, 0, new List<T>())
    {
        
    }
    public int PageCount => (int)Math.Ceiling(TotalItems / (decimal)PageSize);
}
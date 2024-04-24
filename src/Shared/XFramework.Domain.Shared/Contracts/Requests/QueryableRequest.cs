namespace XFramework.Domain.Shared.Contracts.Requests;

public record QueryableRequest : RequestBase
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public string OrderBy { get; set; }
    public string OrderByDirection { get; set; }
    public string? SearchText { get; set; }
    public List<string>? SearchFields { get; set; } = ["Name"];
    public bool Silent { get; set; }
}   
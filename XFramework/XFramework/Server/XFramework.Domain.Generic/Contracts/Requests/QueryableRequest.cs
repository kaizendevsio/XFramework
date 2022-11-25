namespace XFramework.Domain.Generic.Contracts.Requests;

public class QueryableRequest : RequestBase
{
    public Action? OnSuccess { get; set; }
    public Action? OnFailure { get; set; }
    
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public string OrderBy { get; set; }
    public string OrderByDirection { get; set; }
    public string SearchField { get; set; }
    public bool Silent { get; set; }
}   
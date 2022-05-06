namespace XFramework.Domain.Generic.Contracts.Requests;

public class QueryableRequest : RequestBase
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public string OrderBy { get; set; }
    public string OrderByDirection { get; set; }
    public string SearchField { get; set; }
}   
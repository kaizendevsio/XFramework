using System.ComponentModel.DataAnnotations;
using System.Net;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Domain.Shared.BusinessObjects;

public class QueryResponse<T> : IBaseResponse, IHasRequestServer, IQueryResponse
{
    public QueryResponse() { }
    
    [Key]
    public Guid ResponseId { get; set; } = Guid.NewGuid();
    public HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
    public bool IsSuccess => (int)HttpStatusCode >= 200 && (int)HttpStatusCode < 300;
    public T? Response { get; set; }
    public RequestMetadata? Metadata { get; set; }
}

public interface IQueryResponse;
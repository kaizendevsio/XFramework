using System.ComponentModel.DataAnnotations;
using System.Net;

namespace XFramework.Domain.Generic.BusinessObjects;

public class QueryResponse<T> : IBaseResponse, IHasRequestServer
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
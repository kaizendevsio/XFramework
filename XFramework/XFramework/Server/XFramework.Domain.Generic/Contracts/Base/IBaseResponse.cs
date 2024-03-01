using System.Net;

namespace XFramework.Domain.Generic.Contracts.Base;

public interface IBaseResponse
{
    HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
}
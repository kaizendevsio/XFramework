using System.Net;

namespace XFramework.Domain.Shared.Contracts.Base;

public interface IBaseResponse
{
    HttpStatusCode HttpStatusCode { get; set; }
    public string? Message { get; set; }
}
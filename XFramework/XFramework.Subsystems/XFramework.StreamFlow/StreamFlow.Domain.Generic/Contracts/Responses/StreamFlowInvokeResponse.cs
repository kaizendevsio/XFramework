using System.Net;

namespace StreamFlow.Domain.Generic.Contracts.Responses;

public class StreamFlowInvokeResponse
{
    public HttpStatusCode HttpStatusCode { get; set; }
    public string Message { get; set; }
    public object Response { get; set; }
}
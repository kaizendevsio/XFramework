using System.Net;

namespace StreamFlow.Domain.Generic.BusinessObjects;

public class StreamFlowTelemetry
{
    public DateTime RequestDateTime { get; set; }
    public Guid? ClientGuid { get; set; }
    public HttpStatusCode StreamFlowStatusCode { get; set; } = HttpStatusCode.OK;
}
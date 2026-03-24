using System.Net;

namespace Bolt.Domain.Shared.BusinessObjects;

public class BoltTelemetry
{
    public DateTime RequestDateTime { get; set; }
    public Guid? ClientGuid { get; set; }
    public HttpStatusCode BoltStatusCode { get; set; } = HttpStatusCode.OK;
}
using StreamFlow.Domain.Generic.BusinessObjects;

namespace StreamFlow.Domain.Generic.Contracts.Requests;

public class StreamFlowContract
{
    public string Data { get; set; }
    public string Message { get; set; }
    public StreamFlowTelemetryBO Telemetry { get; set; }
}
using StreamFlow.Domain.Generic.BusinessObjects;

namespace StreamFlow.Domain.Generic.Contracts.Requests;

public class StreamFlowContract
{
    public byte[] Data { get; set; }
    public string Message { get; set; }
    public byte[] Telemetry { get; set; }
}
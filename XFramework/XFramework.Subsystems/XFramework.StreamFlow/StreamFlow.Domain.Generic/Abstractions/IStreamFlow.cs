namespace StreamFlow.Domain.Generic.Abstractions;

public interface IStreamFlow
{
    Task Subscribe();
    Task TelemetryCall();
    Task Register();
    Task Push();
    Task<bool> Ping();
    Task<bool> InvokeResponse();
}
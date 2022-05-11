namespace XFramework.Integration.Entity.Contracts.Responses;

public class SignalRResponse
{
    public HttpStatusCode HttpStatusCode { get; set; }
    public string Message { get; set; }
    public byte[] Response { get; set; }   
}
namespace XFramework.Domain.Generic.BusinessObjects;

public class ServiceRequestLog<TRequest, TResponse>
{
    public TRequest Request { get; set; }
    public TResponse Response { get; set; }
}
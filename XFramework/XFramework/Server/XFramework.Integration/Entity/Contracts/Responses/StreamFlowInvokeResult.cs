using System.Net;

namespace XFramework.Integration.Entity.Contracts.Responses
{
    public class StreamFlowInvokeResult<TResult>
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public string Message { get; set; }
        public TResult Response { get; set; }   
    }
}
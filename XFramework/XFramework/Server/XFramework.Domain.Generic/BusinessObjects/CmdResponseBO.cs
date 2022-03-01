using System.Net;

namespace XFramework.Domain.Generic.BusinessObjects
{
    public class CmdResponse<T>
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public string Message { get; set; }
        public T Request { get; set; }
        public bool IsSuccess { get; set; }
    }
    
    public class CmdResponse
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
    }
}
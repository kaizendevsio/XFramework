using System.Net;

namespace IdentityServer.Domain.BusinessObjects
{
    public class QueryResponseBO<T>
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public string Message { get; set; }
        public T Response { get; set; }
    }
}
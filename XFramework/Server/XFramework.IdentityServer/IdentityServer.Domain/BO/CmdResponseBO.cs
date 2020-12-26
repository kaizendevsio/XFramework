using System.Net;

namespace IdentityServer.Domain.BO
{
    public class CmdResponseBO<T>
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public string Message { get; set; }
        public T Request { get; set; }
        public string RedirectUrl { get; set; }
    }
}
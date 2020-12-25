using IdentityServer.Domain.Enums;

namespace IdentityServer.Domain.BO
{
    public class CmdResponseBO<T>
    {
        public ApiStatus HttpStatusCode { get; set; }
        public string Message { get; set; }
        public T Request { get; set; }
        public string RedirectUrl { get; set; }
    }
}
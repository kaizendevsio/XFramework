using IdentityServer.Domain.Enums;

namespace IdentityServer.Domain.BO
{
    public class ApiResponseBO
    {
        public ApiStatus HttpStatusCode { get; set; }
        public string Message { get; set; }
        public string RedirectUrl { get; set; }
    }
}
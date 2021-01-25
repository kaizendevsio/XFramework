using XFramework.Domain.Enums;

namespace XFramework.Domain.BusinessObjects
{
    public class ApiResponseBO
    {
        public ApiStatus HttpStatusCode { get; set; }
        public string Message { get; set; }
        public string RedirectUrl { get; set; }
    }
}
using System.Net;

namespace Coins.Web.Models
{
    public class ResponseVm
    {
        public HttpStatusCode HttpStatusCode { get; set; }
        public string Message { get; set; }
    }
}
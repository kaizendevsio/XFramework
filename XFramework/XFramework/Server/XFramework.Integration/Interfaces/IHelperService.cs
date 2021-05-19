using XFramework.Domain.Generic.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Interfaces
{
    public interface IHelperService : IXFrameworkService
    {
        public string GenerateRandomString(int size);
        public string GenerateReferenceString();
        public HttpHelper Http { get; set; }
        public StopWatchHelper StopWatch { get; set; }
    }
}
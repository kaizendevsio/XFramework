using XFramework.Domain.Generic.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Interfaces
{
    public interface IHelperService : IXFrameworkService
    {
        public string GenerateRandomString(int size);
        public string GenerateReferenceString();
        public HttpHelper Http { get; }
        public StopWatchHelper StopWatch { get; set; }
    }
}
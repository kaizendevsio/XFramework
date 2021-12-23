using XFramework.Domain.Generic.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Interfaces
{
    public interface IHelperService : IXFrameworkService
    {
        public string GenerateRandomString(int size);
        public string GenerateReferenceString();
        public long GenerateRandomNumber(int start, int end);
        public long GenerateRandomNumber(long min, long max);
        public HttpHelper Http { get; }
        public StopWatchHelper StopWatch { get; set; }
    }
}
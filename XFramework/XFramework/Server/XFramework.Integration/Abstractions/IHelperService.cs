using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Abstractions;

public interface IHelperService : IXFrameworkService
{
    public string GenerateRandomString(int size);
    public string GenerateReferenceString();
    public long GenerateRandomNumber(int start, int end);
    public long GenerateRandomNumber(long min, long max);
    public T? RemoveCircularReference<T>(T obj);
    public HttpHelper Http { get; }
}
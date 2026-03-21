using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Services;

public sealed class HelperService(IConfiguration configuration, ILogger<HelperService> logger) : IHelperService
{
    public JsonSerializerOptions CachedSerializationOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public HttpHelper Http { get; } = new(configuration);
    private const string Chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz1234567890";

    public string GenerateRandomString(long size)
    {
        var b = new byte[size];
        RandomNumberGenerator.Fill(b);
        return Encoding.ASCII.GetString(b);
    }

    public long GenerateRandomNumber(int start, int end)
    {
        return Random.Shared.Next(start, end);
    }

    public long GenerateRandomNumber(long min, long max)
    {
        return Random.Shared.NextInt64(min, max);
    }

    public string GenerateRandomString(int size)
    {
        return new(Enumerable.Repeat(Chars, size)
            .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
    }

    public string GenerateReferenceString()
    {
        var ticks = new DateTime(2021, 1, 1).Ticks;
        var ans = DateTime.Now.Ticks - ticks;
        return ans.ToString("x").ToUpper();
    }

    public T? RemoveCircularReference<T>(T obj)
    {
        var sw = Stopwatch.StartNew();
        var json = JsonSerializer.Serialize(obj, CachedSerializationOptions).AsSpan();
        var result = JsonSerializer.Deserialize<T>(json);
        sw.Stop();
        logger.LogInformation("Circular reference removal took {ElapsedMs}ms", sw.ElapsedMilliseconds);
        return result;
    }
}

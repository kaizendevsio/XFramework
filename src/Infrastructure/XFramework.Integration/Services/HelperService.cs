using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Services;

public class HelperService : IHelperService
{
    private readonly ILogger<HelperService> _logger;

    public JsonSerializerOptions CachedSerializationOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public HttpHelper Http { get; }
    private const string Chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz1234567890";

    public HelperService(IConfiguration configuration, ILogger<HelperService> logger)
    {
        _logger = logger;
        Http = new(configuration);
    }
        
    public string GenerateRandomString(long size)
    {
        var b = new byte[size];
        new RNGCryptoServiceProvider().GetBytes(b);
        return Encoding.ASCII.GetString(b);
    }
    public long GenerateRandomNumber(int start, int end)
    {
        var rnd = new Random();
        var n  = rnd.Next(start, end);
        return n;
    }
        
    public long GenerateRandomNumber(long min, long max)
    {
        var rand = new Random();
        byte[] buf = new byte[8];
        rand.NextBytes(buf);
        long longRand = BitConverter.ToInt64(buf, 0);

        return (Math.Abs(longRand % (max - min)) + min);
    }

    public string GenerateRandomString(int size)
    {
        var random = new Random();
        return new(Enumerable.Repeat(Chars, size)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public string GenerateReferenceString()
    {
        var ticks = new DateTime(2021,1,1).Ticks;
        var ans = DateTime.Now.Ticks - ticks;
        var uniqueId = ans.ToString("x").ToUpper();
        return uniqueId;
    }
    
    public T? RemoveCircularReference<T>(T obj)
    {
        var sw = new Stopwatch();
        
        sw.Start();
        var json = JsonSerializer.Serialize(obj,CachedSerializationOptions).AsSpan();
        var result = JsonSerializer.Deserialize<T>(json);
        sw.Stop();
        _logger.LogInformation($"Circular reference removal took {sw.ElapsedMilliseconds}ms");
        return result;
    }
}
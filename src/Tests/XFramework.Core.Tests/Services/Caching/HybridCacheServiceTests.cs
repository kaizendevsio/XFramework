using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using StackExchange.Redis;
using XFramework.Core.Extensions;
using XFramework.Core.Services.Caching;

namespace XFramework.Core.Tests.Services.Caching;

/// <summary>
/// Comprehensive unit tests for HybridCacheService implementation
/// Tests L1 (Memory) and L2 (Redis) caching with graceful degradation
/// </summary>
[TestFixture]
public class HybridCacheServiceTests
{
    private Mock<IMemoryCache> _memoryCacheMock = null!;
    private Mock<IDistributedCache> _distributedCacheMock = null!;
    private Mock<IConnectionMultiplexer> _redisConnectionMock = null!;
    private Mock<ILogger<HybridCacheService>> _loggerMock = null!;
    private Mock<IOptions<CacheOptions>> _optionsMock = null!;
    private CacheOptions _cacheOptions = null!;
    private HybridCacheService _service = null!;

    private sealed record CachePayload(int Id, string Name);

    private sealed record L2OnlyPayload(string Data);

    private string PrefixedKey(string key) =>
        string.IsNullOrEmpty(_cacheOptions.RedisInstanceName)
            ? key
            : $"{_cacheOptions.RedisInstanceName}{key}";

    [SetUp]
    public void Setup()
    {
        _memoryCacheMock = new Mock<IMemoryCache>();
        _distributedCacheMock = new Mock<IDistributedCache>();
        _redisConnectionMock = new Mock<IConnectionMultiplexer>();
        _loggerMock = new Mock<ILogger<HybridCacheService>>();
        _optionsMock = new Mock<IOptions<CacheOptions>>();

        _cacheOptions = new CacheOptions
        {
            Enabled = true,
            EnableL1Cache = true,
            EnableL2Cache = true,
            EnableStatistics = true,
            EnableGracefulDegradation = true,
            DefaultAbsoluteExpirationSeconds = 3600,
            DefaultSlidingExpirationSeconds = 300
        };

        _optionsMock.Setup(x => x.Value).Returns(_cacheOptions);

        // Setup Redis as connected by default
        _redisConnectionMock.Setup(x => x.IsConnected).Returns(true);
    }

    private void CreateService(bool redisAvailable = true)
    {
        if (redisAvailable)
        {
            _redisConnectionMock.Setup(x => x.IsConnected).Returns(true);
        }
        else
        {
            _redisConnectionMock.Setup(x => x.IsConnected).Returns(false);
        }

        _service = new HybridCacheService(
            _memoryCacheMock.Object,
            _distributedCacheMock.Object,
            _redisConnectionMock.Object,
            _optionsMock.Object,
            _loggerMock.Object
        );
    }

    #region Registration Tests

    [Test]
    public void AddMemoryCaching_ShouldResolveHybridCacheServiceWithoutRedisServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCaching();

        using var provider = services.BuildServiceProvider();

        // Act
        var cacheService = provider.GetRequiredService<ICacheService>();

        // Assert
        cacheService.Should().BeOfType<HybridCacheService>();
    }

    [Test]
    public void AddHybridCaching_ShouldLetHybridCacheServiceOwnRedisKeyPrefix()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Caching:RedisConnectionString"] = "localhost:6379",
                ["Caching:RedisInstanceName"] = "XFramework:"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHybridCaching(configuration);

        using var provider = services.BuildServiceProvider();

        // Assert
        var cacheOptions = provider.GetRequiredService<IOptions<CacheOptions>>().Value;
        cacheOptions.RedisInstanceName.Should().Be("XFramework:");

        var redisOptions = provider.GetRequiredService<IOptions<RedisCacheOptions>>().Value;
        redisOptions.InstanceName.Should().BeNullOrEmpty();
    }

    #endregion

    #region GetAsync Tests

    [Test]
    public async Task GetAsync_L1CacheHit_ShouldReturnValueFromMemory()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var expectedValue = "cached-value";
        object? cacheValue = expectedValue;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out cacheValue))
            .Returns(true);

        // Act
        var result = await _service.GetAsync<string>(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(expectedValue);
        _memoryCacheMock.Verify(x => x.TryGetValue(cacheKey, out cacheValue), Times.Once);
        _distributedCacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetAsync_L2CacheHit_ShouldReturnValueFromRedisAndPopulateL1()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var expectedValue = new CachePayload(1, "Test");
        var jsonValue = JsonSerializer.Serialize(expectedValue);
        object? nullCacheValue = null;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullCacheValue))
            .Returns(false);

        _distributedCacheMock
            .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(jsonValue));

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupProperty(x => x.Value);
        cacheEntry.SetupProperty(x => x.AbsoluteExpirationRelativeToNow);
        cacheEntry.SetupProperty(x => x.SlidingExpiration);

        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetAsync<CachePayload>(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(expectedValue);
        _distributedCacheMock.Verify(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
        _memoryCacheMock.Verify(x => x.CreateEntry(cacheKey), Times.Once);
    }

    [Test]
    public async Task GetAsync_CacheMiss_ShouldReturnSuccessWithNullData()
    {
        // Arrange
        CreateService();
        var key = "missing-key";
        var cacheKey = PrefixedKey(key);
        object? nullCacheValue = null;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullCacheValue))
            .Returns(false);

        _distributedCacheMock
            .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _service.GetAsync<string>(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeNull();
        result.Message.Should().Be("Cache miss");
    }

    [Test]
    public async Task GetAsync_CachingDisabled_ShouldReturnSuccessWithNullData()
    {
        // Arrange
        _cacheOptions.Enabled = false;
        CreateService();
        var key = "test-key";

        // Act
        var result = await _service.GetAsync<string>(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeNull();
        result.Message.Should().Be("Caching is disabled");
        _memoryCacheMock.Verify(x => x.TryGetValue(It.IsAny<string>(), out It.Ref<object?>.IsAny), Times.Never);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task GetAsync_InvalidKey_ShouldReturnFailure(string invalidKey)
    {
        // Arrange
        CreateService();

        // Act
        var result = await _service.GetAsync<string>(invalidKey);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Cache key cannot be null or empty");
    }

    [Test]
    public async Task GetAsync_ExceptionThrown_ShouldReturnFailure()
    {
        // Arrange
        _cacheOptions.EnableGracefulDegradation = false;
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var exceptionMessage = "Cache error";

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out It.Ref<object?>.IsAny))
            .Throws(new Exception(exceptionMessage));

        // Act
        var result = await _service.GetAsync<string>(key);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.Message.Should().Contain("Cache retrieval failed");
        result.Message.Should().Contain(exceptionMessage);
    }

    [Test]
    public async Task GetAsync_ExceptionThrown_WithGracefulDegradation_ShouldReturnDefault()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out It.Ref<object?>.IsAny))
            .Throws(new Exception("Cache error"));

        // Act
        var result = await _service.GetAsync<string>(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeNull();
        result.Message.Should().Be("Cache retrieval failed, returning default");
    }

    [Test]
    public async Task GetAsync_WithStatisticsEnabled_ShouldTrackHitsAndMisses()
    {
        // Arrange
        CreateService();
        var key1 = "hit-key";
        var key2 = "miss-key";
        var cacheKey1 = PrefixedKey(key1);
        var cacheKey2 = PrefixedKey(key2);
        object? hitValue = "value";
        object? missValue = null;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey1, out hitValue))
            .Returns(true);

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey2, out missValue))
            .Returns(false);

        _distributedCacheMock
            .Setup(x => x.GetAsync(cacheKey2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        await _service.GetAsync<string>(key1); // Hit
        await _service.GetAsync<string>(key2); // Miss

        var stats = _service.GetStatistics();

        // Assert
        stats.IsSuccess.Should().BeTrue();
        stats.Data.Should().NotBeNull();
        stats.Data!.TotalGets.Should().Be(2);
        stats.Data.Hits.Should().Be(1);
        stats.Data.Misses.Should().Be(1);
        stats.Data.HitRate.Should().BeApproximately(50.0, 0.01);
    }

    #endregion

    #region SetAsync Tests

    [Test]
    public async Task SetAsync_WithBothCachesEnabled_ShouldSetInBothL1AndL2()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var value = "test-value";

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupProperty(x => x.Value);
        cacheEntry.SetupProperty(x => x.AbsoluteExpirationRelativeToNow);
        cacheEntry.SetupProperty(x => x.SlidingExpiration);

        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.SetAsync(key, value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _memoryCacheMock.Verify(x => x.CreateEntry(cacheKey), Times.Once);
        _distributedCacheMock.Verify(
            x => x.SetAsync(cacheKey, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task SetAsync_WithAbsoluteExpiration_ShouldSetWithCorrectExpiration()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var value = "test-value";
        var absoluteExpiration = TimeSpan.FromMinutes(30);

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupProperty(x => x.Value);
        cacheEntry.SetupProperty(x => x.AbsoluteExpirationRelativeToNow);
        cacheEntry.SetupProperty(x => x.SlidingExpiration);

        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        DistributedCacheEntryOptions? capturedOptions = null;
        _distributedCacheMock
            .Setup(x => x.SetAsync(cacheKey, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.SetAsync(key, value, absoluteExpiration: absoluteExpiration);

        // Assert
        result.IsSuccess.Should().BeTrue();
        cacheEntry.Object.AbsoluteExpirationRelativeToNow.Should().Be(absoluteExpiration);
        capturedOptions.Should().NotBeNull();
        capturedOptions!.AbsoluteExpirationRelativeToNow.Should().Be(absoluteExpiration);
    }

    [Test]
    public async Task SetAsync_WithSlidingExpiration_ShouldSetWithCorrectExpiration()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var value = "test-value";
        var slidingExpiration = TimeSpan.FromMinutes(10);

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupProperty(x => x.Value);
        cacheEntry.SetupProperty(x => x.AbsoluteExpirationRelativeToNow);
        cacheEntry.SetupProperty(x => x.SlidingExpiration);

        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        DistributedCacheEntryOptions? capturedOptions = null;
        _distributedCacheMock
            .Setup(x => x.SetAsync(cacheKey, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.SetAsync(key, value, slidingExpiration: slidingExpiration);

        // Assert
        result.IsSuccess.Should().BeTrue();
        cacheEntry.Object.SlidingExpiration.Should().Be(slidingExpiration);
        capturedOptions.Should().NotBeNull();
        capturedOptions!.SlidingExpiration.Should().Be(slidingExpiration);
    }

    [Test]
    public async Task SetAsync_WhenCachingDisabled_ShouldReturnSuccessWithoutCaching()
    {
        // Arrange
        _cacheOptions.Enabled = false;
        CreateService();
        var key = "test-key";
        var value = "test-value";

        // Act
        var result = await _service.SetAsync(key, value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Caching is disabled");
        _memoryCacheMock.Verify(x => x.CreateEntry(It.IsAny<string>()), Times.Never);
        _distributedCacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task SetAsync_InvalidKey_ShouldReturnFailure(string invalidKey)
    {
        // Arrange
        CreateService();

        // Act
        var result = await _service.SetAsync(invalidKey, "value");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Cache key cannot be null or empty");
    }

    [Test]
    public async Task SetAsync_WhenL2Unavailable_WithGracefulDegradation_ShouldSucceedWithL1Only()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var value = "test-value";

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupProperty(x => x.Value);
        cacheEntry.SetupProperty(x => x.AbsoluteExpirationRelativeToNow);
        cacheEntry.SetupProperty(x => x.SlidingExpiration);

        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        _distributedCacheMock
            .Setup(x => x.SetAsync(cacheKey, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis unavailable"));

        // Act
        var result = await _service.SetAsync(key, value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("L1 only");
        _memoryCacheMock.Verify(x => x.CreateEntry(cacheKey), Times.Once);
    }

    [Test]
    public async Task SetAsync_WithMemorySizeLimit_ShouldSetSizedEntry()
    {
        // Arrange
        _cacheOptions.EnableL2Cache = false;
        _cacheOptions.MemoryCacheSizeLimitMb = 1;

        using var memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1024 * 1024
        });

        var service = new HybridCacheService(
            memoryCache,
            _optionsMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.SetAsync("size-limited-key", "value");
        var getResult = await service.GetAsync<string>("size-limited-key");

        // Assert
        result.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Data.Should().Be("value");
    }

    #endregion

    #region RemoveAsync Tests

    [Test]
    public async Task RemoveAsync_WithValidKey_ShouldRemoveFromBothCaches()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);

        // Act
        var result = await _service.RemoveAsync(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Cache entry removed successfully");
        _memoryCacheMock.Verify(x => x.Remove(cacheKey), Times.Once);
        _distributedCacheMock.Verify(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task RemoveAsync_InvalidKey_ShouldReturnFailure(string invalidKey)
    {
        // Arrange
        CreateService();

        // Act
        var result = await _service.RemoveAsync(invalidKey);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Cache key cannot be null or empty");
    }

    [Test]
    public async Task RemoveAsync_WhenL2Unavailable_ShouldStillRemoveFromL1()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);

        _distributedCacheMock
            .Setup(x => x.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis unavailable"));

        // Act
        var result = await _service.RemoveAsync(key);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        _memoryCacheMock.Verify(x => x.Remove(cacheKey), Times.Once);
    }

    #endregion

    #region ExistsAsync Tests

    [Test]
    public async Task ExistsAsync_KeyExistsInL1_ShouldReturnTrue()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        object? value = "cached-value";

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out value))
            .Returns(true);

        // Act
        var result = await _service.ExistsAsync(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        _memoryCacheMock.Verify(x => x.TryGetValue(cacheKey, out value), Times.Once);
    }

    [Test]
    public async Task ExistsAsync_KeyExistsInL2Only_ShouldReturnTrue()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        object? nullValue = null;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(x => x.KeyExistsAsync((RedisKey)cacheKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _redisConnectionMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        // Act
        var result = await _service.ExistsAsync(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeTrue();
        mockDatabase.Verify(x => x.KeyExistsAsync((RedisKey)cacheKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    [Test]
    public async Task ExistsAsync_KeyDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        CreateService();
        var key = "missing-key";
        var cacheKey = PrefixedKey(key);
        object? nullValue = null;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(x => x.KeyExistsAsync((RedisKey)cacheKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        _redisConnectionMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        // Act
        var result = await _service.ExistsAsync(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeFalse();
        mockDatabase.Verify(x => x.KeyExistsAsync((RedisKey)cacheKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task ExistsAsync_InvalidKey_ShouldReturnFailure(string invalidKey)
    {
        // Arrange
        CreateService();

        // Act
        var result = await _service.ExistsAsync(invalidKey);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Cache key cannot be null or empty");
    }

    #endregion

    #region GetOrSetAsync Tests

    [Test]
    public async Task GetOrSetAsync_CacheHit_ShouldReturnCachedValueWithoutExecutingFactory()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var cachedValue = "cached-value";
        object? cacheValue = cachedValue;
        var factoryExecuted = false;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out cacheValue))
            .Returns(true);

        // Act
        var result = await _service.GetOrSetAsync(
            key,
            _ =>
            {
                factoryExecuted = true;
                return Task.FromResult("factory-value");
            }
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(cachedValue);
        factoryExecuted.Should().BeFalse();
    }

    [Test]
    public async Task GetOrSetAsync_CacheMiss_ShouldExecuteFactoryAndCacheResult()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var factoryValue = "factory-value";
        object? nullValue = null;
        var factoryExecuted = false;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);

        _distributedCacheMock
            .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupProperty(x => x.Value);
        cacheEntry.SetupProperty(x => x.AbsoluteExpirationRelativeToNow);
        cacheEntry.SetupProperty(x => x.SlidingExpiration);

        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetOrSetAsync(
            key,
            _ =>
            {
                factoryExecuted = true;
                return Task.FromResult(factoryValue);
            }
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(factoryValue);
        factoryExecuted.Should().BeTrue();
        _memoryCacheMock.Verify(x => x.CreateEntry(cacheKey), Times.Once);
        _distributedCacheMock.Verify(
            x => x.SetAsync(cacheKey, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Test]
    public async Task GetOrSetAsync_FactoryThrowsException_ShouldReturnFailure()
    {
        // Arrange
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var exceptionMessage = "Factory failed";
        object? nullValue = null;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);

        _distributedCacheMock
            .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _service.GetOrSetAsync<string>(
            key,
            _ => throw new Exception(exceptionMessage)
        );

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.Message.Should().Contain("GetOrSet operation failed");
        result.Message.Should().Contain(exceptionMessage);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task GetOrSetAsync_InvalidKey_ShouldReturnFailure(string invalidKey)
    {
        // Arrange
        CreateService();

        // Act
        var result = await _service.GetOrSetAsync(invalidKey, _ => Task.FromResult("value"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Cache key cannot be null or empty");
    }

    [Test]
    public async Task GetOrSetAsync_NullFactory_ShouldReturnFailure()
    {
        // Arrange
        CreateService();

        // Act
        var result = await _service.GetOrSetAsync<string>("key", null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Factory function cannot be null");
    }

    #endregion

    #region RemoveByPrefixAsync Tests

    [Test]
    public async Task RemoveByPrefixAsync_WithMatchingKeys_ShouldRemoveAllMatches()
    {
        // Arrange
        CreateService();
        var prefix = "user:";
        var cachePattern = $"{PrefixedKey(prefix)}*";
        var matchingKeys = new[] { "user:1", "user:2", "user:3" }
            .Select(k => (RedisKey)PrefixedKey(k))
            .ToArray();

        var mockServer = new Mock<IServer>();
        mockServer
            .Setup(x => x.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(matchingKeys);

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(matchingKeys.Length);

        var endPoints = new EndPoint[] { new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6379) };
        _redisConnectionMock
            .Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(endPoints);

        _redisConnectionMock
            .Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(mockServer.Object);

        _redisConnectionMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        // Act
        var result = await _service.RemoveByPrefixAsync(prefix);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(3);
        result.Message.Should().Contain("Removed 3 entries");
        mockServer.Verify(
            x => x.Keys(
                It.IsAny<int>(),
                It.Is<RedisValue>(value => value.ToString() == cachePattern),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
        mockDatabase.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey[]>(keys => keys.SequenceEqual(matchingKeys)),
                It.IsAny<CommandFlags>()),
            Times.Once);
        mockDatabase.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Test]
    public async Task RemoveByPrefixAsync_NoMatchingKeys_ShouldReturnZero()
    {
        // Arrange
        CreateService();
        var prefix = "nonexistent:";
        var cachePattern = $"{PrefixedKey(prefix)}*";
        var emptyKeys = Enumerable.Empty<RedisKey>();

        var mockServer = new Mock<IServer>();
        mockServer
            .Setup(x => x.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(emptyKeys);

        var mockDatabase = new Mock<IDatabase>();

        var endPoints = new EndPoint[] { new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6379) };
        _redisConnectionMock
            .Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(endPoints);

        _redisConnectionMock
            .Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(mockServer.Object);

        _redisConnectionMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        // Act
        var result = await _service.RemoveByPrefixAsync(prefix);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(0);
        mockServer.Verify(
            x => x.Keys(
                It.IsAny<int>(),
                It.Is<RedisValue>(value => value.ToString() == cachePattern),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
        mockDatabase.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
        mockDatabase.Verify(x => x.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task RemoveByPrefixAsync_InvalidPrefix_ShouldReturnFailure(string invalidPrefix)
    {
        // Arrange
        CreateService();

        // Act
        var result = await _service.RemoveByPrefixAsync(invalidPrefix);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Be("Prefix cannot be null or empty");
    }

    #endregion

    #region GetStatistics Tests

    [Test]
    public void GetStatistics_ShouldReturnCorrectStatistics()
    {
        // Arrange
        CreateService();

        // Act
        var result = _service.GetStatistics();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.TotalGets.Should().Be(0);
        result.Data.Hits.Should().Be(0);
        result.Data.Misses.Should().Be(0);
        result.Data.HitRate.Should().Be(0);
        result.Data.L2Available.Should().BeTrue();
    }

    [Test]
    public async Task GetStatistics_AfterMultipleOperations_ShouldCalculateCorrectHitRate()
    {
        // Arrange
        CreateService();
        var hitKey1 = PrefixedKey("hit1");
        var hitKey2 = PrefixedKey("hit2");
        var missKey1 = PrefixedKey("miss1");
        object? hitValue = "value";
        object? missValue = null;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(hitKey1, out hitValue))
            .Returns(true);
        _memoryCacheMock
            .Setup(x => x.TryGetValue(hitKey2, out hitValue))
            .Returns(true);
        _memoryCacheMock
            .Setup(x => x.TryGetValue(missKey1, out missValue))
            .Returns(false);

        _distributedCacheMock
            .Setup(x => x.GetAsync(missKey1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        await _service.GetAsync<string>("hit1");
        await _service.GetAsync<string>("hit2");
        await _service.GetAsync<string>("miss1");

        var result = _service.GetStatistics();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalGets.Should().Be(3);
        result.Data.Hits.Should().Be(2);
        result.Data.Misses.Should().Be(1);
        result.Data.HitRate.Should().BeApproximately(66.67, 0.01);
    }

    [Test]
    public void GetStatistics_WhenRedisUnavailable_ShouldIndicateL2Unavailable()
    {
        // Arrange
        CreateService(redisAvailable: false);

        // Act
        var result = _service.GetStatistics();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.L2Available.Should().BeFalse();
    }

    #endregion

    #region ClearAllAsync Tests

    [Test]
    public async Task ClearAllAsync_ShouldRemovePrefixedRedisKeysAndResetStatistics()
    {
        // Arrange
        CreateService();
        var cacheKey = PrefixedKey("key");
        var matchingKeys = new[] { "one", "two" }
            .Select(k => (RedisKey)PrefixedKey(k))
            .ToArray();
        
        // Add some statistics first
        object? hitValue = "value";
        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out hitValue))
            .Returns(true);
        await _service.GetAsync<string>("key");

        var mockServer = new Mock<IServer>();
        mockServer
            .Setup(x => x.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(matchingKeys);

        var mockDatabase = new Mock<IDatabase>();
        mockDatabase
            .Setup(x => x.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(matchingKeys.Length);

        var endPoints = new EndPoint[] { new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 6379) };
        _redisConnectionMock
            .Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(endPoints);

        _redisConnectionMock
            .Setup(x => x.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(mockServer.Object);

        _redisConnectionMock
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);

        // Act
        var result = await _service.ClearAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Cache cleared successfully");
        mockServer.Verify(
            x => x.Keys(
                It.IsAny<int>(),
                It.Is<RedisValue>(value => value.ToString() == $"{_cacheOptions.RedisInstanceName}*"),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
        mockDatabase.Verify(
            x => x.KeyDeleteAsync(
                It.Is<RedisKey[]>(keys => keys.SequenceEqual(matchingKeys)),
                It.IsAny<CommandFlags>()),
            Times.Once);
        mockServer.Verify(x => x.FlushDatabaseAsync(It.IsAny<int>(), It.IsAny<CommandFlags>()), Times.Never);

        // Verify statistics were reset
        var stats = _service.GetStatistics();
        stats.Data!.TotalGets.Should().Be(0);
        stats.Data.Hits.Should().Be(0);
        stats.Data.Misses.Should().Be(0);
    }

    [Test]
    public async Task ClearAllAsync_WhenRedisUnavailable_ShouldHandleGracefully()
    {
        // Arrange
        CreateService(redisAvailable: false);

        // Act
        var result = await _service.ClearAllAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Cache cleared successfully");
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Test]
    public async Task HybridCache_ComplexObject_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        CreateService();
        var key = "complex-key";
        var cacheKey = PrefixedKey(key);
        var complexObject = new
        {
            Id = 123,
            Name = "Test User",
            CreatedAt = DateTime.UtcNow,
            Tags = new[] { "tag1", "tag2", "tag3" },
            Metadata = new Dictionary<string, object>
            {
                { "IsActive", true },
                { "Score", 95.5 }
            }
        };

        var jsonValue = JsonSerializer.Serialize(complexObject);
        object? nullValue = null;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out nullValue))
            .Returns(false);

        _distributedCacheMock
            .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(jsonValue));

        var cacheEntry = new Mock<ICacheEntry>();
        cacheEntry.SetupProperty(x => x.Value);
        cacheEntry.SetupProperty(x => x.AbsoluteExpirationRelativeToNow);
        cacheEntry.SetupProperty(x => x.SlidingExpiration);

        _memoryCacheMock
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(cacheEntry.Object);

        // Act
        var result = await _service.GetAsync<object>(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        var resultObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(result.Data));
        resultObj.Should().ContainKey("Id");
        resultObj.Should().ContainKey("Name");
        resultObj.Should().ContainKey("Tags");
    }

    [Test]
    public async Task HybridCache_L1OnlyMode_ShouldWorkCorrectly()
    {
        // Arrange
        _cacheOptions.EnableL2Cache = false;
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var value = "test-value";
        object? cacheValue = value;

        _memoryCacheMock
            .Setup(x => x.TryGetValue(cacheKey, out cacheValue))
            .Returns(true);

        // Act
        var result = await _service.GetAsync<string>(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be(value);
        _distributedCacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task HybridCache_L2OnlyMode_ShouldWorkCorrectly()
    {
        // Arrange
        _cacheOptions.EnableL1Cache = false;
        CreateService();
        var key = "test-key";
        var cacheKey = PrefixedKey(key);
        var value = new L2OnlyPayload("test");
        var jsonValue = JsonSerializer.Serialize(value);

        _distributedCacheMock
            .Setup(x => x.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(jsonValue));

        // Act
        var result = await _service.GetAsync<L2OnlyPayload>(key);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(value);
        _memoryCacheMock.Verify(x => x.TryGetValue(It.IsAny<string>(), out It.Ref<object?>.IsAny), Times.Never);
    }

    [Test]
    public void Dispose_ShouldNotThrowException()
    {
        // Arrange
        CreateService();

        // Act & Assert
        Assert.DoesNotThrow(() => _service.Dispose());
    }

    #endregion
}

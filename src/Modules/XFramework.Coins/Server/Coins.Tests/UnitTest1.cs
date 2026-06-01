using Coins.Api.BusinessObjects;
using Coins.Api.Configurations;
using Coins.Api.Drivers;
using Coins.Api.Interfaces.Wrappers;
using Coins.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;

namespace Coins.Tests;

[TestClass]
public class BlockchainServiceTests
{
    private Mock<IBtcBlockchainWrapper> _mockBlockchainWrapper = null!;
    private Mock<ILogger<BlockchainService>> _mockLogger = null!;
    private BlockchainService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockBlockchainWrapper = new Mock<IBtcBlockchainWrapper>();
        _mockLogger = new Mock<ILogger<BlockchainService>>();
        _service = new BlockchainService(_mockBlockchainWrapper.Object, _mockLogger.Object);
    }

    [TestMethod]
    public async Task BulkSendAsync_WithNullList_ReturnsBadRequest()
    {
        // Act
        var result = await _service.BulkSendAsync(null!);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, result.HttpStatusCode);
        Assert.IsNotNull(result.Message);
    }

    [TestMethod]
    public async Task BulkSendAsync_WithEmptyList_ReturnsBadRequest()
    {
        // Act
        var result = await _service.BulkSendAsync(new List<BtcTransactionBO>());

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, result.HttpStatusCode);
    }

    [TestMethod]
    public async Task BulkSendAsync_WithValidList_CallsWrapper()
    {
        // Arrange
        var transactions = new List<BtcTransactionBO>
        {
            new() { Id = 1, BtcAmount = 0.0005m, BtcAddress = "3HmvQYSdKdvQ3cuEB7dMCi8f7AuhDEjXYo" },
            new() { Id = 2, BtcAmount = 0.0005m, BtcAddress = "3HmvQYSdKdvQ3cuEB7dMCi8f7AuhDEjXYo" }
        };

        var mockResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        _mockBlockchainWrapper
            .Setup(x => x.SendToMany(It.IsAny<List<BtcTransactionBO>>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _service.BulkSendAsync(transactions);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, result.HttpStatusCode);
        _mockBlockchainWrapper.Verify(x => x.SendToMany(It.IsAny<List<BtcTransactionBO>>()), Times.Once);
    }

    [TestMethod]
    public void GetGapLimit_ConfiguredValue_ReturnsConfiguredMaxGapLimit()
    {
        // Arrange
        var driver = CreateDriver(new BtcBlockchainConfiguration
        {
            MaxGapLimit = 42
        });

        // Act
        var result = driver.GetGapLimit();

        // Assert
        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public async Task EnableHd_NotSupported_ReturnsFalse()
    {
        // Arrange
        var driver = CreateDriver(new BtcBlockchainConfiguration());

        // Act
        var result = await driver.EnableHd();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SendToMany_WithConfiguredWallet_UrlEncodesQueryValues()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var driver = CreateDriver(
            new BtcBlockchainConfiguration
            {
                ServiceUrl = new Uri("https://blockchain.test/"),
                FeeUrl = new Uri("https://fees.test/"),
                ApiCode = "api code",
                WalletConfiguration = new WalletConfiguration
                {
                    WalletIdentifier = "wallet id",
                    WalletPassword = "p@ss word"
                }
            },
            handler);

        var transactions = new List<BtcTransactionBO>
        {
            new() { Id = 1, BtcAmount = 0.00000001m, BtcAddress = "btc-address" }
        };

        // Act
        var result = await driver.SendToMany(transactions);

        // Assert
        Assert.AreEqual(System.Net.HttpStatusCode.OK, result.StatusCode);
        Assert.AreEqual(2, handler.RequestUris.Count);

        var sendUri = handler.RequestUris[1].AbsoluteUri;
        StringAssert.Contains(sendUri, "wallet%20id");
        StringAssert.Contains(sendUri, "password=p%40ss%20word");
        StringAssert.Contains(sendUri, "api_code=api%20code");
        StringAssert.Contains(sendUri, "recipients=%7B%22btc-address%22%3A1");
    }

    private static BlockchainInfoDriver CreateDriver(
        BtcBlockchainConfiguration configuration,
        RecordingHttpMessageHandler? handler = null)
    {
        var httpClient = new HttpClient(handler ?? new RecordingHttpMessageHandler());
        return new BlockchainInfoDriver(
            configuration,
            httpClient,
            NullLogger<BlockchainInfoDriver>.Instance);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);

            if (request.RequestUri!.AbsolutePath.Contains("fees", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"fastestFee":50,"halfHourFee":25,"hourFee":10}""",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                ReasonPhrase = "OK"
            });
        }
    }
}

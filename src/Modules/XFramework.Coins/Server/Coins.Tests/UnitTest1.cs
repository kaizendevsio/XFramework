using Coins.Api.BusinessObjects;
using Coins.Api.Interfaces.Wrappers;
using Coins.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;

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
}
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using XFramework.Inventario.Api.Services;

namespace Inventario.IntegrationTests.Tests;

[TestFixture]
public class ProductTests
{
    private HttpClient _http = null!;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [SetUp]
    public void SetUp() => _http = new HttpClient { BaseAddress = new Uri(InventarioTestFixture.AppUrl) };

    [TearDown]
    public void TearDown() => _http?.Dispose();

    #region Create

    [Test]
    public async Task CreateProduct_WithValidData_Returns200()
    {
        var request = new CreateProductRequest
        {
            Name = $"TestProduct_{Guid.NewGuid():N}",
            Price = 19.99m,
            StockQuantity = 100,
            CategoryId = XFramework.TestInfrastructure.TestConstants.ProductCategoryId,
            IsAvailable = true
        };

        var response = await _http.PostAsJsonAsync("/api/products", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");
    }

    [Test]
    public async Task CreateProduct_WithMissingName_Returns400()
    {
        var request = new CreateProductRequest
        {
            Name = "",
            Price = 9.99m,
            StockQuantity = 10,
            CategoryId = Guid.Empty
        };

        var response = await _http.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateProduct_WithNegativePrice_Returns400()
    {
        var request = new CreateProductRequest
        {
            Name = "NegativePrice",
            Price = -5m,
            StockQuantity = 10,
            CategoryId = Guid.Empty
        };

        var response = await _http.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Get

    [Test]
    public async Task GetProduct_WithExistingId_ReturnsProduct()
    {
        var productId = await CreateTestProduct();

        var response = await _http.GetAsync($"/api/products/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("TestProduct_");
    }

    [Test]
    public async Task GetProduct_WithNonExistentId_Returns404()
    {
        var response = await _http.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GetList

    [Test]
    public async Task GetProducts_ReturnsPaginatedList()
    {
        await CreateTestProduct();

        var response = await _http.GetAsync("/api/products?Page=1&PageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
    }

    [Test]
    public async Task GetProducts_WithSearchFilter_ReturnsFilteredResults()
    {
        var uniqueName = $"UniqueSearch_{Guid.NewGuid():N}";
        await CreateTestProduct(uniqueName);

        var response = await _http.GetAsync($"/api/products?Search={uniqueName}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(uniqueName);
    }

    #endregion

    #region Update

    [Test]
    public async Task UpdateProduct_WithValidData_Updates()
    {
        var productId = await CreateTestProduct();

        var updateRequest = new UpdateProductRequest
        {
            Name = "UpdatedProduct",
            Price = 29.99m,
            StockQuantity = 50,
            CategoryId = Guid.Empty
        };

        var response = await _http.PutAsJsonAsync($"/api/products/{productId}", updateRequest);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");
    }

    [Test]
    public async Task UpdateProduct_NonExistentId_Returns404()
    {
        var updateRequest = new UpdateProductRequest
        {
            Name = "Updated",
            Price = 10m,
            StockQuantity = 1,
            CategoryId = Guid.Empty
        };

        var response = await _http.PutAsJsonAsync($"/api/products/{Guid.NewGuid()}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Delete

    [Test]
    public async Task DeleteProduct_WithExistingId_Returns200()
    {
        var productId = await CreateTestProduct();

        var response = await _http.DeleteAsync($"/api/products/{productId}");

        response.IsSuccessStatusCode.Should().BeTrue();

        // Verify deleted (soft delete)
        var getResponse = await _http.GetAsync($"/api/products/{productId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteProduct_NonExistentId_Returns404()
    {
        var response = await _http.DeleteAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Helpers

    private async Task<Guid> CreateTestProduct(string? name = null)
    {
        var request = new CreateProductRequest
        {
            Name = name ?? $"TestProduct_{Guid.NewGuid():N}",
            Price = 9.99m,
            StockQuantity = 50,
            CategoryId = XFramework.TestInfrastructure.TestConstants.ProductCategoryId,
            IsAvailable = true
        };

        var response = await _http.PostAsJsonAsync("/api/products", request);
        response.IsSuccessStatusCode.Should().BeTrue();

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(body, JsonOpts);
        return result.GetProperty("id").GetGuid();
    }

    #endregion
}

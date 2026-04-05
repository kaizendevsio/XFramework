using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wallets.Api.Features.Wallets.Create;
using Wallets.Api.Features.Wallets.Shared;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
public class WalletCrudTests : WalletsTestBase
{
    #region Create Wallet (HTTP)

    [Test]
    public async Task Http_CreateWallet_WithValidData_ReturnsWallet()
    {
        var credential = await SeedCredential();

        var request = new CreateWalletRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            InitialBalance = 500m,
            TenantId = WalletsTestFixture.TestTenantId
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var wallet = await db.Set<Wallet>()
            .Where(w => w.CredentialId == credential.Id)
            .FirstOrDefaultAsync();

        wallet.Should().NotBeNull();
        wallet!.Balance.Should().Be(500m);
    }

    [Test]
    public async Task Http_CreateWallet_WithZeroBalance_Succeeds()
    {
        var credential = await SeedCredential();

        var request = new CreateWalletRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            InitialBalance = 0m,
            TenantId = WalletsTestFixture.TestTenantId
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets", request);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Get Wallet (HTTP)

    [Test]
    public async Task Http_GetWallet_WithExistingId_ReturnsWallet()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 750m);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/wallets/{wallet.Id}");
        request.Headers.Add("X-Tenant-Id", WalletsTestFixture.TestTenantId.ToString());

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<WalletResponse>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        result.Should().NotBeNull();
        result!.Balance.Should().Be(750m);
        result.CredentialId.Should().Be(credential.Id);
    }

    [Test]
    public async Task Http_GetWallet_WithNonExistentId_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/wallets/{Guid.NewGuid()}");
        request.Headers.Add("X-Tenant-Id", WalletsTestFixture.TestTenantId.ToString());

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Get Wallets by Credential (HTTP)

    [Test]
    public async Task Http_GetByCredential_WithExistingWallets_ReturnsList()
    {
        var credential = await SeedCredential();
        await SeedWallet(credential.Id, 100m);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/wallets/credential/{credential.Id}");
        request.Headers.Add("X-Tenant-Id", WalletsTestFixture.TestTenantId.ToString());

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        var wallets = JsonSerializer.Deserialize<List<WalletResponse>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        wallets.Should().NotBeNull();
        wallets!.Count.Should().BeGreaterOrEqualTo(1);
    }

    [Test]
    public async Task Http_GetByCredential_WithNoWallets_ReturnsEmptyList()
    {
        var credential = await SeedCredential();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/wallets/credential/{credential.Id}");
        request.Headers.Add("X-Tenant-Id", WalletsTestFixture.TestTenantId.ToString());

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}

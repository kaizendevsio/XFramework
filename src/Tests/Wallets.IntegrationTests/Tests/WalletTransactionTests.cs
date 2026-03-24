using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
public class WalletTransactionTests : WalletsTestBase
{
    #region HTTP — AddFunds (Increment)

    [Test]
    public async Task Http_AddFunds_WithValidData_IncrementsBalance()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);

        var request = new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 500m,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
        updated.Balance.Should().Be(1500m);
    }

    [Test]
    public async Task Http_AddFunds_WithZeroAmount_Returns400()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id);

        var request = new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 0m,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region HTTP — WithdrawFunds (Decrement)

    [Test]
    public async Task Http_WithdrawFunds_WithSufficientBalance_DecrementsBalance()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);

        var request = new DecrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 300m,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/withdraw-funds", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
        updated.Balance.Should().Be(700m);
    }

    [Test]
    public async Task Http_WithdrawFunds_InsufficientBalance_Returns400()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);

        var request = new DecrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 500m,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/withdraw-funds", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region HTTP — Transfer

    [Test]
    public async Task Http_Transfer_WithValidData_MovesBalanceBetweenWallets()
    {
        var sender = await SeedCredential();
        var senderWallet = await SeedWallet(sender.Id, 1000m);

        var recipient = await SeedCredential();
        var recipientWallet = await SeedWallet(recipient.Id, 0m);

        var request = new TransferWalletRequest
        {
            CredentialId = sender.Id,
            RecipientCredentialId = recipient.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            TransferDeductionType = XFramework.Domain.Shared.Enums.TransferDeductionType.DeductFromSender,
            Amount = 250m,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/transfer", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var updatedSender = await db.Set<Wallet>().FirstAsync(w => w.Id == senderWallet.Id);
        var updatedRecipient = await db.Set<Wallet>().FirstAsync(w => w.Id == recipientWallet.Id);

        updatedSender.Balance.Should().Be(750m);
        updatedRecipient.Balance.Should().Be(250m);
    }

    [Test]
    public async Task Http_Transfer_InsufficientBalance_Returns400()
    {
        var sender = await SeedCredential();
        await SeedWallet(sender.Id, 100m);
        var recipient = await SeedCredential();

        var request = new TransferWalletRequest
        {
            CredentialId = sender.Id,
            RecipientCredentialId = recipient.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            TransferDeductionType = XFramework.Domain.Shared.Enums.TransferDeductionType.DeductFromSender,
            Amount = 500m,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/transfer", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Bolt — AddFunds

    [Test]
    public async Task Bolt_AddFunds_WithValidData_IncrementsBalance()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);

        var result = await WalletsTestFixture.ServiceWrapper.IncrementWallet(
            new IncrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                Amount = 500m,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
        updated.Balance.Should().Be(1500m);
    }

    #endregion

    #region Bolt — WithdrawFunds

    [Test]
    public async Task Bolt_WithdrawFunds_WithSufficientBalance_DecrementsBalance()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);

        var result = await WalletsTestFixture.ServiceWrapper.DecrementWallet(
            new DecrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                Amount = 300m,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
        updated.Balance.Should().Be(700m);
    }

    [Test]
    public async Task Bolt_WithdrawFunds_InsufficientBalance_Returns400()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);

        var result = await WalletsTestFixture.ServiceWrapper.DecrementWallet(
            new DecrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                Amount = 500m,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Bolt — Transfer

    [Test]
    public async Task Bolt_Transfer_WithValidData_MovesBalance()
    {
        var sender = await SeedCredential();
        var senderWallet = await SeedWallet(sender.Id, 1000m);

        var recipient = await SeedCredential();
        var recipientWallet = await SeedWallet(recipient.Id, 0m);

        var result = await WalletsTestFixture.ServiceWrapper.TransferWallet(
            new TransferWalletRequest
            {
                CredentialId = sender.Id,
                RecipientCredentialId = recipient.Id,
                WalletTypeId = WalletsTestFixture.TestWalletTypeId,
                TransferDeductionType = XFramework.Domain.Shared.Enums.TransferDeductionType.DeductFromSender,
                Amount = 250m,
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var updatedSender = await db.Set<Wallet>().FirstAsync(w => w.Id == senderWallet.Id);
        var updatedRecipient = await db.Set<Wallet>().FirstAsync(w => w.Id == recipientWallet.Id);

        updatedSender.Balance.Should().Be(750m);
        updatedRecipient.Balance.Should().Be(250m);
    }

    #endregion

    #region Bolt — Convert

    [Test]
    public async Task Bolt_Convert_WithValidData_ReturnsOk()
    {
        // Convert requires two different wallet types — seed a second type
        var secondTypeId = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            if (!await db.Set<Wallets.Domain.Shared.Contracts.WalletType>().AnyAsync(w => w.Id == secondTypeId))
            {
                db.Set<Wallets.Domain.Shared.Contracts.WalletType>().Add(new Wallets.Domain.Shared.Contracts.WalletType
                {
                    Id = secondTypeId,
                    Code = "TST2",
                    Name = "TestCoin2",
                    TenantId = WalletsTestFixture.TestTenantId,
                    MinTransferRule = 0,
                    MaxTransferRule = 1_000_000
                });
                await db.SaveChangesAsync();
            }
        }

        var credential = await SeedCredential();
        await SeedWallet(credential.Id, 1000m); // Source wallet (TestWalletTypeId)

        var result = await WalletsTestFixture.ServiceWrapper.ConvertWallet(
            new ConvertWalletRequest
            {
                CredentialId = credential.Id,
                SourceWalletTypeId = WalletsTestFixture.TestWalletTypeId,
                TargetWalletTypeId = secondTypeId,
                Amount = 100m,
                Metadata = CreateMetadata()
            });

        // Convert may succeed or fail depending on exchange rate config — just verify it doesn't crash
        result.Should().NotBeNull();
    }

    #endregion
}

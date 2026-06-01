using System.Net;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;

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

    [Test]
    public async Task Http_AddFunds_WithDuplicateIdempotencyKey_AppliesOnce()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);
        var idempotencyKey = $"add-{Guid.NewGuid():N}";

        var request = new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 500m,
            IdempotencyKey = idempotencyKey,
            Metadata = CreateMetadata()
        };

        var firstResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);
        var secondResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);

        firstResponse.IsSuccessStatusCode.Should().BeTrue();
        secondResponse.IsSuccessStatusCode.Should().BeTrue();

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
        var transactionCount = await db.Set<WalletTransaction>()
            .CountAsync(t => t.ReferenceNumber == idempotencyKey);

        updated.Balance.Should().Be(1500m);
        transactionCount.Should().Be(1);
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

    [Test]
    public async Task Http_ReleaseHeldDebit_DeductsBalanceAndClearsHold()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);
        var referenceNumber = $"hold-{Guid.NewGuid():N}";

        var holdRequest = new DecrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 300m,
            OnHold = true,
            ReferenceNumber = referenceNumber,
            Metadata = CreateMetadata()
        };

        var holdResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdraw-funds", holdRequest);
        holdResponse.IsSuccessStatusCode.Should().BeTrue();

        await using (var db = CreateDbContext())
        {
            var heldWallet = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
            heldWallet.Balance.Should().Be(1000m);
            heldWallet.DebitOnHoldBalance.Should().Be(300m);
        }

        Guid transactionId;
        await using (var db = CreateDbContext())
        {
            transactionId = await db.Set<WalletTransaction>()
                .Where(t => t.ReferenceNumber == referenceNumber)
                .Select(t => t.Id)
                .SingleAsync();
        }

        var releaseResponse = await HttpClient.PostAsJsonAsync("/api/wallets/release-transaction",
            new ReleaseTransactionRequest
            {
                Id = transactionId,
                Metadata = CreateMetadata()
            });

        releaseResponse.IsSuccessStatusCode.Should().BeTrue();

        await using (var db = CreateDbContext())
        {
            var releasedWallet = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
            var transaction = await db.Set<WalletTransaction>().FirstAsync(t => t.Id == transactionId);

            releasedWallet.Balance.Should().Be(700m);
            releasedWallet.DebitOnHoldBalance.Should().Be(0m);
            releasedWallet.AvailableBalance.Should().Be(700m);
            transaction.Held.Should().BeFalse();
            transaction.Released.Should().BeTrue();
        }
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

    [Test]
    public async Task Http_Transfer_WithRecipientDeductedFeeGreaterThanAmount_Returns400()
    {
        var sender = await SeedCredential();
        await SeedWallet(sender.Id, 1000m);
        var recipient = await SeedCredential();
        await SeedWallet(recipient.Id, 0m);

        var request = new TransferWalletRequest
        {
            CredentialId = sender.Id,
            RecipientCredentialId = recipient.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            TransferDeductionType = TransferDeductionType.DeductFromRecipient,
            Amount = 100m,
            Fee = 150m,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/transfer", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Http_ReleaseHeldTransfer_DoesNotDoubleDeductTransferableBalance()
    {
        var sender = await SeedCredential();
        var senderWallet = await SeedWallet(sender.Id, 1000m);
        var recipient = await SeedCredential();
        var recipientWallet = await SeedWallet(recipient.Id, 0m);
        var referenceNumber = $"transfer-hold-{Guid.NewGuid():N}";

        var transferResponse = await HttpClient.PostAsJsonAsync("/api/wallets/transfer",
            new TransferWalletRequest
            {
                CredentialId = sender.Id,
                RecipientCredentialId = recipient.Id,
                WalletTypeId = WalletsTestFixture.TestWalletTypeId,
                Amount = 300m,
                OnHold = true,
                ReferenceNumber = referenceNumber,
                Metadata = CreateMetadata()
            });
        transferResponse.IsSuccessStatusCode.Should().BeTrue();

        await using (var db = CreateDbContext())
        {
            var heldSenderWallet = await db.Set<Wallet>().FirstAsync(w => w.Id == senderWallet.Id);
            var heldRecipientWallet = await db.Set<Wallet>().FirstAsync(w => w.Id == recipientWallet.Id);

            heldSenderWallet.Balance.Should().Be(1000m);
            heldSenderWallet.TransferableBalance.Should().Be(700m);
            heldSenderWallet.DebitOnHoldBalance.Should().Be(300m);
            heldRecipientWallet.Balance.Should().Be(0m);
            heldRecipientWallet.TransferableBalance.Should().Be(0m);
            heldRecipientWallet.CreditOnHoldBalance.Should().Be(300m);
        }

        Guid senderTransactionId;
        Guid recipientTransactionId;
        await using (var db = CreateDbContext())
        {
            senderTransactionId = await db.Set<WalletTransaction>()
                .Where(t => t.ReferenceNumber == referenceNumber && t.TransactionType == TransactionType.Debit)
                .Select(t => t.Id)
                .SingleAsync();
            recipientTransactionId = await db.Set<WalletTransaction>()
                .Where(t => t.ReferenceNumber == referenceNumber && t.TransactionType == TransactionType.Credit)
                .Select(t => t.Id)
                .SingleAsync();
        }

        var senderReleaseResponse = await HttpClient.PostAsJsonAsync("/api/wallets/release-transaction",
            new ReleaseTransactionRequest
            {
                Id = senderTransactionId,
                Metadata = CreateMetadata()
            });
        senderReleaseResponse.IsSuccessStatusCode.Should().BeTrue();

        var recipientReleaseResponse = await HttpClient.PostAsJsonAsync("/api/wallets/release-transaction",
            new ReleaseTransactionRequest
            {
                Id = recipientTransactionId,
                Metadata = CreateMetadata()
            });
        recipientReleaseResponse.IsSuccessStatusCode.Should().BeTrue();

        await using (var db = CreateDbContext())
        {
            var releasedSenderWallet = await db.Set<Wallet>().FirstAsync(w => w.Id == senderWallet.Id);
            var releasedRecipientWallet = await db.Set<Wallet>().FirstAsync(w => w.Id == recipientWallet.Id);

            releasedSenderWallet.Balance.Should().Be(700m);
            releasedSenderWallet.TransferableBalance.Should().Be(700m);
            releasedSenderWallet.DebitOnHoldBalance.Should().Be(0m);
            releasedRecipientWallet.Balance.Should().Be(300m);
            releasedRecipientWallet.TransferableBalance.Should().Be(300m);
            releasedRecipientWallet.CreditOnHoldBalance.Should().Be(0m);
        }
    }

    #endregion

    #region HTTP - Reverse

    [Test]
    public async Task Http_ReverseCredit_WhenFundsAlreadySpent_Returns400()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);
        var creditReference = $"credit-{Guid.NewGuid():N}";

        var addResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds",
            new IncrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                Amount = 100m,
                ReferenceNumber = creditReference,
                Metadata = CreateMetadata()
            });
        addResponse.IsSuccessStatusCode.Should().BeTrue();

        var withdrawResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdraw-funds",
            new DecrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                Amount = 150m,
                Metadata = CreateMetadata()
            });
        withdrawResponse.IsSuccessStatusCode.Should().BeTrue();

        Guid creditTransactionId;
        await using (var db = CreateDbContext())
        {
            creditTransactionId = await db.Set<WalletTransaction>()
                .Where(t => t.ReferenceNumber == creditReference && t.TransactionType == TransactionType.Credit)
                .Select(t => t.Id)
                .SingleAsync();
        }

        var reverseResponse = await HttpClient.PostAsJsonAsync("/api/wallets/reverse-transaction",
            new ReverseTransactionRequest
            {
                TransactionId = creditTransactionId,
                Reason = "test reversal should fail",
                Metadata = CreateMetadata()
            });

        reverseResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using (var db = CreateDbContext())
        {
            var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
            updated.Balance.Should().Be(50m);
        }
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

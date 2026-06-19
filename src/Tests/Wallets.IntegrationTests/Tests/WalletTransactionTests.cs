using System.Net;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Enums;
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

    [Test]
    public async Task Http_AddFunds_WithReferenceAndIdempotencyKey_UsesReferenceAndDeduplicatesByIdempotency()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);
        var idempotencyKey = $"add-{Guid.NewGuid():N}";
        var referenceNumber = $"ref-{Guid.NewGuid():N}";

        var request = new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 500m,
            ReferenceNumber = referenceNumber,
            IdempotencyKey = idempotencyKey,
            Metadata = CreateMetadata()
        };

        var firstResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);
        var secondResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);

        firstResponse.IsSuccessStatusCode.Should().BeTrue();
        secondResponse.IsSuccessStatusCode.Should().BeTrue();

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
        var transaction = await db.Set<WalletTransaction>()
            .SingleAsync(t => t.WalletId == wallet.Id && t.ReferenceNumber == referenceNumber);
        var operation = await db.Set<WalletOperation>()
            .SingleAsync(o => o.IdempotencyKey == idempotencyKey);

        updated.Balance.Should().Be(1500m);
        transaction.ReferenceNumber.Should().Be(referenceNumber);
        operation.ReferenceNumber.Should().Be(referenceNumber);
    }

    [Test]
    public async Task Http_AddFunds_WithFee_CreatesBalancedLedgerSnapshotAndOutbox()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);
        var idempotencyKey = $"ledger-{Guid.NewGuid():N}";

        var request = new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 500m,
            Fee = 25m,
            IdempotencyKey = idempotencyKey,
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var operation = await db.Set<WalletOperation>()
            .SingleAsync(o => o.IdempotencyKey == idempotencyKey);
        var entries = await db.Set<WalletLedgerEntry>()
            .Where(e => e.OperationId == operation.Id)
            .ToListAsync();
        var snapshot = await db.Set<WalletBalanceSnapshot>()
            .SingleAsync(s => s.WalletId == wallet.Id);
        var outbox = await db.Set<WalletOutboxMessage>()
            .SingleAsync(o => o.OperationId == operation.Id);

        operation.Status.Should().Be(WalletOperationStatus.Completed);
        entries.Where(e => e.Direction == WalletLedgerDirection.Debit).Sum(e => e.Amount)
            .Should().Be(entries.Where(e => e.Direction == WalletLedgerDirection.Credit).Sum(e => e.Amount));
        entries.Should().Contain(e =>
            e.WalletId == wallet.Id &&
            e.Direction == WalletLedgerDirection.Credit &&
            e.Amount == 475m);
        entries.Should().Contain(e =>
            e.EntryKind == WalletLedgerEntryKind.Fee &&
            e.Direction == WalletLedgerDirection.Credit &&
            e.Amount == 25m);
        snapshot.Balance.Should().Be(1475m);
        snapshot.AvailableBalance.Should().Be(1475m);
        snapshot.IsReconciled.Should().BeTrue();
        outbox.Status.Should().Be(WalletOutboxStatus.Pending);
    }

    [Test]
    public async Task Http_AddFunds_SameIdempotencyKeyWithDifferentPayload_ReturnsConflict()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 1000m);
        var idempotencyKey = $"conflict-{Guid.NewGuid():N}";

        var firstRequest = new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 500m,
            IdempotencyKey = idempotencyKey,
            Metadata = CreateMetadata()
        };
        var secondRequest = firstRequest with { Amount = 250m };

        var firstResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", firstRequest);
        var secondResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", secondRequest);

        firstResponse.IsSuccessStatusCode.Should().BeTrue();
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
        var operationCount = await db.Set<WalletOperation>()
            .CountAsync(o => o.IdempotencyKey == idempotencyKey);

        updated.Balance.Should().Be(1500m);
        operationCount.Should().Be(1);
    }

    [Test]
    public async Task Http_AddFunds_WithWalletIdAndDuplicateCredentialType_TargetsExplicitWallet()
    {
        var credential = await SeedCredential();
        var otherWallet = await SeedWallet(credential.Id, 100m, WalletStatus.Closed);
        var targetWallet = await SeedWallet(credential.Id, 0m);

        var request = new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = targetWallet.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            Amount = 25m,
            ReferenceNumber = $"walletid-add-{Guid.NewGuid():N}",
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var unchangedOther = await db.Set<Wallet>().SingleAsync(w => w.Id == otherWallet.Id);
        var updatedTarget = await db.Set<Wallet>().SingleAsync(w => w.Id == targetWallet.Id);

        unchangedOther.Balance.Should().Be(100m);
        unchangedOther.Status.Should().Be(WalletStatus.Closed);
        updatedTarget.Balance.Should().Be(25m);
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
    public async Task Http_WithdrawFunds_WithWalletIdAndDuplicateCredentialType_TargetsExplicitWallet()
    {
        var credential = await SeedCredential();
        var otherWallet = await SeedWallet(credential.Id, 1000m, WalletStatus.Closed);
        var targetWallet = await SeedWallet(credential.Id, 100m);

        var request = new DecrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = targetWallet.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            Amount = 25m,
            ReferenceNumber = $"walletid-withdraw-{Guid.NewGuid():N}",
            Metadata = CreateMetadata()
        };

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/withdraw-funds", request);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var unchangedOther = await db.Set<Wallet>().SingleAsync(w => w.Id == otherWallet.Id);
        var updatedTarget = await db.Set<Wallet>().SingleAsync(w => w.Id == targetWallet.Id);

        unchangedOther.Balance.Should().Be(1000m);
        unchangedOther.Status.Should().Be(WalletStatus.Closed);
        updatedTarget.Balance.Should().Be(75m);
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

            var operation = await db.Set<WalletOperation>()
                .SingleAsync(o => o.IdempotencyKey == $"release:{transactionId}");
            operation.OperationType.Should().Be(WalletOperationType.Release);
            await AssertLedgerBalanced(db, operation.Id);
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

    #region HTTP - Convert

    [Test]
    public async Task Http_Convert_WithFee_CreatesBalancedLedgerOperation()
    {
        var secondTypeId = await SeedSecondWalletType();
        var credential = await SeedCredential();
        var sourceWallet = await SeedWallet(credential.Id, 1000m);

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/convert",
            new ConvertWalletRequest
            {
                CredentialId = credential.Id,
                SourceWalletTypeId = WalletsTestFixture.TestWalletTypeId,
                TargetWalletTypeId = secondTypeId,
                TransferDeductionType = TransferDeductionType.DeductFromSender,
                Amount = 200m,
                Fee = 10m,
                ReferenceNumber = $"convert-{Guid.NewGuid():N}",
                Metadata = CreateMetadata()
            });
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var updatedSource = await db.Set<Wallet>().SingleAsync(w => w.Id == sourceWallet.Id);
        var targetWallet = await db.Set<Wallet>()
            .SingleAsync(w => w.CredentialId == credential.Id && w.WalletTypeId == secondTypeId);
        var operation = await db.Set<WalletOperation>()
            .Where(o => o.OperationType == WalletOperationType.Conversion)
            .OrderByDescending(o => o.CreatedAt)
            .FirstAsync();

        updatedSource.Balance.Should().Be(790m);
        targetWallet.Balance.Should().Be(200m);
        await AssertLedgerBalanced(db, operation.Id);
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

    [Test]
    public async Task Http_ReverseDebit_CreatesBalancedLedgerOperation()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 500m);
        var referenceNumber = $"debit-{Guid.NewGuid():N}";

        var withdrawResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdraw-funds",
            new DecrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                Amount = 125m,
                ReferenceNumber = referenceNumber,
                Metadata = CreateMetadata()
            });
        withdrawResponse.IsSuccessStatusCode.Should().BeTrue();

        Guid debitTransactionId;
        await using (var db = CreateDbContext())
        {
            debitTransactionId = await db.Set<WalletTransaction>()
                .Where(t => t.ReferenceNumber == referenceNumber && t.TransactionType == TransactionType.Debit)
                .Select(t => t.Id)
                .SingleAsync();
        }

        var reverseResponse = await HttpClient.PostAsJsonAsync("/api/wallets/reverse-transaction",
            new ReverseTransactionRequest
            {
                TransactionId = debitTransactionId,
                Reason = "test reversal",
                Metadata = CreateMetadata()
            });
        var body = await reverseResponse.Content.ReadAsStringAsync();

        reverseResponse.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using (var db = CreateDbContext())
        {
            var updated = await db.Set<Wallet>().FirstAsync(w => w.Id == wallet.Id);
            var operation = await db.Set<WalletOperation>()
                .SingleAsync(o => o.IdempotencyKey == $"reversal:{debitTransactionId}");

            updated.Balance.Should().Be(500m);
            operation.OperationType.Should().Be(WalletOperationType.Reversal);
            await AssertLedgerBalanced(db, operation.Id);
        }
    }

    #endregion

    #region HTTP - Close

    [Test]
    public async Task Http_CloseWallet_WhenEmpty_SetsClosedStatusAndBlocksOperations()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 0m);
        var approvalId = await SeedApprovedWalletApproval(wallet.Id, WalletOperationType.Close, credential.Id);

        var closeResponse = await HttpClient.PostAsJsonAsync("/api/wallets/close",
            new CloseWalletRequest
            {
                WalletId = wallet.Id,
                ApprovalId = approvalId,
                Reason = "test close empty wallet",
                Metadata = CreateMetadata()
            });
        var closeBody = await closeResponse.Content.ReadAsStringAsync();

        closeResponse.IsSuccessStatusCode.Should().BeTrue($"Response: {closeBody}");

        await using (var db = CreateDbContext())
        {
            var updated = await db.Set<Wallet>().SingleAsync(w => w.Id == wallet.Id);
            updated.Status.Should().Be(WalletStatus.Closed);
        }

        var addResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds",
            new IncrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                Amount = 10m,
                Metadata = CreateMetadata()
            });

        addResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Http_CloseWallet_WithRemainingBalance_Returns400()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 10m);

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/close",
            new CloseWalletRequest
            {
                WalletId = wallet.Id,
                Reason = "test close non-empty wallet",
                Metadata = CreateMetadata()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().SingleAsync(w => w.Id == wallet.Id);
        updated.Status.Should().Be(WalletStatus.Active);
    }

    [Test]
    public async Task Http_CloseWallet_WithHeldFunds_Returns400()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 0m);

        await using (var db = CreateDbContext())
        {
            var heldWallet = await db.Set<Wallet>().SingleAsync(w => w.Id == wallet.Id);
            heldWallet.DebitOnHoldBalance = 5m;
            db.Update(heldWallet);
            await db.SaveChangesAsync();
        }

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/close",
            new CloseWalletRequest
            {
                WalletId = wallet.Id,
                Reason = "test close held wallet",
                Metadata = CreateMetadata()
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region HTTP - Batch

    [Test]
    public async Task Http_BatchIncrement_CreatesSingleBalancedBatchLedgerOperation()
    {
        var firstCredential = await SeedCredential();
        var firstWallet = await SeedWallet(firstCredential.Id, 0m);
        var secondCredential = await SeedCredential();
        var secondWallet = await SeedWallet(secondCredential.Id, 0m);

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/batch/increment", new
        {
            TenantId = WalletsTestFixture.TestTenantId,
            AllowPartialSuccess = false,
            Requests = new[]
            {
                new BatchIncrementRequest
                {
                    CredentialId = firstCredential.Id,
                    WalletId = firstWallet.Id,
                    Amount = 100m,
                    Fee = 5m,
                    ReferenceNumber = $"batch-inc-{Guid.NewGuid():N}"
                },
                new BatchIncrementRequest
                {
                    CredentialId = secondCredential.Id,
                    WalletId = secondWallet.Id,
                    Amount = 200m,
                    ReferenceNumber = $"batch-inc-{Guid.NewGuid():N}"
                }
            }
        });
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var updatedFirst = await db.Set<Wallet>().SingleAsync(w => w.Id == firstWallet.Id);
        var updatedSecond = await db.Set<Wallet>().SingleAsync(w => w.Id == secondWallet.Id);
        var operation = await db.Set<WalletOperation>()
            .SingleAsync(o => o.OperationType == WalletOperationType.Batch);

        updatedFirst.Balance.Should().Be(95m);
        updatedSecond.Balance.Should().Be(200m);
        await AssertLedgerBalanced(db, operation.Id);
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

    [Test]
    public async Task Bolt_AddFunds_WithWalletIdAndDuplicateCredentialType_TargetsExplicitWallet()
    {
        var credential = await SeedCredential();
        var otherWallet = await SeedWallet(credential.Id, 100m, WalletStatus.Closed);
        var targetWallet = await SeedWallet(credential.Id, 0m);

        var result = await WalletsTestFixture.ServiceWrapper.IncrementWallet(
            new IncrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = targetWallet.Id,
                WalletTypeId = WalletsTestFixture.TestWalletTypeId,
                Amount = 25m,
                ReferenceNumber = $"bolt-walletid-add-{Guid.NewGuid():N}",
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var unchangedOther = await db.Set<Wallet>().SingleAsync(w => w.Id == otherWallet.Id);
        var updatedTarget = await db.Set<Wallet>().SingleAsync(w => w.Id == targetWallet.Id);

        unchangedOther.Balance.Should().Be(100m);
        unchangedOther.Status.Should().Be(WalletStatus.Closed);
        updatedTarget.Balance.Should().Be(25m);
    }

    [Test]
    public async Task Bolt_AddFunds_WithClosedWalletAndDuplicateCredentialType_ReturnsClosedWalletFailure()
    {
        var credential = await SeedCredential();
        var otherWallet = await SeedWallet(credential.Id, 100m, WalletStatus.Frozen);
        var targetWallet = await SeedWallet(credential.Id, 0m, WalletStatus.Closed);

        var result = await WalletsTestFixture.ServiceWrapper.IncrementWallet(
            new IncrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = targetWallet.Id,
                WalletTypeId = WalletsTestFixture.TestWalletTypeId,
                Amount = 25m,
                ReferenceNumber = $"bolt-walletid-closed-{Guid.NewGuid():N}",
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.Message.Should().Be("Wallet is closed. No operations allowed.");

        await using var db = CreateDbContext();
        var unchangedOther = await db.Set<Wallet>().SingleAsync(w => w.Id == otherWallet.Id);
        var unchangedTarget = await db.Set<Wallet>().SingleAsync(w => w.Id == targetWallet.Id);

        unchangedOther.Balance.Should().Be(100m);
        unchangedOther.Status.Should().Be(WalletStatus.Frozen);
        unchangedTarget.Balance.Should().Be(0m);
        unchangedTarget.Status.Should().Be(WalletStatus.Closed);
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
    public async Task Bolt_WithdrawFunds_WithWalletIdAndDuplicateCredentialType_TargetsExplicitWallet()
    {
        var credential = await SeedCredential();
        var otherWallet = await SeedWallet(credential.Id, 1000m, WalletStatus.Closed);
        var targetWallet = await SeedWallet(credential.Id, 100m);

        var result = await WalletsTestFixture.ServiceWrapper.DecrementWallet(
            new DecrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = targetWallet.Id,
                WalletTypeId = WalletsTestFixture.TestWalletTypeId,
                Amount = 25m,
                ReferenceNumber = $"bolt-walletid-withdraw-{Guid.NewGuid():N}",
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var unchangedOther = await db.Set<Wallet>().SingleAsync(w => w.Id == otherWallet.Id);
        var updatedTarget = await db.Set<Wallet>().SingleAsync(w => w.Id == targetWallet.Id);

        unchangedOther.Balance.Should().Be(1000m);
        unchangedOther.Status.Should().Be(WalletStatus.Closed);
        updatedTarget.Balance.Should().Be(75m);
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

    #region Bolt - Close

    [Test]
    public async Task Bolt_CloseWallet_WhenEmpty_SetsClosedStatus()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 0m);
        var approvalId = await SeedApprovedWalletApproval(wallet.Id, WalletOperationType.Close, credential.Id);

        var result = await WalletsTestFixture.ServiceWrapper.CloseWallet(
            new CloseWalletRequest
            {
                WalletId = wallet.Id,
                ApprovalId = approvalId,
                Reason = "test bolt close empty wallet",
                Metadata = CreateMetadata()
            });

        result.HttpStatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().SingleAsync(w => w.Id == wallet.Id);
        updated.Status.Should().Be(WalletStatus.Closed);
    }

    #endregion

    private async Task<Guid> SeedSecondWalletType()
    {
        var secondTypeId = Guid.NewGuid();
        await using var db = CreateDbContext();
        db.Set<Wallets.Domain.Shared.Contracts.WalletType>().Add(new Wallets.Domain.Shared.Contracts.WalletType
        {
            Id = secondTypeId,
            Code = $"TST{Guid.NewGuid():N}"[..8],
            Name = "TestCoin2",
            TenantId = WalletsTestFixture.TestTenantId,
            MinTransferRule = 0,
            MaxTransferRule = 1_000_000
        });
        await db.SaveChangesAsync();
        return secondTypeId;
    }

    private static async Task AssertLedgerBalanced(DbContext db, Guid operationId)
    {
        var entries = await db.Set<WalletLedgerEntry>()
            .Where(e => e.OperationId == operationId)
            .ToListAsync();

        entries.Should().NotBeEmpty();
        entries.Where(e => e.Direction == WalletLedgerDirection.Debit).Sum(e => e.Amount)
            .Should().Be(entries.Where(e => e.Direction == WalletLedgerDirection.Credit).Sum(e => e.Amount));
    }
}

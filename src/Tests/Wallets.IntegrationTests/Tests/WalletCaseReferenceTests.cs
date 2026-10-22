using System.Net;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Enums;
using XFramework.Domain.Shared.Enums;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
[NonParallelizable]
public sealed class WalletCaseReferenceTests : WalletsTestBase
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Refund_TransactionOnly_SelectsWithdrawalSettlement_NotHold_AndPreservesLimit(bool legacyCase)
    {
        var owner = await SeedCredential();
        var checker = await SeedCredential();
        var wallet = await SeedWallet(owner.Id, 200m);
        using var maker = WalletsTestFixture.PushActor(owner.Id);
        var withdrawal = await WalletsTestFixture.ServiceWrapper.CreateWithdrawalWorkflow(new()
        {
            WalletId = wallet.Id, CredentialId = owner.Id, Amount = 40m, Metadata = CreateMetadata()
        });
        withdrawal.IsSuccess.Should().BeTrue(withdrawal.Message);
        using (WalletsTestFixture.PushActor(checker.Id))
        {
            var approved = await WalletsTestFixture.ServiceWrapper.ApproveWithdrawalWorkflow(new()
            {
                RequestId = withdrawal.Response!.Id, Metadata = CreateMetadata()
            });
            approved.IsSuccess.Should().BeTrue(approved.Message);
        }

        await using var db = CreateDbContext();
        var held = await db.Set<WithdrawalRequest>().AsNoTracking().SingleAsync(x => x.Id == withdrawal.Response!.Id);
        var transactionId = held.SettlementTransactionId!.Value;
        var createRequest = new CreateWalletCaseRequest
        {
            WalletId = wallet.Id, OriginalTransactionId = transactionId, CaseType = WalletCaseType.Refund,
            Amount = 15m, Reason = "Transaction-only refund", Metadata = CreateMetadata()
        };
        var holdOnly = await WalletsTestFixture.ServiceWrapper.CreateWalletCase(createRequest);
        holdOnly.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);

        using (WalletsTestFixture.PushActor(checker.Id))
        {
            var settled = await WalletsTestFixture.ServiceWrapper.SettleWithdrawalWorkflow(new()
            {
                RequestId = held.Id, Metadata = CreateMetadata()
            });
            settled.IsSuccess.Should().BeTrue(settled.Message);
        }
        var settledWithdrawal = await db.Set<WithdrawalRequest>().AsNoTracking().SingleAsync(x => x.Id == held.Id);
        Guid caseId;
        if (legacyCase)
        {
            var retained = new WalletCase
            {
                Id = Guid.NewGuid(), TenantId = wallet.TenantId, WalletId = wallet.Id,
                OriginalTransactionId = transactionId, CaseType = WalletCaseType.Refund,
                Status = WalletCaseStatus.Open, Amount = 15m, RequesterCredentialId = owner.Id
            };
            db.Add(retained);
            await db.SaveChangesAsync();
            caseId = retained.Id;
        }
        else
        {
            var created = await WalletsTestFixture.ServiceWrapper.CreateWalletCase(createRequest);
            created.IsSuccess.Should().BeTrue(created.Message);
            caseId = created.Response!.CaseId;
            var linked = await db.Set<WalletCase>().AsNoTracking().SingleAsync(x => x.Id == caseId);
            linked.OriginalOperationId.Should().Be(settledWithdrawal.SettlementOperationId);
            linked.OriginalOperationId.Should().NotBe(held.HoldOperationId!.Value);
        }

        using (WalletsTestFixture.PushActor(checker.Id))
        {
            var resolve = new ResolveWalletCaseRequest
            {
                CaseId = caseId, Approve = true, Reason = "Checker approved", Metadata = CreateMetadata()
            };
            var result = await WalletsTestFixture.ServiceWrapper.ResolveWalletCase(resolve);
            result.IsSuccess.Should().BeTrue(result.Message);
            (await WalletsTestFixture.ServiceWrapper.ResolveWalletCase(resolve)).IsSuccess.Should().BeTrue();
        }
        var completed = await db.Set<WalletCase>().AsNoTracking().SingleAsync(x => x.Id == caseId);
        completed.Status.Should().Be(WalletCaseStatus.Resolved);
        completed.OriginalOperationId.Should().Be(settledWithdrawal.SettlementOperationId);
        var refundOperation = await db.Set<WalletOperation>().SingleAsync(x => x.Id == completed.SettlementOperationId);
        refundOperation.OriginalOperationId.Should().Be(settledWithdrawal.SettlementOperationId);
        (await db.Set<Wallet>().AsNoTracking().SingleAsync(x => x.Id == wallet.Id)).Balance.Should().Be(175m);

        var excessive = await WalletsTestFixture.ServiceWrapper.CreateWalletCase(createRequest with { Amount = 30m });
        excessive.IsSuccess.Should().BeTrue(excessive.Message);
        using (WalletsTestFixture.PushActor(checker.Id))
        {
            var rejected = await WalletsTestFixture.ServiceWrapper.ResolveWalletCase(new()
            {
                CaseId = excessive.Response!.CaseId, Approve = true, Reason = "Exceeds remaining debit", Metadata = CreateMetadata()
            });
            rejected.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
            rejected.Message.Should().Contain("remaining refundable debit");
        }
        (await db.Set<Wallet>().AsNoTracking().SingleAsync(x => x.Id == wallet.Id)).Balance.Should().Be(175m);
    }

    [TestCase(WalletCaseType.Refund)]
    [TestCase(WalletCaseType.Dispute)]
    [TestCase(WalletCaseType.Chargeback)]
    public async Task Create_UnlinkedCase_RejectsWithoutPersisting(WalletCaseType caseType)
    {
        var owner = await SeedCredential();
        var wallet = await SeedWallet(owner.Id);
        using var actor = WalletsTestFixture.PushActor(owner.Id);
        var response = await WalletsTestFixture.ServiceWrapper.CreateWalletCase(new()
        {
            WalletId = wallet.Id, CaseType = caseType, Amount = 1m, Metadata = CreateMetadata()
        });
        response.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var db = CreateDbContext();
        (await db.Set<WalletCase>().AnyAsync(x => x.WalletId == wallet.Id)).Should().BeFalse();
    }

    [TestCase("missing-transaction")]
    [TestCase("empty-transaction")]
    [TestCase("deleted-transaction")]
    [TestCase("other-wallet")]
    [TestCase("foreign-transaction")]
    [TestCase("foreign-operation")]
    [TestCase("pending-operation")]
    [TestCase("deleted-operation")]
    [TestCase("deleted-entry")]
    [TestCase("credit-only")]
    [TestCase("mismatched-operation")]
    [TestCase("ambiguous-operation")]
    public async Task Create_InvalidOriginalReference_RejectsWithoutPersisting(string scenario)
    {
        var owner = await SeedCredential();
        var wallet = await SeedWallet(owner.Id);
        var originalWallet = scenario == "other-wallet" ? await SeedWallet((await SeedCredential()).Id) : wallet;
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(), TenantId = scenario == "foreign-transaction" ? Guid.NewGuid() : wallet.TenantId,
            WalletId = originalWallet.Id, CredentialId = owner.Id, Amount = 20m,
            TransactionType = TransactionType.Debit, IsDeleted = scenario == "deleted-transaction"
        };
        var operation = new WalletOperation
        {
            Id = Guid.NewGuid(), TenantId = scenario == "foreign-operation" ? Guid.NewGuid() : wallet.TenantId,
            OperationType = WalletOperationType.Debit,
            Status = scenario == "pending-operation" ? WalletOperationStatus.Pending : WalletOperationStatus.Completed,
            IsDeleted = scenario == "deleted-operation"
        };
        await using (var transactionDb = CreateDbContext(transaction.TenantId))
        {
            transactionDb.Add(transaction);
            await transactionDb.SaveChangesAsync();
        }
        await using (var operationDb = CreateDbContext(operation.TenantId))
        {
            operationDb.Add(operation);
            await operationDb.SaveChangesAsync();
        }
        await using var db = CreateDbContext();
        db.Add(new WalletLedgerEntry
        {
            Id = Guid.NewGuid(), TenantId = wallet.TenantId, WalletId = originalWallet.Id,
            WalletTransactionId = transaction.Id, OperationId = operation.Id,
            Amount = 20m, BalanceBucket = WalletBalanceBucket.Available,
            Direction = scenario == "credit-only" ? WalletLedgerDirection.Credit : WalletLedgerDirection.Debit,
            IsDeleted = scenario == "deleted-entry"
        });
        if (scenario == "ambiguous-operation")
        {
            var second = new WalletOperation { Id = Guid.NewGuid(), TenantId = wallet.TenantId, Status = WalletOperationStatus.Completed };
            db.Add(second);
            db.Add(new WalletLedgerEntry
            {
                Id = Guid.NewGuid(), TenantId = wallet.TenantId, WalletId = wallet.Id,
                WalletTransactionId = transaction.Id, OperationId = second.Id, Amount = 20m,
                BalanceBucket = WalletBalanceBucket.Available, Direction = WalletLedgerDirection.Debit
            });
        }
        await db.SaveChangesAsync();
        using var actor = WalletsTestFixture.PushActor(owner.Id);
        var response = await WalletsTestFixture.ServiceWrapper.CreateWalletCase(new()
        {
            WalletId = wallet.Id, CaseType = WalletCaseType.Refund, Amount = 1m,
            OriginalTransactionId = scenario switch
            {
                "missing-transaction" => Guid.NewGuid(), "empty-transaction" => Guid.Empty, _ => transaction.Id
            },
            OriginalOperationId = scenario == "mismatched-operation" ? Guid.NewGuid() : null,
            Metadata = CreateMetadata()
        });
        response.HttpStatusCode.Should().Be(HttpStatusCode.BadRequest, response.Message);
        (await db.Set<WalletCase>().AnyAsync(x => x.WalletId == wallet.Id)).Should().BeFalse();
    }

    [Test]
    public async Task Resolve_LegacyUnlinkedCase_AllowsRejectionButNotApproval()
    {
        var owner = await SeedCredential();
        var checker = await SeedCredential();
        var wallet = await SeedWallet(owner.Id);
        var retained = new WalletCase
        {
            Id = Guid.NewGuid(), TenantId = wallet.TenantId, WalletId = wallet.Id,
            CaseType = WalletCaseType.Refund, Status = WalletCaseStatus.Open, Amount = 1m,
            RequesterCredentialId = owner.Id
        };
        await using var db = CreateDbContext();
        db.Add(retained);
        await db.SaveChangesAsync();
        using var actor = WalletsTestFixture.PushActor(checker.Id);
        var request = new ResolveWalletCaseRequest
        {
            CaseId = retained.Id, Approve = true, Reason = "Legacy case", Metadata = CreateMetadata()
        };
        (await WalletsTestFixture.ServiceWrapper.ResolveWalletCase(request)).HttpStatusCode.Should().Be(HttpStatusCode.BadRequest);
        var rejected = await WalletsTestFixture.ServiceWrapper.ResolveWalletCase(request with { Approve = false });
        rejected.IsSuccess.Should().BeTrue(rejected.Message);
        (await db.Set<WalletCase>().AsNoTracking().SingleAsync(x => x.Id == retained.Id)).Status.Should().Be(WalletCaseStatus.Rejected);
    }
}

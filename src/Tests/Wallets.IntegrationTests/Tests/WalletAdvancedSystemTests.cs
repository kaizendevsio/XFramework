using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wallets.Api.Events;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using Wallets.Domain.Shared.Enums;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;
using SharedContracts = XFramework.Domain.Shared.Contracts;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
public class WalletAdvancedSystemTests : WalletsTestBase
{
    private static readonly Guid SamplePaymentGatewayId = Guid.Parse("2ab0a82e-4c3b-4ed4-9e8b-1e9b71918a6d");

    [Test]
    public async Task Http_DepositAndWithdrawalRequestLifecycle_CreatesAndUpdatesWorkflowRecords()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 500m);
        var gatewayId = await SeedPaymentGateway();
        var depositReference = $"dep-{Guid.NewGuid():N}";
        var withdrawalReference = $"wd-{Guid.NewGuid():N}";

        var depositResponse = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            GatewayId = gatewayId,
            Amount = 125m,
            ExternalReference = depositReference,
            RequestedFee = 5m,
            Remarks = "maker submitted deposit",
            Metadata = CreateMetadata()
        });
        var depositBody = await depositResponse.Content.ReadAsStringAsync();

        depositResponse.IsSuccessStatusCode.Should().BeTrue($"Response: {depositBody}");

        await using var db = CreateDbContext();
        var deposit = await db.Set<DepositRequest>().SingleAsync(x => x.ExternalReference == depositReference);
        deposit.TenantId.Should().Be(WalletsTestFixture.TestTenantId);
        deposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.PendingApproval);

        var approveDepositResponse = await PostAsJsonAsActorAsync("/api/wallets/deposits/approve", new ApproveDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            Reason = "checker approved deposit",
            Metadata = CreateMetadata()
        }, checker.Id);
        var approveDepositBody = await approveDepositResponse.Content.ReadAsStringAsync();
        approveDepositResponse.IsSuccessStatusCode.Should().BeTrue(approveDepositBody);
        db.ChangeTracker.Clear();
        var approvedDeposit = await db.Set<DepositRequest>().SingleAsync(x => x.Id == deposit.Id);
        approvedDeposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.Approved, approveDepositBody);

        var settleDepositResponse = await HttpClient.PostAsJsonAsync("/api/wallets/deposits/settle", new SettleDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            ProviderEventId = $"evt-{Guid.NewGuid():N}",
            ProviderTransactionId = $"provider-tx-{Guid.NewGuid():N}",
            ProviderStatus = "completed",
            RawProviderPayloadJson = """{"approvedBy":"checker"}""",
            Metadata = CreateMetadata()
        });
        settleDepositResponse.IsSuccessStatusCode.Should().BeTrue(await settleDepositResponse.Content.ReadAsStringAsync());

        var withdrawalResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals", new CreateWithdrawalWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 75m,
            RequestedFee = 5m,
            ExternalReference = withdrawalReference,
            Address = "test-address",
            Remarks = "maker submitted withdrawal",
            Metadata = CreateMetadata()
        });
        var withdrawalBody = await withdrawalResponse.Content.ReadAsStringAsync();

        withdrawalResponse.IsSuccessStatusCode.Should().BeTrue($"Response: {withdrawalBody}");
        var withdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.ExternalReference == withdrawalReference);
        withdrawal.TenantId.Should().Be(WalletsTestFixture.TestTenantId);
        withdrawal.WorkflowStatus.Should().Be(WalletWorkflowStatus.PendingApproval);

        var approveWithdrawalResponse = await PostAsJsonAsActorAsync("/api/wallets/withdrawals/approve", new ApproveWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            Reason = "checker accepted withdrawal",
            Metadata = CreateMetadata()
        }, checker.Id);
        var approveWithdrawalBody = await approveWithdrawalResponse.Content.ReadAsStringAsync();
        approveWithdrawalResponse.IsSuccessStatusCode.Should().BeTrue(approveWithdrawalBody);
        db.ChangeTracker.Clear();
        var approvedWithdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.Id == withdrawal.Id);
        approvedWithdrawal.WorkflowStatus.Should().Be(WalletWorkflowStatus.Approved, approveWithdrawalBody);

        var settleWithdrawalResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals/settle", new SettleWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            ProviderEventId = $"evt-{Guid.NewGuid():N}",
            ProviderTransactionId = $"provider-tx-{Guid.NewGuid():N}",
            ProviderStatus = "completed",
            Metadata = CreateMetadata()
        });
        settleWithdrawalResponse.IsSuccessStatusCode.Should().BeTrue(await settleWithdrawalResponse.Content.ReadAsStringAsync());

        db.ChangeTracker.Clear();
        var updatedDeposit = await db.Set<DepositRequest>().SingleAsync(x => x.Id == deposit.Id);
        var updatedWithdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.Id == withdrawal.Id);

        updatedDeposit.DepositStatus.Should().Be((short)DepositStatus.Paid);
        updatedDeposit.RawResponseData.Should().Contain("checker");
        updatedDeposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.Completed);
        updatedDeposit.SettlementOperationId.Should().NotBeNull();
        updatedDeposit.SettlementTransactionId.Should().NotBeNull();
        updatedWithdrawal.WithdrawalStatus.Should().Be(TransactionStatus.Completed);
        updatedWithdrawal.WorkflowStatus.Should().Be(WalletWorkflowStatus.Completed);
        updatedWithdrawal.SettlementOperationId.Should().NotBeNull();
        updatedWithdrawal.SettlementTransactionId.Should().NotBeNull();

        var withdrawalSettlementEntries = await db.Set<WalletLedgerEntry>()
            .Where(x => x.OperationId == updatedWithdrawal.SettlementOperationId)
            .ToListAsync();
        withdrawalSettlementEntries.Where(x => x.Direction == WalletLedgerDirection.Debit).Sum(x => x.Amount)
            .Should().Be(withdrawalSettlementEntries.Where(x => x.Direction == WalletLedgerDirection.Credit).Sum(x => x.Amount));
        withdrawalSettlementEntries.Should().Contain(x =>
            x.WalletId == wallet.Id &&
            x.Direction == WalletLedgerDirection.Debit &&
            x.BalanceBucket == WalletBalanceBucket.Available &&
            x.Amount == 80m);
        withdrawalSettlementEntries.Should().Contain(x =>
            x.Direction == WalletLedgerDirection.Debit &&
            x.BalanceBucket == WalletBalanceBucket.External &&
            x.EntryKind == WalletLedgerEntryKind.SystemCounterparty &&
            x.Amount == 5m);
        withdrawalSettlementEntries.Should().Contain(x =>
            x.Direction == WalletLedgerDirection.Credit &&
            x.BalanceBucket == WalletBalanceBucket.Fee &&
            x.EntryKind == WalletLedgerEntryKind.Fee &&
            x.Amount == 5m);
    }

    [Test]
    public async Task Http_RegisteredPaymentGateway_InitiatesDepositAndSettlesWithdrawalThroughProvider()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 500m);
        var gatewayId = await SeedPaymentGateway(SamplePaymentGatewayId, "Sample Payment Provider");
        var depositReference = $"provider-dep-{Guid.NewGuid():N}";
        var withdrawalReference = $"provider-wd-{Guid.NewGuid():N}";

        var depositResponse = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            GatewayId = gatewayId,
            Amount = 80m,
            ExternalReference = depositReference,
            Metadata = CreateMetadata()
        });
        depositResponse.IsSuccessStatusCode.Should().BeTrue(await depositResponse.Content.ReadAsStringAsync());

        await using var db = CreateDbContext();
        var deposit = await db.Set<DepositRequest>().SingleAsync(x => x.ExternalReference == depositReference);
        deposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.PendingApproval);
        deposit.ProviderTransactionId.Should().Be(depositReference);
        deposit.ProviderStatus.Should().Contain("Pending");
        deposit.RawResponseData.Should().Contain("CallbackUrl");
        deposit.SettlementOperationId.Should().BeNull();

        var createWithdrawalResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals", new CreateWithdrawalWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            GatewayId = gatewayId,
            Amount = 60m,
            RequestedFee = 4m,
            ExternalReference = withdrawalReference,
            Metadata = CreateMetadata()
        });
        createWithdrawalResponse.IsSuccessStatusCode.Should().BeTrue(await createWithdrawalResponse.Content.ReadAsStringAsync());

        db.ChangeTracker.Clear();
        var withdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.ExternalReference == withdrawalReference);
        var approveWithdrawalResponse = await PostAsJsonAsActorAsync("/api/wallets/withdrawals/approve", new ApproveWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            Reason = "provider payout approved",
            Metadata = CreateMetadata()
        }, checker.Id);
        approveWithdrawalResponse.IsSuccessStatusCode.Should().BeTrue(await approveWithdrawalResponse.Content.ReadAsStringAsync());

        db.ChangeTracker.Clear();
        var updatedWithdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.Id == withdrawal.Id);
        var updatedWallet = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        var settlementEntries = await db.Set<WalletLedgerEntry>()
            .Where(x => x.OperationId == updatedWithdrawal.SettlementOperationId)
            .ToListAsync();

        updatedWithdrawal.WorkflowStatus.Should().Be(WalletWorkflowStatus.Completed);
        updatedWithdrawal.WithdrawalStatus.Should().Be(TransactionStatus.Completed);
        updatedWithdrawal.ProviderTransactionId.Should().Be(withdrawalReference);
        updatedWithdrawal.ProviderStatus.Should().Contain("Approved");
        updatedWithdrawal.RawResponseData.Should().Contain("COMPLETED");
        updatedWithdrawal.HoldOperationId.Should().NotBeNull();
        updatedWithdrawal.SettlementOperationId.Should().NotBeNull();
        updatedWallet.Balance.Should().Be(436m);
        settlementEntries.Where(x => x.Direction == WalletLedgerDirection.Debit).Sum(x => x.Amount)
            .Should().Be(settlementEntries.Where(x => x.Direction == WalletLedgerDirection.Credit).Sum(x => x.Amount));
    }

    [Test]
    public async Task Http_CreateDeposit_WithExplicitWalletAndDifferentCredential_ReturnsBadRequest()
    {
        var owner = await SeedCredential();
        var otherCredential = await SeedCredential();
        var wallet = await SeedWallet(owner.Id, 100m);

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", new CreateDepositWorkflowRequest
        {
            CredentialId = otherCredential.Id,
            WalletId = wallet.Id,
            Amount = 25m,
            ExternalReference = $"deposit-wrong-credential-{Guid.NewGuid():N}",
            Metadata = CreateMetadata()
        });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        body.Should().Contain("Wallet does not belong to the requested credential");
    }

    [Test]
    public async Task Http_CreateDeposit_WithExplicitWalletAndDifferentWalletType_ReturnsBadRequest()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);
        var wrongWalletTypeId = Guid.NewGuid();

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            WalletTypeId = wrongWalletTypeId,
            Amount = 25m,
            ExternalReference = $"deposit-wrong-type-{Guid.NewGuid():N}",
            Metadata = CreateMetadata()
        });
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        body.Should().Contain("Wallet does not match requested wallet type");
    }

    [Test]
    public async Task Http_AddFunds_DuplicateWebhookIdempotencyKey_CreatesOneOperationTransactionAndOutbox()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);
        var idempotencyKey = $"webhook-{Guid.NewGuid():N}";

        var request = new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 50m,
            IdempotencyKey = idempotencyKey,
            ReferenceNumber = $"provider-ref-{Guid.NewGuid():N}",
            Metadata = CreateMetadata()
        };

        var firstResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);
        var secondResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", request);

        firstResponse.IsSuccessStatusCode.Should().BeTrue();
        secondResponse.IsSuccessStatusCode.Should().BeTrue();

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        var operation = await db.Set<WalletOperation>().SingleAsync(x => x.IdempotencyKey == idempotencyKey);
        var transactionCount = await db.Set<WalletTransaction>()
            .CountAsync(x => x.WalletId == wallet.Id && x.ReferenceNumber == request.ReferenceNumber);
        var outboxCount = await db.Set<WalletOutboxMessage>().CountAsync(x => x.OperationId == operation.Id);

        updated.Balance.Should().Be(150m);
        operation.Status.Should().Be(WalletOperationStatus.Completed);
        transactionCount.Should().Be(1);
        outboxCount.Should().Be(1);
    }

    [Test]
    public async Task Http_WithdrawFunds_WithFee_DebitsPrincipalAndFeeAndKeepsLedgerBalanced()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 500m);
        var referenceNumber = $"withdraw-fee-{Guid.NewGuid():N}";

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/withdraw-funds", new DecrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 100m,
            Fee = 7m,
            ReferenceNumber = referenceNumber,
            Metadata = CreateMetadata()
        });
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue($"Response: {body}");

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        var operation = await db.Set<WalletOperation>().SingleAsync(x => x.ReferenceNumber == referenceNumber);
        var entries = await db.Set<WalletLedgerEntry>()
            .Where(x => x.OperationId == operation.Id)
            .ToListAsync();

        updated.Balance.Should().Be(393m);
        entries.Where(x => x.Direction == WalletLedgerDirection.Debit).Sum(x => x.Amount)
            .Should().Be(entries.Where(x => x.Direction == WalletLedgerDirection.Credit).Sum(x => x.Amount));
        entries.Should().Contain(x =>
            x.WalletId == wallet.Id &&
            x.Direction == WalletLedgerDirection.Debit &&
            x.BalanceBucket == WalletBalanceBucket.Available &&
            x.Amount == 107m);
        entries.Should().Contain(x =>
            x.EntryKind == WalletLedgerEntryKind.Fee &&
            x.Direction == WalletLedgerDirection.Credit &&
            x.Amount == 7m);
    }

    [Test]
    public async Task Ledger_PolicyRejectsFrozenWallet_PersistsRejectedOperationWithoutOutbox()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m, WalletStatus.Frozen);
        var referenceNumber = $"policy-{Guid.NewGuid():N}";

        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        EstablishTrustedActor(scope, credential.Id);
        var ledger = scope.ServiceProvider.GetRequiredService<IWalletLedgerService>();

        var result = await ledger.ExecuteAsync(new WalletLedgerExecutionRequest
        {
            TenantId = WalletsTestFixture.TestTenantId,
            OperationType = WalletOperationType.Debit,
            ActorCredentialId = credential.Id,
            IdempotencyKey = referenceNumber,
            ReferenceNumber = referenceNumber,
            Postings =
            [
                new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = WalletLedgerEntryKind.Principal,
                    Amount = 10m,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber
                },
                new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = 10m,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber
                }
            ]
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403, result.Message);
        result.Message.Should().Contain("frozen");

        await using var db = CreateDbContext();
        var operation = await db.Set<WalletOperation>().SingleAsync(x => x.ReferenceNumber == referenceNumber);
        var walletOutboxExists = await db.Set<WalletOutboxMessage>().AnyAsync(x => x.AggregateId == wallet.Id);

        operation.Status.Should().Be(WalletOperationStatus.Rejected);
        operation.FailureMessage.Should().Contain("frozen");
        walletOutboxExists.Should().BeFalse();
    }

    [Test]
    public async Task Ledger_PolicyApprovalThreshold_BlocksImmediateSettlementWithoutApproval()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 500m);
        var referenceNumber = $"approval-threshold-{Guid.NewGuid():N}";
        var policyRuleId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            db.Set<WalletPolicyRule>().Add(new WalletPolicyRule
            {
                Id = policyRuleId,
                TenantId = WalletsTestFixture.TestTenantId,
                Name = $"Approval threshold {Guid.NewGuid():N}",
                OperationType = WalletOperationType.Debit,
                WalletTypeId = wallet.WalletTypeId,
                ApprovalThreshold = 100m,
                EffectiveAt = DateTime.UtcNow.AddMinutes(-1),
                IsEnabled = true
            });
            await db.SaveChangesAsync();
        }

        try
        {
            await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
            EstablishTrustedActor(scope, credential.Id);
            var ledger = scope.ServiceProvider.GetRequiredService<IWalletLedgerService>();
            var result = await ledger.ExecuteAsync(new WalletLedgerExecutionRequest
            {
                TenantId = WalletsTestFixture.TestTenantId,
                OperationType = WalletOperationType.Debit,
                ActorCredentialId = credential.Id,
                IdempotencyKey = referenceNumber,
                ReferenceNumber = referenceNumber,
                Postings =
                [
                    new WalletLedgerPostingRequest
                    {
                        WalletId = wallet.Id,
                        Direction = WalletLedgerDirection.Debit,
                        BalanceBucket = WalletBalanceBucket.Available,
                        EntryKind = WalletLedgerEntryKind.Principal,
                        Amount = 125m,
                        WalletTypeId = wallet.WalletTypeId,
                        ReferenceNumber = referenceNumber
                    },
                    new WalletLedgerPostingRequest
                    {
                        Direction = WalletLedgerDirection.Credit,
                        BalanceBucket = WalletBalanceBucket.External,
                        EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                        Amount = 125m,
                        WalletTypeId = wallet.WalletTypeId,
                        ReferenceNumber = referenceNumber
                    }
                ]
            });

            result.IsSuccess.Should().BeFalse();
            result.StatusCode.Should().Be(409);
            result.Message.Should().Contain("maker-checker");

            await using var verifyDb = CreateDbContext();
            var updatedWallet = await verifyDb.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
            var operation = await verifyDb.Set<WalletOperation>().SingleAsync(x => x.ReferenceNumber == referenceNumber);

            updatedWallet.Balance.Should().Be(500m);
            operation.Status.Should().Be(WalletOperationStatus.Rejected);
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();
            var policyRule = await cleanupDb.Set<WalletPolicyRule>().FindAsync([policyRuleId]);
            if (policyRule is not null)
            {
                cleanupDb.Set<WalletPolicyRule>().Remove(policyRule);
                await cleanupDb.SaveChangesAsync();
            }
        }
    }

    [Test]
    public async Task Ledger_PolicyVelocityLimit_UsesCumulativeRequestAmount()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 500m);
        var referenceNumber = $"velocity-cumulative-{Guid.NewGuid():N}";
        var policyRuleId = Guid.NewGuid();

        await using (var db = CreateDbContext())
        {
            db.Set<WalletPolicyRule>().Add(new WalletPolicyRule
            {
                Id = policyRuleId,
                TenantId = WalletsTestFixture.TestTenantId,
                Name = $"Daily velocity {Guid.NewGuid():N}",
                OperationType = WalletOperationType.Debit,
                WalletTypeId = wallet.WalletTypeId,
                DailyVelocityLimit = 100m,
                EffectiveAt = DateTime.UtcNow.AddMinutes(-1),
                IsEnabled = true
            });
            await db.SaveChangesAsync();
        }

        try
        {
            await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
            EstablishTrustedActor(scope, credential.Id);
            var ledger = scope.ServiceProvider.GetRequiredService<IWalletLedgerService>();

            var result = await ledger.ExecuteAsync(new WalletLedgerExecutionRequest
            {
                TenantId = WalletsTestFixture.TestTenantId,
                OperationType = WalletOperationType.Debit,
                ActorCredentialId = credential.Id,
                IdempotencyKey = referenceNumber,
                ReferenceNumber = referenceNumber,
                Postings =
                [
                    new WalletLedgerPostingRequest
                    {
                        WalletId = wallet.Id,
                        Direction = WalletLedgerDirection.Debit,
                        BalanceBucket = WalletBalanceBucket.Available,
                        EntryKind = WalletLedgerEntryKind.Principal,
                        Amount = 60m,
                        WalletTypeId = wallet.WalletTypeId,
                        ReferenceNumber = referenceNumber
                    },
                    new WalletLedgerPostingRequest
                    {
                        WalletId = wallet.Id,
                        Direction = WalletLedgerDirection.Debit,
                        BalanceBucket = WalletBalanceBucket.Available,
                        EntryKind = WalletLedgerEntryKind.Principal,
                        Amount = 60m,
                        WalletTypeId = wallet.WalletTypeId,
                        ReferenceNumber = referenceNumber
                    },
                    new WalletLedgerPostingRequest
                    {
                        Direction = WalletLedgerDirection.Credit,
                        BalanceBucket = WalletBalanceBucket.External,
                        EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                        Amount = 120m,
                        WalletTypeId = wallet.WalletTypeId,
                        ReferenceNumber = referenceNumber
                    }
                ]
            });

            result.IsSuccess.Should().BeFalse();
            result.StatusCode.Should().Be(400);
            result.Message.Should().Contain("Daily wallet velocity limit exceeded");

            await using var verifyDb = CreateDbContext();
            var updatedWallet = await verifyDb.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
            var operation = await verifyDb.Set<WalletOperation>().SingleAsync(x => x.ReferenceNumber == referenceNumber);

            updatedWallet.Balance.Should().Be(500m);
            operation.Status.Should().Be(WalletOperationStatus.Rejected);
        }
        finally
        {
            await using var cleanupDb = CreateDbContext();
            var policyRule = await cleanupDb.Set<WalletPolicyRule>().FindAsync([policyRuleId]);
            if (policyRule is not null)
            {
                cleanupDb.Set<WalletPolicyRule>().Remove(policyRule);
                await cleanupDb.SaveChangesAsync();
            }
        }
    }

    [Test]
    public async Task Ledger_RefundDisputeOperation_CreditsWalletAndCreatesOutbox()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);
        var referenceNumber = $"refund-{Guid.NewGuid():N}";

        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        EstablishTrustedActor(scope, credential.Id);
        var ledger = scope.ServiceProvider.GetRequiredService<IWalletLedgerService>();

        var result = await ledger.ExecuteAsync(new WalletLedgerExecutionRequest
        {
            TenantId = WalletsTestFixture.TestTenantId,
            OperationType = WalletOperationType.Refund,
            ActorCredentialId = credential.Id,
            IdempotencyKey = referenceNumber,
            ReferenceNumber = referenceNumber,
            Reason = "dispute refund",
            Postings =
            [
                new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = 25m,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber
                },
                new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = WalletLedgerEntryKind.Reversal,
                    Amount = 25m,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber
                }
            ]
        });

        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        var operation = await db.Set<WalletOperation>().SingleAsync(x => x.ReferenceNumber == referenceNumber);
        var outbox = await db.Set<WalletOutboxMessage>().SingleAsync(x => x.OperationId == operation.Id);

        updated.Balance.Should().Be(125m);
        operation.OperationType.Should().Be(WalletOperationType.Refund);
        operation.Reason.Should().Be("dispute refund");
        outbox.Status.Should().Be(WalletOutboxStatus.Pending);
    }

    [Test]
    public async Task Webhook_SignedUnauthenticatedRequest_SettlesDepositOnceAndIgnoresDuplicate()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        var externalReference = $"signed-webhook-{Guid.NewGuid():N}";

        var depositResponse = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            GatewayId = gatewayId,
            Amount = 90m,
            ExternalReference = externalReference,
            Metadata = CreateMetadata()
        });
        depositResponse.IsSuccessStatusCode.Should().BeTrue(await depositResponse.Content.ReadAsStringAsync());

        await using var db = CreateDbContext();
        var deposit = await db.Set<DepositRequest>().SingleAsync(x => x.ExternalReference == externalReference);
        var approveResponse = await PostAsJsonAsActorAsync("/api/wallets/deposits/approve", new ApproveDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            Metadata = CreateMetadata()
        }, checker.Id);
        approveResponse.IsSuccessStatusCode.Should().BeTrue(await approveResponse.Content.ReadAsStringAsync());

        var payload = $$"""{"tenantId":"{{WalletsTestFixture.TestTenantId}}","event":"deposit.completed","reference":"{{externalReference}}","amount":90}""";
        var request = new IngestWalletPaymentWebhookRequest
        {
            ProviderKey = "test-provider",
            ExternalEventId = $"evt-{Guid.NewGuid():N}",
            ExternalReference = externalReference,
            ProviderTransactionId = $"ptx-{Guid.NewGuid():N}",
            ProviderStatus = "completed",
            Amount = 90m,
            RawPayloadJson = payload,
            Signature = SignWebhookPayload(payload),
            Metadata = CreateMetadata()
        };

        var firstResponse = await HttpClient.PostAsJsonAsync("/api/wallets/payment-webhooks", request);
        var secondResponse = await HttpClient.PostAsJsonAsync("/api/wallets/payment-webhooks", request);

        firstResponse.IsSuccessStatusCode.Should().BeTrue(await firstResponse.Content.ReadAsStringAsync());
        secondResponse.IsSuccessStatusCode.Should().BeTrue(await secondResponse.Content.ReadAsStringAsync());
        var first = await firstResponse.Content.ReadFromJsonAsync<WalletWebhookIngestResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<WalletWebhookIngestResponse>();
        first.Should().NotBeNull();
        first!.Duplicate.Should().BeFalse();
        second.Should().NotBeNull();
        second!.Duplicate.Should().BeTrue();

        db.ChangeTracker.Clear();
        var settledDeposit = await db.Set<DepositRequest>().SingleAsync(x => x.Id == deposit.Id);
        var webhookRows = await db.Set<WalletPaymentWebhookEvent>()
            .Where(x => x.ExternalEventId == request.ExternalEventId)
            .ToListAsync();
        var operationCount = await db.Set<WalletOperation>()
            .CountAsync(x => x.IdempotencyKey == $"webhook:{request.ProviderKey}:{request.ExternalEventId}");

        settledDeposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.Completed);
        settledDeposit.SettlementOperationId.Should().NotBeNull();
        webhookRows.Should().ContainSingle();
        webhookRows[0].ProcessingStatus.Should().Be(WalletWebhookProcessingStatus.Processed);
        operationCount.Should().Be(1);

        var processedOperationId = webhookRows[0].OperationId;
        var processedPayload = webhookRows[0].RawPayloadJson;
        var invalidDuplicate = request with
        {
            RawPayloadJson = $$"""{"tenantId":"{{WalletsTestFixture.TestTenantId}}","event":"deposit.tampered","reference":"{{externalReference}}","amount":91}""",
            Signature = "not-a-valid-signature"
        };

        var invalidDuplicateResponse = await HttpClient.PostAsJsonAsync(
            "/api/wallets/payment-webhooks",
            invalidDuplicate);

        invalidDuplicateResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        db.ChangeTracker.Clear();
        var webhookAfterInvalidDuplicate = await db.Set<WalletPaymentWebhookEvent>()
            .SingleAsync(x => x.ExternalEventId == request.ExternalEventId);
        webhookAfterInvalidDuplicate.ProcessingStatus.Should().Be(WalletWebhookProcessingStatus.Processed);
        webhookAfterInvalidDuplicate.SignatureValid.Should().BeTrue();
        webhookAfterInvalidDuplicate.OperationId.Should().Be(processedOperationId);
        webhookAfterInvalidDuplicate.RawPayloadJson.Should().Be(processedPayload);
    }

    [Test]
    public async Task Webhook_RejectedProviderStatus_MovesDepositToRejectedWithoutHumanApprover()
    {
        var credential = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        var externalReference = $"signed-webhook-rejected-{Guid.NewGuid():N}";

        var depositResponse = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            GatewayId = gatewayId,
            Amount = 45m,
            ExternalReference = externalReference,
            Metadata = CreateMetadata()
        });
        depositResponse.IsSuccessStatusCode.Should().BeTrue(await depositResponse.Content.ReadAsStringAsync());

        await using var db = CreateDbContext();
        var deposit = await db.Set<DepositRequest>().SingleAsync(x => x.ExternalReference == externalReference);
        var payload = $$"""{"tenantId":"{{WalletsTestFixture.TestTenantId}}","event":"deposit.rejected","reference":"{{externalReference}}","amount":45}""";
        var request = new IngestWalletPaymentWebhookRequest
        {
            ProviderKey = "test-provider",
            ExternalEventId = $"evt-{Guid.NewGuid():N}",
            ExternalReference = externalReference,
            ProviderTransactionId = $"ptx-{Guid.NewGuid():N}",
            ProviderStatus = "rejected",
            Amount = 45m,
            RawPayloadJson = payload,
            Signature = SignWebhookPayload(payload),
            Metadata = CreateMetadata()
        };

        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        var webhookService = scope.ServiceProvider.GetRequiredService<IWalletPaymentWebhookService>();

        var result = await webhookService.IngestAsync(request);

        result.IsSuccess.Should().BeTrue(result.Message);
        db.ChangeTracker.Clear();
        var rejectedDeposit = await db.Set<DepositRequest>().SingleAsync(x => x.Id == deposit.Id);
        var approval = await db.Set<WalletApprovalRequest>().SingleAsync(x => x.Id == deposit.ApprovalId);
        var webhook = await db.Set<WalletPaymentWebhookEvent>().SingleAsync(x => x.ExternalEventId == request.ExternalEventId);
        rejectedDeposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.Rejected);
        approval.Status.Should().Be(WalletApprovalStatus.Rejected);
        approval.ApproverCredentialId.Should().BeNull();
        webhook.ProcessingStatus.Should().Be(WalletWebhookProcessingStatus.Processed);
    }

    [Test]
    public async Task Webhook_FailedDuplicateEvent_CanRetryAfterWorkflowBecomesValid()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        var externalReference = $"signed-webhook-retry-{Guid.NewGuid():N}";

        var depositResponse = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            GatewayId = gatewayId,
            Amount = 70m,
            ExternalReference = externalReference,
            Metadata = CreateMetadata()
        });
        depositResponse.IsSuccessStatusCode.Should().BeTrue(await depositResponse.Content.ReadAsStringAsync());

        await using var db = CreateDbContext();
        var deposit = await db.Set<DepositRequest>().SingleAsync(x => x.ExternalReference == externalReference);
        var payload = $$"""{"tenantId":"{{WalletsTestFixture.TestTenantId}}","event":"deposit.completed","reference":"{{externalReference}}","amount":70}""";
        var request = new IngestWalletPaymentWebhookRequest
        {
            ProviderKey = "test-provider",
            ExternalEventId = $"evt-{Guid.NewGuid():N}",
            ExternalReference = externalReference,
            ProviderTransactionId = $"ptx-{Guid.NewGuid():N}",
            ProviderStatus = "completed",
            Amount = 70m,
            RawPayloadJson = payload,
            Signature = SignWebhookPayload(payload),
            Metadata = CreateMetadata()
        };

        bool firstIsSuccess;
        int firstStatusCode;
        string? firstMessage;
        await using (var firstScope = WalletsTestFixture.Services.CreateAsyncScope())
        {
            var accessor = firstScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext();
            var webhookService = firstScope.ServiceProvider.GetRequiredService<IWalletPaymentWebhookService>();
            var first = await webhookService.IngestAsync(request);
            firstIsSuccess = first.IsSuccess;
            firstStatusCode = first.StatusCode;
            firstMessage = first.Message;
        }

        firstIsSuccess.Should().BeFalse();
        firstStatusCode.Should().Be(400);
        firstMessage.Should().Contain("approved");

        db.ChangeTracker.Clear();
        var failedWebhook = await db.Set<WalletPaymentWebhookEvent>().SingleAsync(x => x.ExternalEventId == request.ExternalEventId);
        failedWebhook.ProcessingStatus.Should().Be(WalletWebhookProcessingStatus.Failed);

        var approveResponse = await PostAsJsonAsActorAsync("/api/wallets/deposits/approve", new ApproveDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            Metadata = CreateMetadata()
        }, checker.Id);
        approveResponse.IsSuccessStatusCode.Should().BeTrue(await approveResponse.Content.ReadAsStringAsync());

        bool secondIsSuccess;
        string? secondMessage;
        bool secondDuplicate;
        Guid secondWebhookEventId;
        await using (var retryScope = WalletsTestFixture.Services.CreateAsyncScope())
        {
            var accessor = retryScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext();
            var webhookService = retryScope.ServiceProvider.GetRequiredService<IWalletPaymentWebhookService>();
            var second = await webhookService.IngestAsync(request);
            secondIsSuccess = second.IsSuccess;
            secondMessage = second.Message;
            secondDuplicate = second.Data?.Duplicate ?? true;
            secondWebhookEventId = second.Data?.WebhookEventId ?? Guid.Empty;
        }

        secondIsSuccess.Should().BeTrue(secondMessage);
        secondDuplicate.Should().BeFalse();
        secondWebhookEventId.Should().Be(failedWebhook.Id);

        db.ChangeTracker.Clear();
        var settledDeposit = await db.Set<DepositRequest>().SingleAsync(x => x.Id == deposit.Id);
        var webhookRows = await db.Set<WalletPaymentWebhookEvent>()
            .Where(x => x.ExternalEventId == request.ExternalEventId)
            .ToListAsync();
        var operationCount = await db.Set<WalletOperation>()
            .CountAsync(x => x.IdempotencyKey == $"webhook:{request.ProviderKey}:{request.ExternalEventId}");

        settledDeposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.Completed);
        webhookRows.Should().ContainSingle();
        webhookRows[0].ProcessingStatus.Should().Be(WalletWebhookProcessingStatus.Processed);
        operationCount.Should().Be(1);
    }

    [Test]
    public async Task Webhook_InvalidSignatureCannotChooseAuditTenant()
    {
        var payload = $$"""{"tenantId":"{{WalletsTestFixture.TestTenantId}}","event":"deposit.completed","reference":"bad-signature","amount":10}""";
        var request = new IngestWalletPaymentWebhookRequest
        {
            ProviderKey = "unconfigured-provider",
            ExternalEventId = $"evt-invalid-{Guid.NewGuid():N}",
            ExternalReference = $"bad-signature-{Guid.NewGuid():N}",
            ProviderStatus = "completed",
            Amount = 10m,
            RawPayloadJson = payload,
            Signature = "not-a-valid-signature",
            Metadata = CreateMetadata()
        };

        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var webhookService = scope.ServiceProvider.GetRequiredService<IWalletPaymentWebhookService>();

        var result = await webhookService.IngestAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(401);

        await using var db = CreateDbContext();
        var webhookExists = await db.Set<WalletPaymentWebhookEvent>()
            .AnyAsync(x => x.ExternalEventId == request.ExternalEventId);
        webhookExists.Should().BeFalse();
    }

    [Test]
    public async Task CreateDeposit_IdempotencyKey_ReplaysAndRejectsChangedRequest()
    {
        var credential = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        var key = $"deposit-create-{Guid.NewGuid():N}";
        var reference = $"deposit-reference-{Guid.NewGuid():N}";
        var request = new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            GatewayId = gatewayId,
            Amount = 25m,
            ExternalReference = reference,
            IdempotencyKey = key,
            Metadata = CreateMetadata()
        };

        var first = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", request);
        var replay = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", request);
        var changed = await HttpClient.PostAsJsonAsync("/api/wallets/deposits", request with { Amount = 26m });

        first.IsSuccessStatusCode.Should().BeTrue(await first.Content.ReadAsStringAsync());
        replay.IsSuccessStatusCode.Should().BeTrue(await replay.Content.ReadAsStringAsync());
        changed.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var db = CreateDbContext();
        var deposits = await db.Set<DepositRequest>().Where(x => x.IdempotencyKey == key).ToListAsync();
        deposits.Should().ContainSingle();
        var approvalCount = await db.Set<WalletApprovalRequest>()
            .CountAsync(x => x.Id == deposits[0].ApprovalId);
        approvalCount.Should().Be(1);
    }

    [Test]
    public async Task CreateWithdrawal_IdempotencyKey_ReplaysAndRejectsChangedRequest()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);
        var gatewayId = await SeedPaymentGateway();
        var key = $"withdrawal-create-{Guid.NewGuid():N}";
        var reference = $"withdrawal-reference-{Guid.NewGuid():N}";
        var request = new CreateWithdrawalWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            GatewayId = gatewayId,
            Amount = 25m,
            ExternalReference = reference,
            IdempotencyKey = key,
            Metadata = CreateMetadata()
        };

        var first = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals", request);
        var replay = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals", request);
        var changed = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals", request with { Amount = 26m });

        first.IsSuccessStatusCode.Should().BeTrue(await first.Content.ReadAsStringAsync());
        replay.IsSuccessStatusCode.Should().BeTrue(await replay.Content.ReadAsStringAsync());
        changed.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var db = CreateDbContext();
        var withdrawals = await db.Set<WithdrawalRequest>().Where(x => x.IdempotencyKey == key).ToListAsync();
        withdrawals.Should().ContainSingle();
        var approvalCount = await db.Set<WalletApprovalRequest>()
            .CountAsync(x => x.Id == withdrawals[0].ApprovalId);
        approvalCount.Should().Be(1);
    }

    [Test]
    public async Task WalletEvents_GetRecentEvents_IsTenantScoped()
    {
        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IWalletEventPublisher>();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await publisher.PublishAsync(new WalletEvent { TenantId = tenantA, EventType = "tenant-a" });
        await publisher.PublishAsync(new WalletEvent { TenantId = tenantB, EventType = "tenant-b" });

        publisher.GetRecentEvents(tenantA).Should().OnlyContain(x => x.TenantId == tenantA);
        publisher.GetRecentEvents(tenantB).Should().OnlyContain(x => x.TenantId == tenantB);
    }

    [Test]
    public async Task DirectWorkflowCall_UnsignedMetadataWithoutHttpContext_IsRejected()
    {
        var credential = await SeedCredential();

        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = null;
        var service = scope.ServiceProvider.GetRequiredService<IWalletWorkflowService>();

        var result = await service.CreateDepositAsync(new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            Amount = 25m,
            Metadata = CreateMetadata()
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("Tenant context");
    }

    [Test]
    public async Task WorkflowAction_WrongActorWithoutAdminRole_IsForbidden()
    {
        var credential = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        await using var db = CreateDbContext();
        var approval = new WalletApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            OperationType = WalletOperationType.DepositApproval,
            Status = WalletApprovalStatus.Pending,
            RequesterCredentialId = credential.Id,
            Amount = 25m,
            RequestedAt = DateTime.UtcNow
        };
        var deposit = new DepositRequest
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            Amount = 25m,
            GatewayId = gatewayId,
            RequestedFee = 0m,
            CalculatedFee = 0m,
            ConvenienceFee = 0m,
            ReferenceNo = $"wrong-actor-{Guid.NewGuid():N}",
            ExternalReference = $"wrong-actor-{Guid.NewGuid():N}",
            DepositStatus = (short)DepositStatus.PendingPayment,
            WorkflowStatus = WalletWorkflowStatus.PendingApproval,
            RequestedByCredentialId = credential.Id,
            ApprovalId = approval.Id,
            RawRequestData = "{}"
        };
        db.Set<WalletApprovalRequest>().Add(approval);
        db.Set<DepositRequest>().Add(deposit);
        await db.SaveChangesAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/wallets/deposits/approve")
        {
            Content = JsonContent.Create(new ApproveDepositWorkflowRequest
            {
                RequestId = deposit.Id,
                Metadata = CreateMetadata()
            })
        };
        request.Headers.TryAddWithoutValidation("X-Wallets-Test-CredentialId", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("X-Wallets-Test-No-Role", "true");

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task WorkflowAction_CrossTenantApprovalReference_DoesNotMutateForeignApproval()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        var foreignTenantId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var foreignApproval = new WalletApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = foreignTenantId,
            OperationType = WalletOperationType.DepositApproval,
            Status = WalletApprovalStatus.Pending,
            RequesterCredentialId = credential.Id,
            Amount = 25m,
            RequestedAt = DateTime.UtcNow
        };
        var deposit = new DepositRequest
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            Amount = 25m,
            GatewayId = gatewayId,
            RequestedFee = 0m,
            CalculatedFee = 0m,
            ConvenienceFee = 0m,
            ReferenceNo = $"foreign-approval-{Guid.NewGuid():N}",
            ExternalReference = $"foreign-approval-{Guid.NewGuid():N}",
            DepositStatus = (short)DepositStatus.PendingPayment,
            WorkflowStatus = WalletWorkflowStatus.PendingApproval,
            RequestedByCredentialId = credential.Id,
            ApprovalId = foreignApproval.Id,
            RawRequestData = "{}"
        };
        await using (var foreignDb = CreateDbContext(foreignTenantId))
        {
            foreignDb.Set<WalletApprovalRequest>().Add(foreignApproval);
            await foreignDb.SaveChangesAsync();
        }
        db.Set<DepositRequest>().Add(deposit);
        await db.SaveChangesAsync();

        var response = await PostAsJsonAsActorAsync("/api/wallets/deposits/approve", new ApproveDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            Reason = "approve only local deposit",
            Metadata = CreateMetadata()
        }, checker.Id);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        db.ChangeTracker.Clear();
        var updatedDeposit = await db.Set<DepositRequest>().SingleAsync(x => x.Id == deposit.Id);
        var unchangedForeignApproval = await db.Set<WalletApprovalRequest>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == foreignApproval.Id);

        updatedDeposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.Approved);
        unchangedForeignApproval.TenantId.Should().Be(foreignTenantId);
        unchangedForeignApproval.Status.Should().Be(WalletApprovalStatus.Pending);
        unchangedForeignApproval.ApproverCredentialId.Should().BeNull();
        unchangedForeignApproval.DecidedAt.Should().BeNull();
    }

    [Test]
    public async Task WorkflowApproval_SameRequesterAndApprover_IsForbidden()
    {
        var credential = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        var externalReference = $"self-approval-{Guid.NewGuid():N}";

        var createResponse = await PostAsJsonAsActorAsync("/api/wallets/deposits", new CreateDepositWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            GatewayId = gatewayId,
            Amount = 40m,
            ExternalReference = externalReference,
            Metadata = CreateMetadata()
        }, credential.Id, privileged: false);
        createResponse.IsSuccessStatusCode.Should().BeTrue(await createResponse.Content.ReadAsStringAsync());

        await using var db = CreateDbContext();
        var deposit = await db.Set<DepositRequest>().SingleAsync(x => x.ExternalReference == externalReference);

        var approveResponse = await PostAsJsonAsActorAsync("/api/wallets/deposits/approve", new ApproveDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            Reason = "same actor tries to approve",
            Metadata = CreateMetadata()
        }, credential.Id, privileged: false, capabilities: [WalletAuthorizationCapabilities.Manage]);

        approveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        db.ChangeTracker.Clear();
        var unchangedDeposit = await db.Set<DepositRequest>().SingleAsync(x => x.Id == deposit.Id);
        var approval = await db.Set<WalletApprovalRequest>().SingleAsync(x => x.Id == deposit.ApprovalId);
        unchangedDeposit.WorkflowStatus.Should().Be(WalletWorkflowStatus.PendingApproval);
        approval.Status.Should().Be(WalletApprovalStatus.Pending);
        approval.ApproverCredentialId.Should().BeNull();
    }

    [Test]
    public async Task WithdrawalApproval_SameRequesterAndApprover_IsForbiddenAndDoesNotHoldFunds()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);
        var externalReference = $"self-withdrawal-{Guid.NewGuid():N}";

        var createResponse = await PostAsJsonAsActorAsync("/api/wallets/withdrawals", new CreateWithdrawalWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 25m,
            RequestedFee = 0m,
            ExternalReference = externalReference,
            Metadata = CreateMetadata()
        }, credential.Id, privileged: false);
        createResponse.IsSuccessStatusCode.Should().BeTrue(await createResponse.Content.ReadAsStringAsync());

        await using var db = CreateDbContext();
        var withdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.ExternalReference == externalReference);

        var approveResponse = await PostAsJsonAsActorAsync("/api/wallets/withdrawals/approve", new ApproveWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            Reason = "same actor tries to approve withdrawal",
            Metadata = CreateMetadata()
        }, credential.Id, privileged: false, capabilities: [WalletAuthorizationCapabilities.Manage]);

        approveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        db.ChangeTracker.Clear();
        var unchangedWithdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.Id == withdrawal.Id);
        var unchangedWallet = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        var approval = await db.Set<WalletApprovalRequest>().SingleAsync(x => x.Id == withdrawal.ApprovalId);
        var holdExists = await db.Set<WalletOperation>().AnyAsync(x => x.ReferenceNumber == withdrawal.ReferenceNumber);
        unchangedWithdrawal.WorkflowStatus.Should().Be(WalletWorkflowStatus.PendingApproval);
        unchangedWithdrawal.HoldOperationId.Should().BeNull();
        unchangedWallet.Balance.Should().Be(100m);
        unchangedWallet.DebitOnHoldBalance.Should().Be(0m);
        unchangedWallet.TransferableBalance.Should().Be(100m);
        approval.Status.Should().Be(WalletApprovalStatus.Pending);
        holdExists.Should().BeFalse();
    }

    [Test]
    public async Task WalletCase_WrongActorCannotCreateOrResolveCase()
    {
        var owner = await SeedCredential();
        var otherActor = await SeedCredential();
        var wallet = await SeedWallet(owner.Id, 100m);

        var wrongCreateResponse = await PostAsJsonAsActorAsync("/api/wallets/cases", new CreateWalletCaseRequest
        {
            WalletId = wallet.Id,
            CaseType = WalletCaseType.Refund,
            Amount = 10m,
            ExternalReference = $"wrong-case-{Guid.NewGuid():N}",
            Reason = "wrong actor",
            Metadata = CreateMetadata()
        }, otherActor.Id, privileged: false);

        wrongCreateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var caseReference = $"case-{Guid.NewGuid():N}";
        var createResponse = await PostAsJsonAsActorAsync("/api/wallets/cases", new CreateWalletCaseRequest
        {
            WalletId = wallet.Id,
            CaseType = WalletCaseType.Refund,
            Amount = 10m,
            ExternalReference = caseReference,
            Reason = "owner opened case",
            Metadata = CreateMetadata()
        }, owner.Id, privileged: false);
        createResponse.IsSuccessStatusCode.Should().BeTrue(await createResponse.Content.ReadAsStringAsync());

        await using var db = CreateDbContext();
        var walletCase = await db.Set<WalletCase>().SingleAsync(x => x.ExternalReference == caseReference);
        var wrongResolveResponse = await PostAsJsonAsActorAsync("/api/wallets/cases/resolve", new ResolveWalletCaseRequest
        {
            CaseId = walletCase.Id,
            Approve = true,
            Reason = "wrong actor tries to resolve",
            Metadata = CreateMetadata()
        }, otherActor.Id, privileged: false, capabilities: [WalletAuthorizationCapabilities.PolicyManage]);

        wrongResolveResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        db.ChangeTracker.Clear();
        var unchangedCase = await db.Set<WalletCase>().SingleAsync(x => x.Id == walletCase.Id);
        var operationExists = await db.Set<WalletOperation>().AnyAsync(x => x.ReferenceNumber == caseReference);
        unchangedCase.Status.Should().Be(WalletCaseStatus.Open);
        operationExists.Should().BeFalse();
    }

    [Test]
    public async Task WalletCase_ConcurrentRefunds_CannotExceedOriginalDebit()
    {
        var owner = await SeedCredential();
        var checker = await SeedCredential();
        var wallet = await SeedWallet(owner.Id, 200m);
        Guid originalOperationId;

        await using (var ledgerScope = WalletsTestFixture.Services.CreateAsyncScope())
        {
            EstablishTrustedActor(ledgerScope, owner.Id);
            var ledger = ledgerScope.ServiceProvider.GetRequiredService<IWalletLedgerService>();
            var originalResult = await ledger.ExecuteAsync(new WalletLedgerExecutionRequest
            {
                TenantId = WalletsTestFixture.TestTenantId,
                OperationType = WalletOperationType.Debit,
                ActorCredentialId = owner.Id,
                IdempotencyKey = $"refund-original:{Guid.NewGuid():N}",
                ReferenceNumber = $"refund-original-{Guid.NewGuid():N}",
                Postings =
                [
                    new WalletLedgerPostingRequest
                    {
                        Direction = WalletLedgerDirection.Credit,
                        BalanceBucket = WalletBalanceBucket.External,
                        EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                        Amount = 100m,
                        WalletTypeId = wallet.WalletTypeId
                    },
                    new WalletLedgerPostingRequest
                    {
                        WalletId = wallet.Id,
                        Direction = WalletLedgerDirection.Debit,
                        BalanceBucket = WalletBalanceBucket.Available,
                        EntryKind = WalletLedgerEntryKind.Principal,
                        Amount = 100m,
                        WalletTypeId = wallet.WalletTypeId
                    }
                ]
            });
            originalResult.IsSuccess.Should().BeTrue(originalResult.Message);
            originalOperationId = originalResult.Data!.OperationId;
        }

        var cases = new[]
        {
            new WalletCase
            {
                Id = Guid.NewGuid(),
                TenantId = WalletsTestFixture.TestTenantId,
                WalletId = wallet.Id,
                OriginalOperationId = originalOperationId,
                CaseType = WalletCaseType.Refund,
                Status = WalletCaseStatus.Open,
                Amount = 70m,
                ExternalReference = $"refund-case-{Guid.NewGuid():N}",
                Reason = "concurrent refund test",
                RequesterCredentialId = owner.Id
            },
            new WalletCase
            {
                Id = Guid.NewGuid(),
                TenantId = WalletsTestFixture.TestTenantId,
                WalletId = wallet.Id,
                OriginalOperationId = originalOperationId,
                CaseType = WalletCaseType.Refund,
                Status = WalletCaseStatus.Open,
                Amount = 70m,
                ExternalReference = $"refund-case-{Guid.NewGuid():N}",
                Reason = "concurrent refund test",
                RequesterCredentialId = owner.Id
            }
        };
        await using (var seedDb = CreateDbContext())
        {
            seedDb.Set<WalletCase>().AddRange(cases);
            await seedDb.SaveChangesAsync();
        }

        var responses = await Task.WhenAll(cases.Select(walletCase =>
            PostAsJsonAsActorAsync("/api/wallets/cases/resolve", new ResolveWalletCaseRequest
            {
                CaseId = walletCase.Id,
                Approve = true,
                Reason = "checker approved concurrent refund",
                IdempotencyKey = $"refund-resolution:{walletCase.Id:N}",
                Metadata = CreateMetadata()
            }, checker.Id)));

        responses.Count(static response => response.IsSuccessStatusCode).Should().Be(1);

        await using var verifyDb = CreateDbContext();
        var refundOperations = await verifyDb.Set<WalletOperation>()
            .Where(x =>
                x.OriginalOperationId == originalOperationId &&
                x.OperationType == WalletOperationType.Refund)
            .Select(x => new { x.Id, x.Status })
            .ToListAsync();
        var refundOperationIds = refundOperations.Select(static x => x.Id).ToArray();
        var completedRefundOperations = refundOperations
            .Where(static x => x.Status == WalletOperationStatus.Completed)
            .Select(static x => x.Id)
            .ToArray();
        var completedRefundAmount = await verifyDb.Set<WalletLedgerEntry>()
            .Where(x =>
                completedRefundOperations.Contains(x.OperationId) &&
                x.WalletId == wallet.Id &&
                x.Direction == WalletLedgerDirection.Credit &&
                x.BalanceBucket == WalletBalanceBucket.Available &&
                x.EntryKind == WalletLedgerEntryKind.Refund)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var refundEntryOperationIds = await verifyDb.Set<WalletLedgerEntry>()
            .Where(x => refundOperationIds.Contains(x.OperationId))
            .Select(x => x.OperationId)
            .Distinct()
            .ToListAsync();
        var refundOutboxOperationIds = await verifyDb.Set<WalletOutboxMessage>()
            .Where(x => x.OperationId.HasValue && refundOperationIds.Contains(x.OperationId.Value))
            .Select(x => x.OperationId!.Value)
            .Distinct()
            .ToListAsync();
        var caseIds = cases.Select(static item => item.Id).ToArray();
        var resolvedCases = await verifyDb.Set<WalletCase>()
            .CountAsync(x => caseIds.Contains(x.Id) && x.Status == WalletCaseStatus.Resolved);
        var completedOutboxCount = await verifyDb.Set<WalletOutboxMessage>()
            .CountAsync(x => x.OperationId.HasValue && completedRefundOperations.Contains(x.OperationId.Value));
        var updatedWallet = await verifyDb.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);

        completedRefundOperations.Should().ContainSingle();
        completedRefundAmount.Should().Be(70m);
        completedRefundAmount.Should().BeLessThanOrEqualTo(100m);
        refundEntryOperationIds.Should().BeEquivalentTo(completedRefundOperations);
        refundOutboxOperationIds.Should().BeEquivalentTo(completedRefundOperations);
        resolvedCases.Should().Be(1);
        completedOutboxCount.Should().Be(1);
        updatedWallet.Balance.Should().Be(170m);
    }

    [Test]
    public async Task WalletCase_ConcurrentApproveAndReject_LeavesOneConsistentDecision()
    {
        var owner = await SeedCredential();
        var approver = await SeedCredential();
        var rejecter = await SeedCredential();
        var wallet = await SeedWallet(owner.Id, 200m);
        var originalOperationId = await CreateOriginalDebitAsync(wallet, owner.Id, 100m);
        var walletCase = await SeedRefundCaseAsync(wallet, owner.Id, originalOperationId, 50m);

        await using var blockerDb = CreateDbContext();
        await using var blockerTransaction = await blockerDb.Database.BeginTransactionAsync();
        await blockerDb.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "ID"
            FROM "Wallet"."WalletCase"
            WHERE "ID" = {walletCase.Id}
            FOR UPDATE
            """);

        var approve = PostAsJsonAsActorAsync("/api/wallets/cases/resolve", new ResolveWalletCaseRequest
        {
            CaseId = walletCase.Id,
            Approve = true,
            Reason = "approve concurrent case",
            IdempotencyKey = $"refund-approve:{walletCase.Id:N}",
            Metadata = CreateMetadata()
        }, approver.Id);

        await WaitForWalletRowLockAsync(wallet.Id);
        await Task.Delay(200);

        var reject = PostAsJsonAsActorAsync("/api/wallets/cases/resolve", new ResolveWalletCaseRequest
        {
            CaseId = walletCase.Id,
            Approve = false,
            Reason = "reject concurrent case",
            Metadata = CreateMetadata()
        }, rejecter.Id);

        await Task.Delay(200);
        await blockerTransaction.CommitAsync();

        var responses = await Task.WhenAll(approve, reject);
        responses.Should().Contain(static response => response.IsSuccessStatusCode);

        await using var verifyDb = CreateDbContext();
        var decidedCase = await verifyDb.Set<WalletCase>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == walletCase.Id);
        var completedOperations = await verifyDb.Set<WalletOperation>()
            .AsNoTracking()
            .Where(x =>
                x.OriginalOperationId == originalOperationId &&
                x.OperationType == WalletOperationType.Refund &&
                x.Status == WalletOperationStatus.Completed)
            .ToListAsync();

        decidedCase.Status.Should().Be(WalletCaseStatus.Resolved);
        decidedCase.SettlementOperationId.Should().NotBeNull();
        completedOperations.Should().ContainSingle(x => x.Id == decidedCase.SettlementOperationId);
        (await verifyDb.Set<WalletLedgerEntry>()
            .CountAsync(x => x.OperationId == decidedCase.SettlementOperationId)).Should().Be(2);
        (await verifyDb.Set<WalletOutboxMessage>()
            .CountAsync(x => x.OperationId == decidedCase.SettlementOperationId)).Should().Be(1);
    }

    [Test]
    public async Task WalletCase_RefundLimitRejection_ReplaysOriginalStatusWithoutFinancialEffects()
    {
        var owner = await SeedCredential();
        var checker = await SeedCredential();
        var wallet = await SeedWallet(owner.Id, 200m);
        var originalOperationId = await CreateOriginalDebitAsync(wallet, owner.Id, 100m);
        var firstCase = await SeedRefundCaseAsync(wallet, owner.Id, originalOperationId, 80m);
        var excessiveCase = await SeedRefundCaseAsync(wallet, owner.Id, originalOperationId, 30m);

        var firstResponse = await PostAsJsonAsActorAsync("/api/wallets/cases/resolve", new ResolveWalletCaseRequest
        {
            CaseId = firstCase.Id,
            Approve = true,
            Reason = "approve first partial refund",
            IdempotencyKey = $"refund-resolution:{firstCase.Id:N}",
            Metadata = CreateMetadata()
        }, checker.Id);
        firstResponse.IsSuccessStatusCode.Should().BeTrue(await firstResponse.Content.ReadAsStringAsync());

        var idempotencyKey = $"refund-resolution:{excessiveCase.Id:N}";
        var request = new ResolveWalletCaseRequest
        {
            CaseId = excessiveCase.Id,
            Approve = true,
            Reason = "reject excessive cumulative refund",
            IdempotencyKey = idempotencyKey,
            Metadata = CreateMetadata()
        };
        var rejected = await PostAsJsonAsActorAsync("/api/wallets/cases/resolve", request, checker.Id);
        var replay = await PostAsJsonAsActorAsync("/api/wallets/cases/resolve", request, checker.Id);

        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        replay.StatusCode.Should().Be(rejected.StatusCode);

        await using var verifyDb = CreateDbContext();
        var rejectedOperation = await verifyDb.Set<WalletOperation>()
            .AsNoTracking()
            .SingleAsync(x => x.IdempotencyKey == idempotencyKey);
        rejectedOperation.Status.Should().Be(WalletOperationStatus.Rejected);
        rejectedOperation.FailureStatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        (await verifyDb.Set<WalletLedgerEntry>().AnyAsync(x => x.OperationId == rejectedOperation.Id)).Should().BeFalse();
        (await verifyDb.Set<WalletOutboxMessage>().AnyAsync(x => x.OperationId == rejectedOperation.Id)).Should().BeFalse();
    }

    [Test]
    public async Task OutboxDispatcher_ExpiredProcessingLease_IsReprocessed()
    {
        var outboxId = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            db.Set<WalletOutboxMessage>().Add(new WalletOutboxMessage
            {
                Id = outboxId,
                TenantId = WalletsTestFixture.TestTenantId,
                EventType = "WalletTest.ExpiredLease",
                AggregateType = "wallet",
                AggregateId = Guid.NewGuid(),
                PayloadJson = $$"""{"walletId":"{{Guid.NewGuid()}}","actorCredentialId":"{{Guid.NewGuid()}}"}""",
                Status = WalletOutboxStatus.Processing,
                LockedBy = "dead-worker",
                LockedUntil = DateTime.UtcNow.AddMinutes(-5),
                LastAttemptAt = DateTime.UtcNow.AddMinutes(-10),
                MaxAttempts = 5
            });
            await db.SaveChangesAsync();
        }

        await using (var discoveryScope = WalletsTestFixture.Services.CreateAsyncScope())
        {
            var authorization = await discoveryScope.ServiceProvider
                .GetRequiredService<ITrustedServiceTargetContextInitializer>()
                .EstablishTenantlessAsync(
                    XFrameworkServiceNames.Wallets,
                    [
                        XFrameworkServiceScopes.WalletsAdmin,
                        XFrameworkServiceScopes.DataContextQueryAllTenants
                    ],
                    XFrameworkServiceNames.Wallets);
            authorization.IsSuccess.Should().BeTrue(authorization.Error);
            authorization.Context!.Service!.Scopes.Should().BeEquivalentTo(
                XFrameworkServiceScopes.WalletsAdmin,
                XFrameworkServiceScopes.DataContextQueryAllTenants);

            var tenantIds = await discoveryScope.ServiceProvider
                .GetRequiredService<IWalletOutboxService>()
                .GetDueTenantIdsAsync();
            tenantIds.Should().Contain(WalletsTestFixture.TestTenantId);
        }

        await using (var dispatchScope = WalletsTestFixture.Services.CreateAsyncScope())
        {
            var authorization = await dispatchScope.ServiceProvider
                .GetRequiredService<ITrustedServiceTargetContextInitializer>()
                .EstablishAsync(
                    WalletsTestFixture.TestTenantId,
                    XFrameworkServiceNames.Wallets,
                    [XFrameworkServiceScopes.WalletsAdmin],
                    XFrameworkServiceNames.Wallets);
            authorization.IsSuccess.Should().BeTrue(authorization.Error);
            authorization.Context!.Service!.Scopes.Should().BeEquivalentTo(
                XFrameworkServiceScopes.WalletsAdmin,
                XFrameworkServiceScopes.TenantTarget);

            var outbox = dispatchScope.ServiceProvider.GetRequiredService<IWalletOutboxService>();
            await outbox.DispatchDueAsync();
        }

        await using var verifyDb = CreateDbContext();
        var message = await verifyDb.Set<WalletOutboxMessage>().SingleAsync(x => x.Id == outboxId);
        message.Status.Should().Be(WalletOutboxStatus.Published);
        message.PublishedAt.Should().NotBeNull();
        message.LockedBy.Should().BeNull();
        message.LockedUntil.Should().BeNull();
        message.LastError.Should().BeNull();
    }

    [Test]
    public async Task OutboxDispatcher_WithoutTrustedServiceTarget_IsRejected()
    {
        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IWalletOutboxService>();

        var act = () => outbox.DispatchDueAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*authorized service target tenant*");
    }

    [TestCase(XFrameworkServiceNames.Portal, true, true)]
    [TestCase(XFrameworkServiceNames.Wallets, false, true)]
    [TestCase(XFrameworkServiceNames.Wallets, true, false)]
    public async Task OutboxDispatcher_WithInvalidServiceAuthorization_IsRejected(
        string clientId,
        bool hasTenantTarget,
        bool hasWalletsAdmin)
    {
        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasTenantTarget)
            scopes.Add(XFrameworkServiceScopes.TenantTarget);
        if (hasWalletsAdmin)
            scopes.Add(XFrameworkServiceScopes.WalletsAdmin);
        scope.ServiceProvider.GetRequiredService<ITrustedInvocationContextStore>().Set(
            new TrustedInvocationContext(
                null,
                new TrustedServiceIdentity(
                    clientId,
                    XFrameworkServiceNames.Wallets,
                    scopes,
                    "wallets-integration-tests-g1"),
                WalletsTestFixture.TestTenantId,
                WalletsTestFixture.TestTenantId,
                Guid.NewGuid()));
        var outbox = scope.ServiceProvider.GetRequiredService<IWalletOutboxService>();

        var act = () => outbox.DispatchDueAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Wallets background-service identity*");
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task OutboxTenantDiscovery_WithMissingScope_IsRejected(
        bool hasWalletsAdmin,
        bool hasAllTenantQuery)
    {
        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasWalletsAdmin)
            scopes.Add(XFrameworkServiceScopes.WalletsAdmin);
        if (hasAllTenantQuery)
            scopes.Add(XFrameworkServiceScopes.DataContextQueryAllTenants);
        scope.ServiceProvider.GetRequiredService<ITrustedInvocationContextStore>().Set(
            new TrustedInvocationContext(
                null,
                new TrustedServiceIdentity(
                    XFrameworkServiceNames.Wallets,
                    XFrameworkServiceNames.Wallets,
                    scopes,
                    "wallets-integration-tests-g1"),
                null,
                null,
                Guid.NewGuid()));
        var outbox = scope.ServiceProvider.GetRequiredService<IWalletOutboxService>();

        var act = () => outbox.GetDueTenantIdsAsync();

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*tenant discovery*");
    }

    [Test]
    public async Task Reports_NonPrivilegedActorIsScopedToOwnWallets()
    {
        var owner = await SeedCredential();
        var otherActor = await SeedCredential();
        var ownerWallet = await SeedWallet(owner.Id, 100m);
        var otherWallet = await SeedWallet(otherActor.Id, 100m);
        var ownerReference = $"owner-report-{Guid.NewGuid():N}";
        var otherReference = $"other-report-{Guid.NewGuid():N}";

        await using (var scope = WalletsTestFixture.Services.CreateAsyncScope())
        {
            EstablishTrustedActor(scope, owner.Id);
            var ledger = scope.ServiceProvider.GetRequiredService<IWalletLedgerService>();
            (await ledger.ExecuteAsync(CreateReportCreditRequest(ownerWallet, owner.Id, ownerReference, 11m))).IsSuccess.Should().BeTrue();
            (await ledger.ExecuteAsync(CreateReportCreditRequest(otherWallet, otherActor.Id, otherReference, 13m))).IsSuccess.Should().BeTrue();
        }

        await using var reportScope = WalletsTestFixture.Services.CreateAsyncScope();
        var contextStore = reportScope.ServiceProvider.GetRequiredService<ITrustedInvocationContextStore>();
        contextStore.Set(new TrustedInvocationContext(
            Actor(WalletsTestFixture.TestTenantId, owner.Id),
            null,
            WalletsTestFixture.TestTenantId,
            WalletsTestFixture.TestTenantId,
            Guid.NewGuid()));
        var reporting = reportScope.ServiceProvider.GetRequiredService<IWalletReportingService>();

        var ledgerEntries = await reporting.GetLedgerEntriesAsync(new WalletLedgerEntriesRequest
        {
            PageSize = 50,
            Metadata = CreateMetadata()
        });
        ledgerEntries.IsSuccess.Should().BeTrue(ledgerEntries.Message);
        ledgerEntries.Data!.Select(x => x.ReferenceNumber).Should().Contain(ownerReference);
        ledgerEntries.Data!.Select(x => x.ReferenceNumber).Should().NotContain(otherReference);

        var history = await reporting.GetOperationHistoryAsync(new WalletOperationHistoryRequest
        {
            PageSize = 50,
            Metadata = CreateMetadata()
        });
        history.IsSuccess.Should().BeTrue(history.Message);
        history.Data!.Should().Contain(x => x.ReferenceNumber == ownerReference);
        history.Data!.Should().NotContain(x => x.ReferenceNumber == otherReference);

        var forbidden = await reporting.GetLedgerEntriesAsync(new WalletLedgerEntriesRequest
        {
            WalletId = otherWallet.Id,
            Metadata = CreateMetadata()
        });
        forbidden.StatusCode.Should().Be(403);

        var outboxFailures = await reporting.GetOutboxFailuresAsync(new WalletOutboxFailuresRequest
        {
            Metadata = CreateMetadata()
        });
        outboxFailures.StatusCode.Should().Be(403);
    }

    [Test]
    public async Task BoltMoneyOperation_DisabledWalletSubfeature_IsRejected()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);

        await SetTenantFeatureEnabled(TenantModuleFeatureKeys.WalletsDeposits, enabled: false);
        try
        {
            var result = await WalletsTestFixture.ServiceWrapper.IncrementWallet(new IncrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                WalletTypeId = wallet.WalletTypeId ?? WalletsTestFixture.TestWalletTypeId,
                Amount = 10m,
                ReferenceNumber = $"feature-gate-{Guid.NewGuid():N}",
                Metadata = CreateMetadata()
            });

            result.HttpStatusCode.Should().Be(HttpStatusCode.Forbidden);
            result.Message.Should().Contain("Feature disabled");
        }
        finally
        {
            await SetTenantFeatureEnabled(TenantModuleFeatureKeys.WalletsDeposits, enabled: true);
        }
    }

    [Test]
    public async Task DatabaseConstraints_RejectNegativeWalletBalance()
    {
        var credential = await SeedCredential();
        await using var db = CreateDbContext();
        db.Set<Wallet>().Add(new Wallet
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            AccountNumber = $"neg-{Guid.NewGuid():N}"[..20],
            Balance = -1m,
            TransferableBalance = 0m,
            DebitOnHoldBalance = 0m,
            CreditOnHoldBalance = 0m,
            IsEnabled = true
        });

        var act = () => db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task Ledger_OutboxRetryFailureState_IsPersistedForRetryContract()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);
        var referenceNumber = $"outbox-{Guid.NewGuid():N}";

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 10m,
            ReferenceNumber = referenceNumber,
            Metadata = CreateMetadata()
        });
        response.IsSuccessStatusCode.Should().BeTrue();

        Guid outboxId;
        var nextAttempt = DateTime.UtcNow.AddMinutes(5);
        await using (var db = CreateDbContext())
        {
            var operation = await db.Set<WalletOperation>().SingleAsync(x => x.ReferenceNumber == referenceNumber);
            var outbox = await db.Set<WalletOutboxMessage>().SingleAsync(x => x.OperationId == operation.Id);
            outbox.Status = WalletOutboxStatus.Failed;
            outbox.Attempts = 3;
            outbox.NextAttemptAt = nextAttempt;
            outbox.LastError = "provider unavailable";
            db.Update(outbox);
            await db.SaveChangesAsync();
            outboxId = outbox.Id;
        }

        await using (var db = CreateDbContext())
        {
            var failed = await db.Set<WalletOutboxMessage>().SingleAsync(x => x.Id == outboxId);

            failed.Status.Should().Be(WalletOutboxStatus.Failed);
            failed.Attempts.Should().Be(3);
            failed.NextAttemptAt.Should().NotBeNull();
            failed.LastError.Should().Be("provider unavailable");
        }
    }

    [Test]
    public async Task Ledger_ReconciliationSnapshotDriftFields_ResetAfterNextLedgerOperation()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);

        var firstResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 10m,
            ReferenceNumber = $"drift-seed-{Guid.NewGuid():N}",
            Metadata = CreateMetadata()
        });
        firstResponse.IsSuccessStatusCode.Should().BeTrue();

        await using (var db = CreateDbContext())
        {
            var snapshot = await db.Set<WalletBalanceSnapshot>().SingleAsync(x => x.WalletId == wallet.Id);
            snapshot.IsReconciled = false;
            snapshot.DriftAmount = 9m;
            snapshot.ReconciledAt = null;
            db.Update(snapshot);
            await db.SaveChangesAsync();
        }

        var repairResponse = await HttpClient.PostAsJsonAsync("/api/wallets/add-funds", new IncrementWalletRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 5m,
            ReferenceNumber = $"drift-repair-{Guid.NewGuid():N}",
            Metadata = CreateMetadata()
        });
        repairResponse.IsSuccessStatusCode.Should().BeTrue();

        await using (var db = CreateDbContext())
        {
            var updatedWallet = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
            var snapshot = await db.Set<WalletBalanceSnapshot>().SingleAsync(x => x.WalletId == wallet.Id);

            snapshot.Balance.Should().Be(updatedWallet.Balance);
            snapshot.AvailableBalance.Should().Be(updatedWallet.AvailableBalance);
            snapshot.IsReconciled.Should().BeTrue();
            snapshot.DriftAmount.Should().Be(0m);
            snapshot.ReconciledAt.Should().NotBeNull();
        }
    }

    [Test]
    public void Resolver_AuthenticatedRequestRejectsSpoofedMetadataTenant()
    {
        var resolver = new WalletRequestContextResolver(
            TrustedContext(Actor(WalletsTestFixture.TestTenantId, Guid.NewGuid())));
        var spoofedTenantId = Guid.NewGuid();

        var result = resolver.Resolve(new RequestBase
        {
            Metadata = new XFramework.Domain.Shared.BusinessObjects.RequestMetadata
            {
                RequestedTenantId = spoofedTenantId,
                RequestId = Guid.NewGuid()
            }
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Contain("trusted tenant");
    }

    [Test]
    public void Resolver_RequestMetadataDoesNotEstablishActorIdentity()
    {
        var targetCredentialId = Guid.NewGuid();
        var resolver = new WalletRequestContextResolver(
            TrustedContext(effectiveTenantId: WalletsTestFixture.TestTenantId));

        var contextRequest = new RequestBase
        {
            Metadata = new XFramework.Domain.Shared.BusinessObjects.RequestMetadata
            {
                RequestedTenantId = WalletsTestFixture.TestTenantId,
                RequestId = Guid.NewGuid(),
                IpAddress = "203.0.113.20",
                UserAgent = "DiagnosticAgent"
            }
        };

        var metadataOnlyResult = resolver.Resolve(contextRequest);

        metadataOnlyResult.IsSuccess.Should().BeTrue(metadataOnlyResult.Message);
        metadataOnlyResult.Data!.ActorCredentialId.Should().BeNull();
        metadataOnlyResult.Data.IpAddress.Should().Be("203.0.113.20");
        metadataOnlyResult.Data.UserAgent.Should().Be("DiagnosticAgent");
        metadataOnlyResult.Data.IsPrivilegedActor.Should().BeFalse();

        var targetOperationResult = resolver.Resolve(contextRequest, targetCredentialId);

        targetOperationResult.IsSuccess.Should().BeFalse();
        targetOperationResult.StatusCode.Should().Be(403);
        targetOperationResult.Message.Should().Contain("Actor credential");
    }

    [Test]
    public void Resolver_ActorPlusServiceCannotOperateOnAnotherCredential()
    {
        var actorCredentialId = Guid.NewGuid();
        var resolver = new WalletRequestContextResolver(
            TrustedContext(
                actor: Actor(WalletsTestFixture.TestTenantId, actorCredentialId),
                service: Service()));

        var result = resolver.Resolve(
            new RequestBase
            {
                Metadata = new XFramework.Domain.Shared.BusinessObjects.RequestMetadata
                {
                    RequestedTenantId = WalletsTestFixture.TestTenantId
                }
            },
            requestCredentialId: Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Contain("cannot operate");
    }

    [Test]
    public void Resolver_ServiceOnlyContext_RemainsSystemAuthority()
    {
        var resolver = new WalletRequestContextResolver(
            TrustedContext(
                effectiveTenantId: WalletsTestFixture.TestTenantId,
                service: Service()));

        var result = resolver.Resolve(
            new RequestBase { Metadata = new XFramework.Domain.Shared.BusinessObjects.RequestMetadata() },
            requestCredentialId: Guid.NewGuid());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.IsSystemActor.Should().BeTrue();
        result.Data.IsPrivilegedActor.Should().BeTrue();
    }

    [Test]
    public async Task Batch_ProcessTransactionsAsync_IsBlockedOutsideLedger()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m);
        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var batchService = scope.ServiceProvider.GetRequiredService<IBatchWalletService>();

        var result = await batchService.ProcessTransactionsAsync(
            [
                new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = WalletsTestFixture.TestTenantId,
                    CredentialId = credential.Id,
                    WalletId = wallet.Id,
                    Amount = 10m,
                    TransactionType = TransactionType.Credit,
                    ReferenceNumber = $"direct-{Guid.NewGuid():N}"
                }
            ],
            WalletsTestFixture.TestTenantId);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        result.Message.Should().Contain("Direct WalletTransaction batch writes are disabled");

        await using var db = CreateDbContext();
        var directTransactionExists = await db.Set<WalletTransaction>()
            .AnyAsync(x => x.ReferenceNumber != null && x.ReferenceNumber.StartsWith("direct-"));
        directTransactionExists.Should().BeFalse();
    }

    [Test]
    public async Task BatchIncrement_WrongActorWithoutAdminRole_IsForbidden()
    {
        var owner = await SeedCredential();
        var wallet = await SeedWallet(owner.Id, 100m);
        var wrongActorId = Guid.NewGuid();
        var referenceNumber = $"batch-wrong-actor-{Guid.NewGuid():N}";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/wallets/batch/increment")
        {
            Content = JsonContent.Create(new Wallets.Api.Features.Batch.IncrementBatch.BatchIncrementRequestWrapper
            {
                Metadata = CreateMetadata(),
                Requests =
                [
                    new BatchIncrementRequest
                    {
                        WalletId = wallet.Id,
                        WalletTypeId = WalletsTestFixture.TestWalletTypeId,
                        CredentialId = owner.Id,
                        Amount = 10m,
                        ReferenceNumber = referenceNumber
                    }
                ]
            })
        };
        request.Headers.TryAddWithoutValidation("X-Wallets-Test-CredentialId", wrongActorId.ToString());
        request.Headers.TryAddWithoutValidation("X-Wallets-Test-No-Role", "true");

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var db = CreateDbContext();
        var unchangedWallet = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        var transactionExists = await db.Set<WalletTransaction>().AnyAsync(x => x.ReferenceNumber == referenceNumber);

        unchangedWallet.Balance.Should().Be(100m);
        transactionExists.Should().BeFalse();
    }

    [Test]
    public async Task Http_FailApprovedWithdrawal_ReleasesLedgerHoldAndKeepsBalance()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 500m);
        var withdrawalReference = $"wd-fail-{Guid.NewGuid():N}";

        var createResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals", new CreateWithdrawalWorkflowRequest
        {
            CredentialId = credential.Id,
            WalletId = wallet.Id,
            Amount = 75m,
            RequestedFee = 5m,
            ExternalReference = withdrawalReference,
            Metadata = CreateMetadata()
        });
        createResponse.IsSuccessStatusCode.Should().BeTrue(await createResponse.Content.ReadAsStringAsync());

        await using var db = CreateDbContext();
        var withdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.ExternalReference == withdrawalReference);

        var approveResponse = await PostAsJsonAsActorAsync("/api/wallets/withdrawals/approve", new ApproveWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            Reason = "hold funds",
            Metadata = CreateMetadata()
        }, checker.Id);
        approveResponse.IsSuccessStatusCode.Should().BeTrue(await approveResponse.Content.ReadAsStringAsync());

        db.ChangeTracker.Clear();
        var heldWallet = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        heldWallet.Balance.Should().Be(500m);
        heldWallet.DebitOnHoldBalance.Should().Be(80m);
        heldWallet.TransferableBalance.Should().Be(420m);

        var failResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals/fail", new FailWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            Reason = "provider failed",
            Metadata = CreateMetadata()
        });
        failResponse.IsSuccessStatusCode.Should().BeTrue(await failResponse.Content.ReadAsStringAsync());

        db.ChangeTracker.Clear();
        var failedWithdrawal = await db.Set<WithdrawalRequest>().SingleAsync(x => x.Id == withdrawal.Id);
        var releasedWallet = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        var releaseOperation = await db.Set<WalletOperation>()
            .SingleAsync(x => x.IdempotencyKey == $"withdrawal:{WalletWorkflowStatus.Failed}:release:{withdrawal.Id}");

        failedWithdrawal.WorkflowStatus.Should().Be(WalletWorkflowStatus.Failed);
        failedWithdrawal.WithdrawalStatus.Should().Be(TransactionStatus.Failed);
        releasedWallet.Balance.Should().Be(500m);
        releasedWallet.DebitOnHoldBalance.Should().Be(0m);
        releasedWallet.TransferableBalance.Should().Be(500m);
        releaseOperation.OperationType.Should().Be(WalletOperationType.Release);
    }

    [Test]
    public async Task Database_UniqueOperationIdempotencyKey_PerTenantRejectsDuplicate()
    {
        var idempotencyKey = $"unique-{Guid.NewGuid():N}";

        await using var db = CreateDbContext();
        db.Set<WalletOperation>().Add(new WalletOperation
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            OperationType = WalletOperationType.Credit,
            Status = WalletOperationStatus.Completed,
            IdempotencyKey = idempotencyKey,
            ReferenceNumber = $"first-{Guid.NewGuid():N}"
        });
        db.Set<WalletOperation>().Add(new WalletOperation
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            OperationType = WalletOperationType.Credit,
            Status = WalletOperationStatus.Completed,
            IdempotencyKey = idempotencyKey,
            ReferenceNumber = $"second-{Guid.NewGuid():N}"
        });

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Test]
    public async Task Http_GetWallet_WithSpoofedTenantHeader_DoesNotCrossTenant()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 250m);
        var spoofedTenantId = Guid.NewGuid();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/wallets/{wallet.Id}");
        request.Headers.Add("X-Tenant-Id", spoofedTenantId.ToString());

        var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var db = CreateDbContext();
        var loaded = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        loaded.TenantId.Should().Be(WalletsTestFixture.TestTenantId);
    }

    [Test]
    public async Task Http_ConcurrentDebits_RejectsOverspendAndNeverMakesBalanceNegative()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 500m);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => HttpClient.PostAsJsonAsync("/api/wallets/withdraw-funds", new DecrementWalletRequest
            {
                CredentialId = credential.Id,
                WalletId = wallet.Id,
                Amount = 75m,
                IdempotencyKey = $"concurrent-debit-{Guid.NewGuid():N}",
                Metadata = CreateMetadata()
            }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        responses.Should().Contain(x => !x.IsSuccessStatusCode);

        await using var db = CreateDbContext();
        var updated = await db.Set<Wallet>().SingleAsync(x => x.Id == wallet.Id);
        var debits = await db.Set<WalletTransaction>()
            .Where(x => x.WalletId == wallet.Id && x.TransactionType == TransactionType.Debit)
            .Select(x => new { x.Amount, x.TransactionFee })
            .ToListAsync();
        var debitTotal = debits.Sum(x => Math.Abs(x.Amount) + Math.Abs(x.TransactionFee));

        updated.Balance.Should().BeGreaterThanOrEqualTo(0m);
        debitTotal.Should().BeLessThanOrEqualTo(500m);
        updated.Balance.Should().Be(500m - debitTotal);
    }

    [Test]
    public async Task Http_ConcurrentTransfers_RejectsOverspendAndNeverMakesSenderBalanceNegative()
    {
        var sender = await SeedCredential();
        var senderWallet = await SeedWallet(sender.Id, 500m);
        var recipients = new List<Guid>();

        for (var i = 0; i < 10; i++)
        {
            var recipient = await SeedCredential();
            await SeedWallet(recipient.Id, 0m);
            recipients.Add(recipient.Id);
        }

        var tasks = recipients.Select(recipientId =>
            HttpClient.PostAsJsonAsync("/api/wallets/transfer", new TransferWalletRequest
            {
                CredentialId = sender.Id,
                RecipientCredentialId = recipientId,
                WalletTypeId = WalletsTestFixture.TestWalletTypeId,
                Amount = 75m,
                TransferDeductionType = TransferDeductionType.DeductFromSender,
                IdempotencyKey = $"concurrent-transfer-{Guid.NewGuid():N}",
                Metadata = CreateMetadata()
            })).ToArray();

        var responses = await Task.WhenAll(tasks);
        responses.Should().Contain(x => !x.IsSuccessStatusCode);

        await using var db = CreateDbContext();
        var updatedSender = await db.Set<Wallet>().SingleAsync(x => x.Id == senderWallet.Id);
        var debits = await db.Set<WalletTransaction>()
            .Where(x => x.WalletId == senderWallet.Id && x.TransactionType == TransactionType.Debit)
            .Select(x => new { x.Amount, x.TransactionFee })
            .ToListAsync();
        var debitTotal = debits.Sum(x => Math.Abs(x.Amount) + Math.Abs(x.TransactionFee));

        updatedSender.Balance.Should().BeGreaterThanOrEqualTo(0m);
        debitTotal.Should().BeLessThanOrEqualTo(500m);
        updatedSender.Balance.Should().Be(500m - debitTotal);
    }

    private async Task<HttpResponseMessage> PostAsJsonAsActorAsync<TRequest>(
        string requestUri,
        TRequest request,
        Guid credentialId,
        bool privileged = true,
        IReadOnlyCollection<string>? capabilities = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("X-Wallets-Test-CredentialId", credentialId.ToString());
        if (!privileged)
        {
            message.Headers.TryAddWithoutValidation("X-Wallets-Test-No-Role", "true");
            message.Headers.TryAddWithoutValidation(
                "X-Wallets-Test-Capabilities",
                string.Join(',', capabilities ??
                [
                    WalletAuthorizationCapabilities.View,
                    WalletAuthorizationCapabilities.Update
                ]));
        }

        return await HttpClient.SendAsync(message);
    }

    private static DefaultHttpContext CreateActorHttpContext(Guid credentialId, bool privileged)
    {
        var claims = new List<Claim>
        {
            new("tenantId", WalletsTestFixture.TestTenantId.ToString()),
            new("credentialId", credentialId.ToString())
        };
        if (privileged)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "WalletsTest"))
        };
    }

    private static WalletLedgerExecutionRequest CreateReportCreditRequest(
        Wallet wallet,
        Guid actorCredentialId,
        string referenceNumber,
        decimal amount) =>
        new()
        {
            TenantId = WalletsTestFixture.TestTenantId,
            OperationType = WalletOperationType.DepositApproval,
            ActorCredentialId = actorCredentialId,
            IdempotencyKey = referenceNumber,
            ReferenceNumber = referenceNumber,
            Postings =
            [
                new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = amount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber
                },
                new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = WalletLedgerEntryKind.Principal,
                    Amount = amount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber
                }
            ]
        };

    private async Task<Guid> SeedPaymentGateway(Guid? gatewayId = null, string? gatewayName = null)
    {
        await using var db = CreateDbContext();
        if (gatewayId.HasValue && await db.Set<SharedContracts.PaymentGateway>().AnyAsync(x => x.Id == gatewayId.Value))
        {
            return gatewayId.Value;
        }

        var category = new SharedContracts.PaymentGatewayCategory
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            Name = $"GatewayCategory-{Guid.NewGuid():N}",
            Description = "Wallet integration test gateway category",
            IsEnabled = true
        };
        var gateway = new SharedContracts.PaymentGateway
        {
            Id = gatewayId ?? Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            GatewayCategoryId = category.Id,
            Name = gatewayName ?? $"Gateway-{Guid.NewGuid():N}",
            Description = "Wallet integration test gateway",
            ServiceCharge = 1m,
            ConvenienceFee = 2m,
            IsEnabled = true
        };

        db.Set<SharedContracts.PaymentGatewayCategory>().Add(category);
        db.Set<SharedContracts.PaymentGateway>().Add(gateway);
        await db.SaveChangesAsync();

        return gateway.Id;
    }

    private async Task<Guid> CreateOriginalDebitAsync(
        Wallet wallet,
        Guid actorCredentialId,
        decimal amount)
    {
        await using var ledgerScope = WalletsTestFixture.Services.CreateAsyncScope();
        EstablishTrustedActor(ledgerScope, actorCredentialId);
        var ledger = ledgerScope.ServiceProvider.GetRequiredService<IWalletLedgerService>();
        var result = await ledger.ExecuteAsync(new WalletLedgerExecutionRequest
        {
            TenantId = WalletsTestFixture.TestTenantId,
            OperationType = WalletOperationType.Debit,
            ActorCredentialId = actorCredentialId,
            IdempotencyKey = $"refund-original:{Guid.NewGuid():N}",
            ReferenceNumber = $"refund-original-{Guid.NewGuid():N}",
            Postings =
            [
                new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = amount,
                    WalletTypeId = wallet.WalletTypeId
                },
                new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = WalletLedgerEntryKind.Principal,
                    Amount = amount,
                    WalletTypeId = wallet.WalletTypeId
                }
            ]
        });

        result.IsSuccess.Should().BeTrue(result.Message);
        return result.Data!.OperationId;
    }

    private async Task<WalletCase> SeedRefundCaseAsync(
        Wallet wallet,
        Guid requesterCredentialId,
        Guid originalOperationId,
        decimal amount)
    {
        var walletCase = new WalletCase
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            WalletId = wallet.Id,
            OriginalOperationId = originalOperationId,
            CaseType = WalletCaseType.Refund,
            Status = WalletCaseStatus.Open,
            Amount = amount,
            ExternalReference = $"refund-case-{Guid.NewGuid():N}",
            Reason = "refund integration test",
            RequesterCredentialId = requesterCredentialId
        };
        await using var db = CreateDbContext();
        db.Set<WalletCase>().Add(walletCase);
        await db.SaveChangesAsync();
        return walletCase;
    }

    private async Task WaitForWalletRowLockAsync(Guid walletId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var probeDb = CreateDbContext();
            await using var probeTransaction = await probeDb.Database.BeginTransactionAsync();
            try
            {
                await probeDb.Database.ExecuteSqlInterpolatedAsync($"""
                    SELECT "ID"
                    FROM "Wallet"."Wallet"
                    WHERE "ID" = {walletId}
                    FOR UPDATE NOWAIT
                    """);
                await probeTransaction.RollbackAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail("Approval did not acquire the wallet row lock before the timeout.");
    }

    private static string SignWebhookPayload(string payload)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes("wallets-webhook-test-secret"));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static void EstablishTrustedActor(AsyncServiceScope scope, Guid credentialId)
    {
        var store = scope.ServiceProvider.GetRequiredService<ITrustedInvocationContextStore>();
        store.Set(TrustedContext(Actor(WalletsTestFixture.TestTenantId, credentialId)).Current!);
    }

    private static ITrustedInvocationContextAccessor TrustedContext(
        TrustedActorIdentity? actor = null,
        Guid? effectiveTenantId = null,
        TrustedServiceIdentity? service = null) =>
        new TestTrustedInvocationContextAccessor(new TrustedInvocationContext(
            actor,
            service,
            effectiveTenantId ?? actor?.TenantId,
            null,
            Guid.NewGuid()));

    private static TrustedActorIdentity Actor(Guid tenantId, Guid credentialId) => new(
        credentialId,
        Guid.NewGuid(),
        tenantId,
        Guid.NewGuid(),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        "test-generation",
        DateTimeOffset.UtcNow.AddMinutes(5));

    private static TrustedServiceIdentity Service() => new(
        XFrameworkServiceNames.Portal,
        XFrameworkServiceNames.Wallets,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        "test-service-generation");

    private sealed class TestTrustedInvocationContextAccessor(TrustedInvocationContext context)
        : ITrustedInvocationContextAccessor
    {
        public TrustedInvocationContext? Current => context;
    }

    private async Task SetTenantFeatureEnabled(string featureKey, bool enabled)
    {
        var (moduleKey, subFeatureKey) = TenantModuleFeatureKeys.Normalize(featureKey);
        await using (var db = CreateDbContext())
        {
            var feature = await db.Set<TenantModuleFeature>()
                .IgnoreQueryFilters()
                .SingleAsync(x =>
                    x.TenantId == WalletsTestFixture.TestTenantId &&
                    x.ModuleKey == moduleKey &&
                    x.SubFeatureKey == subFeatureKey);
            feature.IsEnabled = enabled;
            feature.ModifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var featureService = scope.ServiceProvider.GetRequiredService<ITenantModuleFeatureService>();
        featureService.Invalidate(WalletsTestFixture.TestTenantId, moduleKey, subFeatureKey);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using IdentityServer.Domain.Shared.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallets.Api.Services;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Enums;
using XFramework.Core.Services.FeatureGates;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;
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

        var approveDepositResponse = await HttpClient.PostAsJsonAsync("/api/wallets/deposits/approve", new ApproveDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            Reason = "checker approved deposit",
            Metadata = CreateMetadata()
        });
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

        var approveWithdrawalResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals/approve", new ApproveWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            Reason = "checker accepted withdrawal",
            Metadata = CreateMetadata()
        });
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
    }

    [Test]
    public async Task Http_RegisteredPaymentGateway_InitiatesDepositAndSettlesWithdrawalThroughProvider()
    {
        var credential = await SeedCredential();
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
        var approveWithdrawalResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals/approve", new ApproveWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            Reason = "provider payout approved",
            Metadata = CreateMetadata()
        });
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
    public async Task Ledger_PolicyRejectsFrozenWallet_DoesNotCreateOperationOrOutbox()
    {
        var credential = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 100m, WalletStatus.Frozen);
        var referenceNumber = $"policy-{Guid.NewGuid():N}";

        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
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
        result.StatusCode.Should().Be(403);
        result.Message.Should().Contain("frozen");

        await using var db = CreateDbContext();
        var operationExists = await db.Set<WalletOperation>().AnyAsync(x => x.ReferenceNumber == referenceNumber);
        var walletOutboxExists = await db.Set<WalletOutboxMessage>().AnyAsync(x => x.AggregateId == wallet.Id);

        operationExists.Should().BeFalse();
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
            var operationExists = await verifyDb.Set<WalletOperation>().AnyAsync(x => x.ReferenceNumber == referenceNumber);

            updatedWallet.Balance.Should().Be(500m);
            operationExists.Should().BeFalse();
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
        var approveResponse = await HttpClient.PostAsJsonAsync("/api/wallets/deposits/approve", new ApproveDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            Metadata = CreateMetadata()
        });
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

        await using var scope = WalletsTestFixture.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();
        var webhookService = scope.ServiceProvider.GetRequiredService<IWalletPaymentWebhookService>();

        var first = await webhookService.IngestAsync(request);
        var second = await webhookService.IngestAsync(request);

        first.IsSuccess.Should().BeTrue(first.Message);
        first.Data!.Duplicate.Should().BeFalse();
        second.IsSuccess.Should().BeTrue(second.Message);
        second.Data!.Duplicate.Should().BeTrue();

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
        db.Set<WalletApprovalRequest>().Add(foreignApproval);
        db.Set<DepositRequest>().Add(deposit);
        await db.SaveChangesAsync();

        var response = await HttpClient.PostAsJsonAsync("/api/wallets/deposits/approve", new ApproveDepositWorkflowRequest
        {
            RequestId = deposit.Id,
            Reason = "approve only local deposit",
            Metadata = CreateMetadata()
        });

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
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", WalletsTestFixture.TestTenantId.ToString()),
            new Claim("credentialId", Guid.NewGuid().ToString())
        ], "WalletsTest"));

        var resolver = new WalletRequestContextResolver(
            new HttpContextAccessor { HttpContext = httpContext },
            new ConfigurationBuilder().Build());
        var spoofedTenantId = Guid.NewGuid();

        var result = resolver.Resolve(new RequestBase
        {
            Metadata = new XFramework.Domain.Shared.BusinessObjects.RequestMetadata
            {
                TenantId = spoofedTenantId,
                RequestId = Guid.NewGuid()
            }
        });

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        result.Message.Should().Contain("trusted tenant");
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

        var approveResponse = await HttpClient.PostAsJsonAsync("/api/wallets/withdrawals/approve", new ApproveWithdrawalWorkflowRequest
        {
            RequestId = withdrawal.Id,
            Reason = "hold funds",
            Metadata = CreateMetadata()
        });
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

    private static string SignWebhookPayload(string payload)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes("wallets-webhook-test-secret"));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
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

using IdentityServer.Domain.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Wallets.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Enums;
using XFramework.Domain.Shared.Enums;
using XFramework.TestInfrastructure;
using SharedContracts = XFramework.Domain.Shared.Contracts;

namespace Wallets.IntegrationTests.Tests;

[TestFixture]
[Category(TestCategories.Integration)]
[Category(TestCategories.ExtendedIntegration)]
[Category(TestCategories.Wallets)]
[Category(TestCategories.Wrappers)]
public sealed class WrapperContractCoverageTests : WalletsTestBase
{
    [Test]
    public async Task CoreWalletWrappers_DirectCalls_ReturnExpectedResults()
    {
        var credential = await SeedCredential();
        var create = await WalletsTestFixture.ServiceWrapper.CreateWallet(new CreateWalletRequest
        {
            Metadata = Metadata(credential.Id),
            CredentialId = credential.Id,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            InitialBalance = 25m
        });
        create.IsSuccess.Should().BeTrue(create.Message);
        create.Response.Should().NotBeNull();

        var walletId = create.Response!.Id;
        var freezeApprovalId = await SeedApprovedWalletApproval(walletId, WalletOperationType.Freeze, credential.Id);
        var freeze = await WalletsTestFixture.ServiceWrapper.FreezeWallet(new FreezeWalletRequest
        {
            Metadata = Metadata(credential.Id),
            WalletId = walletId,
            ApprovalId = freezeApprovalId,
            Reason = "wrapper freeze coverage"
        });
        freeze.IsSuccess.Should().BeTrue(freeze.Message);

        var events = await WalletsTestFixture.ServiceWrapper.GetWalletEvents(new GetWalletEventsRequest
        {
            Metadata = Metadata(credential.Id),
            WalletId = walletId,
            PageSize = 20
        });
        events.IsSuccess.Should().BeTrue(events.Message);
        events.Response.Should().Contain(x => x.WalletId == walletId && x.EventType == "WalletFrozenEvent");

        var unfreezeApprovalId = await SeedApprovedWalletApproval(walletId, WalletOperationType.Unfreeze, credential.Id);
        var unfreeze = await WalletsTestFixture.ServiceWrapper.UnfreezeWallet(new UnfreezeWalletRequest
        {
            Metadata = Metadata(credential.Id),
            WalletId = walletId,
            ApprovalId = unfreezeApprovalId,
            Reason = "wrapper unfreeze coverage"
        });
        unfreeze.IsSuccess.Should().BeTrue(unfreeze.Message);

        var release = await WalletsTestFixture.ServiceWrapper.ReleaseTransaction(new ReleaseTransactionRequest
        {
            Metadata = Metadata(credential.Id),
            Id = Guid.NewGuid()
        });
        release.IsSuccess.Should().BeFalse();

        var reverse = await WalletsTestFixture.ServiceWrapper.ReverseTransaction(new ReverseTransactionRequest
        {
            Metadata = Metadata(credential.Id),
            TransactionId = Guid.NewGuid(),
            Reason = "missing transaction wrapper coverage"
        });
        reverse.IsSuccess.Should().BeFalse();
    }

    [Test]
    public async Task BatchWrappers_DirectCalls_PersistLedgerBackedOperations()
    {
        var credential = await SeedCredential();
        var sourceWallet = await SeedWallet(credential.Id, 200m);
        var destination = await SeedCredential();
        var destinationWallet = await SeedWallet(destination.Id, 25m);

        var incrementReference = Unique("batch-inc");
        var increment = await WalletsTestFixture.ServiceWrapper.BatchIncrementWallet(new BatchIncrementWalletRequest
        {
            Metadata = Metadata(credential.Id),
            Requests =
            [
                new BatchIncrementRequest
                {
                    WalletId = sourceWallet.Id,
                    WalletTypeId = WalletsTestFixture.TestWalletTypeId,
                    CredentialId = credential.Id,
                    Amount = 10m,
                    Fee = 0m,
                    ReferenceNumber = incrementReference
                }
            ]
        });
        increment.IsSuccess.Should().BeTrue(increment.Message);

        var decrementReference = Unique("batch-dec");
        var decrement = await WalletsTestFixture.ServiceWrapper.BatchDecrementWallet(new BatchDecrementWalletRequest
        {
            Metadata = Metadata(credential.Id),
            Requests =
            [
                new BatchDecrementRequest
                {
                    WalletId = sourceWallet.Id,
                    CredentialId = credential.Id,
                    Amount = 5m,
                    Fee = 0m,
                    ReferenceNumber = decrementReference
                }
            ]
        });
        decrement.IsSuccess.Should().BeTrue(decrement.Message);

        var transferReference = Unique("batch-xfer");
        var transfer = await WalletsTestFixture.ServiceWrapper.BatchTransferWallet(new BatchTransferWalletRequest
        {
            Metadata = Metadata(credential.Id),
            Requests =
            [
                new BatchTransferRequest
                {
                    FromWalletId = sourceWallet.Id,
                    ToWalletId = destinationWallet.Id,
                    FromCredentialId = credential.Id,
                    ToCredentialId = destination.Id,
                    Amount = 7m,
                    Fee = 0m,
                    ReferenceNumber = transferReference
                }
            ]
        });
        transfer.IsSuccess.Should().BeTrue(transfer.Message);

        await using var db = CreateDbContext();
        var references = new[] { incrementReference, decrementReference, transferReference };
        var transactionCount = await db.Set<WalletTransaction>()
            .IgnoreQueryFilters()
            .CountAsync(x => references.Contains(x.ReferenceNumber));
        transactionCount.Should().BeGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task DepositWorkflowWrappers_DirectCalls_ExerciseStateTransitions()
    {
        var credential = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();

        var validateDeposit = await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("dep-validate"));
        var validate = await WalletsTestFixture.ServiceWrapper.ValidateDepositWorkflow(new ValidateDepositWorkflowRequest
        {
            Metadata = Metadata(credential.Id),
            RequestId = validateDeposit.Id,
            Reason = "wrapper validate deposit"
        });
        validate.IsSuccess.Should().BeTrue(validate.Message);

        var approveDeposit = await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("dep-approve"));
        var checker = await SeedCredential();
        var approve = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.ApproveDepositWorkflow(new ApproveDepositWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = approveDeposit.Id,
            Reason = "wrapper approve deposit"
        }));
        approve.IsSuccess.Should().BeTrue(approve.Message);

        var rejectDeposit = await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("dep-reject"));
        var reject = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.RejectDepositWorkflow(new RejectDepositWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = rejectDeposit.Id,
            Reason = "wrapper reject deposit"
        }));
        reject.IsSuccess.Should().BeTrue(reject.Message);

        var settleDeposit = await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("dep-settle"));
        await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.ApproveDepositWorkflow(new ApproveDepositWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = settleDeposit.Id,
            Reason = "wrapper approve before settle"
        }));
        var settle = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.SettleDepositWorkflow(new SettleDepositWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = settleDeposit.Id,
            ProviderEventId = Unique("provider-event"),
            ProviderTransactionId = Unique("provider-tx"),
            ProviderStatus = "completed",
            IdempotencyKey = Unique("deposit-settle")
        }));
        settle.IsSuccess.Should().BeTrue(settle.Message);

        var failDeposit = await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("dep-fail"));
        var fail = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.FailDepositWorkflow(new FailDepositWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = failDeposit.Id,
            Reason = "wrapper fail deposit"
        }));
        fail.IsSuccess.Should().BeTrue(fail.Message);

        var cancelDeposit = await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("dep-cancel"));
        var cancel = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.CancelDepositWorkflow(new CancelDepositWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = cancelDeposit.Id,
            Reason = "wrapper cancel deposit"
        }));
        cancel.IsSuccess.Should().BeTrue(cancel.Message);
    }

    [Test]
    public async Task WithdrawalWorkflowWrappers_DirectCalls_ExerciseStateTransitions()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        var wallet = await SeedWallet(credential.Id, 500m);

        var validateWithdrawal = await CreateWithdrawalViaWrapper(credential.Id, wallet.Id, gatewayId, Unique("wd-validate"));
        var validate = await WalletsTestFixture.ServiceWrapper.ValidateWithdrawalWorkflow(new ValidateWithdrawalWorkflowRequest
        {
            Metadata = Metadata(credential.Id),
            RequestId = validateWithdrawal.Id,
            Reason = "wrapper validate withdrawal"
        });
        validate.IsSuccess.Should().BeTrue(validate.Message);

        var approveWithdrawal = await CreateWithdrawalViaWrapper(credential.Id, wallet.Id, gatewayId, Unique("wd-approve"));
        var approve = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.ApproveWithdrawalWorkflow(new ApproveWithdrawalWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = approveWithdrawal.Id,
            Reason = "wrapper approve withdrawal"
        }));
        approve.IsSuccess.Should().BeTrue(approve.Message);

        var rejectWithdrawal = await CreateWithdrawalViaWrapper(credential.Id, wallet.Id, gatewayId, Unique("wd-reject"));
        var reject = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.RejectWithdrawalWorkflow(new RejectWithdrawalWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = rejectWithdrawal.Id,
            Reason = "wrapper reject withdrawal"
        }));
        reject.IsSuccess.Should().BeTrue(reject.Message);

        var settleWithdrawal = await CreateWithdrawalViaWrapper(credential.Id, wallet.Id, gatewayId, Unique("wd-settle"));
        await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.ApproveWithdrawalWorkflow(new ApproveWithdrawalWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = settleWithdrawal.Id,
            Reason = "wrapper approve before settle"
        }));
        var settle = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.SettleWithdrawalWorkflow(new SettleWithdrawalWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = settleWithdrawal.Id,
            ProviderEventId = Unique("provider-event"),
            ProviderTransactionId = Unique("provider-tx"),
            ProviderStatus = "completed",
            IdempotencyKey = Unique("withdrawal-settle")
        }));
        settle.IsSuccess.Should().BeTrue(settle.Message);

        var failWithdrawal = await CreateWithdrawalViaWrapper(credential.Id, wallet.Id, gatewayId, Unique("wd-fail"));
        var fail = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.FailWithdrawalWorkflow(new FailWithdrawalWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = failWithdrawal.Id,
            Reason = "wrapper fail withdrawal"
        }));
        fail.IsSuccess.Should().BeTrue(fail.Message);

        var cancelWithdrawal = await CreateWithdrawalViaWrapper(credential.Id, wallet.Id, gatewayId, Unique("wd-cancel"));
        var cancel = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.CancelWithdrawalWorkflow(new CancelWithdrawalWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = cancelWithdrawal.Id,
            Reason = "wrapper cancel withdrawal"
        }));
        cancel.IsSuccess.Should().BeTrue(cancel.Message);

        var expireDeposit = await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("dep-expire"), DateTime.UtcNow.AddMinutes(-5));
        var expire = await WalletsTestFixture.ServiceWrapper.ExpireWalletWorkflows(new ExpireWalletWorkflowsRequest
        {
            Metadata = Metadata(checker.Id),
            IncludeDeposits = true,
            IncludeWithdrawals = true
        });
        expire.IsSuccess.Should().BeTrue(expire.Message);

        await using var db = CreateDbContext();
        var expired = await db.Set<DepositRequest>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.Id == expireDeposit.Id);
        expired.WorkflowStatus.Should().Be(WalletWorkflowStatus.Expired);
    }

    [Test]
    public async Task WebhookOutboxAndReconciliationWrappers_DirectCalls_UpdateOperationalRecords()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var gatewayId = await SeedPaymentGateway();
        var deposit = await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("webhook-dep"));
        await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.ApproveDepositWorkflow(new ApproveDepositWorkflowRequest
        {
            Metadata = Metadata(checker.Id),
            RequestId = deposit.Id,
            Reason = "approve before webhook"
        }));

        var payload = $$"""{"tenantId":"{{WalletsTestFixture.TestTenantId}}","reference":"{{deposit.ExternalReference}}","amount":{{deposit.Amount}}}""";
        var webhook = await WalletsTestFixture.ServiceWrapper.IngestWalletPaymentWebhook(new IngestWalletPaymentWebhookRequest
        {
            Metadata = Metadata(),
            ProviderKey = "test-provider",
            ExternalEventId = Unique("webhook-event"),
            ExternalReference = deposit.ExternalReference,
            ProviderTransactionId = Unique("provider-tx"),
            ProviderStatus = "completed",
            Amount = deposit.Amount,
            RawPayloadJson = payload,
            Signature = SignWebhookPayload(payload)
        });
        webhook.IsSuccess.Should().BeTrue(webhook.Message);

        var retryOutboxId = await SeedOutboxMessage(WalletOutboxStatus.Failed);
        var retry = await WalletsTestFixture.ServiceWrapper.RetryWalletOutboxMessage(new RetryWalletOutboxMessageRequest
        {
            Metadata = Metadata(checker.Id),
            OutboxMessageId = retryOutboxId,
            Reason = "wrapper retry"
        });
        retry.IsSuccess.Should().BeTrue(retry.Message);

        var replayOutboxId = await SeedOutboxMessage(WalletOutboxStatus.DeadLetter, attempts: 3, lastError: "failed");
        var replay = await WalletsTestFixture.ServiceWrapper.ReplayWalletOutboxMessage(new ReplayWalletOutboxMessageRequest
        {
            Metadata = Metadata(checker.Id),
            OutboxMessageId = replayOutboxId,
            Reason = "wrapper replay"
        });
        replay.IsSuccess.Should().BeTrue(replay.Message);

        var deadLetterOutboxId = await SeedOutboxMessage(WalletOutboxStatus.Failed);
        var deadLetter = await WalletsTestFixture.ServiceWrapper.DeadLetterWalletOutboxMessage(new DeadLetterWalletOutboxMessageRequest
        {
            Metadata = Metadata(checker.Id),
            OutboxMessageId = deadLetterOutboxId,
            Reason = "wrapper dead-letter"
        });
        deadLetter.IsSuccess.Should().BeTrue(deadLetter.Message);

        var reconciliationCredential = await SeedCredential();
        var wallet = await SeedWallet(reconciliationCredential.Id, 40m);
        var run = await WalletsTestFixture.ServiceWrapper.RunWalletReconciliation(new RunWalletReconciliationRequest
        {
            Metadata = Metadata(checker.Id),
            WalletId = wallet.Id
        });
        run.IsSuccess.Should().BeTrue(run.Message);

        var itemId = await SeedReconciliationItem(wallet.Id);
        var mark = await WalletsTestFixture.ServiceWrapper.MarkWalletReconciliationItem(new MarkWalletReconciliationItemRequest
        {
            Metadata = Metadata(checker.Id),
            ItemId = itemId,
            Reason = "wrapper mark reconciled"
        });
        mark.IsSuccess.Should().BeTrue(mark.Message);
    }

    [Test]
    public async Task PolicyApprovalCaseAndReportingWrappers_DirectCalls_ReturnExpectedResults()
    {
        var credential = await SeedCredential();
        var checker = await SeedCredential();
        var wallet = await SeedWallet(credential.Id, 300m);

        var policy = await WalletsTestFixture.ServiceWrapper.UpsertWalletPolicyRule(new UpsertWalletPolicyRuleRequest
        {
            Metadata = Metadata(checker.Id),
            Name = Unique("policy"),
            OperationType = WalletOperationType.Transfer,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            MaxSingleTransactionAmount = 10_000m,
            IsEnabled = true
        });
        policy.IsSuccess.Should().BeTrue(policy.Message);

        var fee = await WalletsTestFixture.ServiceWrapper.UpsertWalletFeeSchedule(new UpsertWalletFeeScheduleRequest
        {
            Metadata = Metadata(checker.Id),
            Name = Unique("fee"),
            OperationType = WalletOperationType.Transfer,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            FixedFee = 0m,
            PercentageFee = 0m,
            AllowRequestedFeeOverride = true,
            EffectiveAt = DateTime.UtcNow.AddMinutes(-5),
            IsEnabled = true
        });
        fee.IsSuccess.Should().BeTrue(fee.Message);

        var createApprovalReason = Unique("approval-create");
        var createApproval = await WalletsTestFixture.ServiceWrapper.CreateWalletApproval(new CreateWalletApprovalRequest
        {
            Metadata = Metadata(credential.Id),
            WalletId = wallet.Id,
            OperationType = WalletOperationType.Reversal,
            Amount = 20m,
            Reason = createApprovalReason
        });
        createApproval.IsSuccess.Should().BeTrue(createApproval.Message);

        var approvalId = await LoadApprovalIdByReason(createApprovalReason);
        var approve = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.ApproveWalletApproval(new ApproveWalletApprovalRequest
        {
            Metadata = Metadata(checker.Id),
            ApprovalId = approvalId,
            Reason = "wrapper approve maker-checker"
        }));
        approve.IsSuccess.Should().BeTrue(approve.Message);

        var rejectApprovalReason = Unique("approval-reject");
        await WalletsTestFixture.ServiceWrapper.CreateWalletApproval(new CreateWalletApprovalRequest
        {
            Metadata = Metadata(credential.Id),
            WalletId = wallet.Id,
            OperationType = WalletOperationType.ManualAdjustment,
            Amount = 15m,
            Reason = rejectApprovalReason
        });
        var rejectApprovalId = await LoadApprovalIdByReason(rejectApprovalReason);
        var reject = await AsActor(checker.Id, () =>
            WalletsTestFixture.ServiceWrapper.RejectWalletApproval(new RejectWalletApprovalRequest
        {
            Metadata = Metadata(checker.Id),
            ApprovalId = rejectApprovalId,
            Reason = "wrapper reject maker-checker"
        }));
        reject.IsSuccess.Should().BeTrue(reject.Message);

        var createCaseReference = Unique("case");
        var createCase = await WalletsTestFixture.ServiceWrapper.CreateWalletCase(new CreateWalletCaseRequest
        {
            Metadata = Metadata(credential.Id),
            WalletId = wallet.Id,
            CaseType = WalletCaseType.Refund,
            Amount = 10m,
            ExternalReference = createCaseReference,
            ReasonCode = "WRAPPER",
            Reason = "wrapper case"
        });
        createCase.IsSuccess.Should().BeTrue(createCase.Message);

        var caseId = await LoadCaseIdByExternalReference(createCaseReference);
        var resolveCase = await WalletsTestFixture.ServiceWrapper.ResolveWalletCase(new ResolveWalletCaseRequest
        {
            Metadata = Metadata(checker.Id),
            CaseId = caseId,
            Approve = false,
            Reason = "wrapper case resolved"
        });
        resolveCase.IsSuccess.Should().BeTrue(resolveCase.Message);

        var reference = Unique("report-credit");
        await CreditWalletViaWrapper(wallet, credential.Id, reference, 30m);

        var statement = await WalletsTestFixture.ServiceWrapper.WalletStatement(new WalletStatementRequest
        {
            Metadata = Metadata(checker.Id),
            WalletId = wallet.Id,
            ReferenceNumber = reference
        });
        statement.IsSuccess.Should().BeTrue(statement.Message);

        var ledgerEntries = await WalletsTestFixture.ServiceWrapper.WalletLedgerEntries(new WalletLedgerEntriesRequest
        {
            Metadata = Metadata(checker.Id),
            WalletId = wallet.Id,
            ReferenceNumber = reference
        });
        ledgerEntries.IsSuccess.Should().BeTrue(ledgerEntries.Message);

        var balanceAsOf = await WalletsTestFixture.ServiceWrapper.WalletBalanceAsOf(new WalletBalanceAsOfRequest
        {
            Metadata = Metadata(checker.Id),
            WalletId = wallet.Id,
            AsOf = DateTime.UtcNow.AddMinutes(1)
        });
        balanceAsOf.IsSuccess.Should().BeTrue(balanceAsOf.Message);

        var operationHistory = await WalletsTestFixture.ServiceWrapper.WalletOperationHistory(new WalletOperationHistoryRequest
        {
            Metadata = Metadata(checker.Id),
            WalletId = wallet.Id,
            ReferenceNumber = reference
        });
        operationHistory.IsSuccess.Should().BeTrue(operationHistory.Message);

        var failedRejected = await WalletsTestFixture.ServiceWrapper.WalletFailedRejectedOperations(new WalletFailedRejectedOperationsRequest
        {
            Metadata = Metadata(checker.Id),
            WalletId = wallet.Id
        });
        failedRejected.IsSuccess.Should().BeTrue(failedRejected.Message);

        var reconciliationItemId = await SeedReconciliationItem(wallet.Id);
        var unreconciled = await WalletsTestFixture.ServiceWrapper.WalletUnreconciledBalances(new WalletUnreconciledBalancesRequest
        {
            Metadata = Metadata(checker.Id),
            WalletId = wallet.Id
        });
        unreconciled.IsSuccess.Should().BeTrue(unreconciled.Message);

        var failedOutboxId = await SeedOutboxMessage(WalletOutboxStatus.Failed, lastError: "wrapper failure report");
        var outboxFailures = await WalletsTestFixture.ServiceWrapper.WalletOutboxFailures(new WalletOutboxFailuresRequest
        {
            Metadata = Metadata(checker.Id)
        });
        outboxFailures.IsSuccess.Should().BeTrue(outboxFailures.Message);

        var gatewayId = await SeedPaymentGateway();
        await CreateDepositViaWrapper(credential.Id, gatewayId, Unique("settlement-report"));
        var settlementReport = await WalletsTestFixture.ServiceWrapper.WalletSettlementReport(new WalletSettlementReportRequest
        {
            Metadata = Metadata(checker.Id),
            IncludeDeposits = true,
            IncludeWithdrawals = true
        });
        settlementReport.IsSuccess.Should().BeTrue(settlementReport.Message);

        await using var db = CreateDbContext();
        (await db.Set<WalletReconciliationItem>().IgnoreQueryFilters().AnyAsync(x => x.Id == reconciliationItemId))
            .Should().BeTrue();
        (await db.Set<WalletOutboxMessage>().IgnoreQueryFilters().AnyAsync(x => x.Id == failedOutboxId))
            .Should().BeTrue();
    }

    private async Task<DepositRequest> CreateDepositViaWrapper(
        Guid credentialId,
        Guid gatewayId,
        string externalReference,
        DateTime? expiryDate = null)
    {
        var result = await WalletsTestFixture.ServiceWrapper.CreateDepositWorkflow(new CreateDepositWorkflowRequest
        {
            Metadata = Metadata(credentialId),
            CredentialId = credentialId,
            WalletTypeId = WalletsTestFixture.TestWalletTypeId,
            GatewayId = gatewayId,
            Amount = 25m,
            RequestedFee = 0m,
            ExternalReference = externalReference,
            ExpiryDate = expiryDate,
            Remarks = "wrapper deposit coverage"
        });
        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        return await db.Set<DepositRequest>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.ExternalReference == externalReference);
    }

    private async Task<WithdrawalRequest> CreateWithdrawalViaWrapper(
        Guid credentialId,
        Guid walletId,
        Guid gatewayId,
        string externalReference)
    {
        var result = await WalletsTestFixture.ServiceWrapper.CreateWithdrawalWorkflow(new CreateWithdrawalWorkflowRequest
        {
            Metadata = Metadata(credentialId),
            CredentialId = credentialId,
            WalletId = walletId,
            GatewayId = gatewayId,
            Amount = 10m,
            RequestedFee = 0m,
            ExternalReference = externalReference,
            Address = "wallet-wrapper-test",
            Remarks = "wrapper withdrawal coverage"
        });
        result.IsSuccess.Should().BeTrue(result.Message);

        await using var db = CreateDbContext();
        return await db.Set<WithdrawalRequest>()
            .IgnoreQueryFilters()
            .SingleAsync(x => x.ExternalReference == externalReference);
    }

    private async Task CreditWalletViaWrapper(
        Wallet wallet,
        Guid credentialId,
        string reference,
        decimal amount)
    {
        var result = await WalletsTestFixture.ServiceWrapper.IncrementWallet(new IncrementWalletRequest
        {
            Metadata = Metadata(credentialId),
            WalletId = wallet.Id,
            WalletTypeId = wallet.WalletTypeId ?? WalletsTestFixture.TestWalletTypeId,
            CredentialId = credentialId,
            Amount = amount,
            Fee = 0m,
            ReferenceNumber = reference,
            Remarks = "wrapper reporting credit"
        });
        result.IsSuccess.Should().BeTrue(result.Message);
    }

    private async Task<Guid> SeedPaymentGateway(Guid? gatewayId = null)
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
            Name = Unique("GatewayCategory"),
            Description = "Wallet wrapper integration test gateway category",
            IsEnabled = true
        };
        var gateway = new SharedContracts.PaymentGateway
        {
            Id = gatewayId ?? Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            GatewayCategoryId = category.Id,
            Name = Unique("Gateway"),
            Description = "Wallet wrapper integration test gateway",
            ServiceCharge = 0m,
            ConvenienceFee = 0m,
            IsEnabled = true
        };

        db.Set<SharedContracts.PaymentGatewayCategory>().Add(category);
        db.Set<SharedContracts.PaymentGateway>().Add(gateway);
        await db.SaveChangesAsync();
        return gateway.Id;
    }

    private async Task<Guid> SeedOutboxMessage(
        WalletOutboxStatus status,
        int attempts = 0,
        string? lastError = null)
    {
        await using var db = CreateDbContext();
        var message = new WalletOutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            EventType = "wallet.wrapper.coverage",
            AggregateType = nameof(Wallet),
            AggregateId = Guid.NewGuid(),
            PayloadJson = "{}",
            Status = status,
            Attempts = attempts,
            LastError = lastError,
            MaxAttempts = 5,
            IsEnabled = true
        };

        db.Set<WalletOutboxMessage>().Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    private async Task<Guid> SeedReconciliationItem(Guid? walletId)
    {
        await using var db = CreateDbContext();
        var run = new WalletReconciliationRun
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            Status = WalletReconciliationStatus.Drifted,
            StartedAt = DateTime.UtcNow.AddMinutes(-1),
            CompletedAt = DateTime.UtcNow,
            CheckedCount = 1,
            DriftCount = 1,
            IsEnabled = true
        };
        var item = new WalletReconciliationItem
        {
            Id = Guid.NewGuid(),
            TenantId = WalletsTestFixture.TestTenantId,
            RunId = run.Id,
            WalletId = walletId,
            CheckType = "wrapper_seeded_drift",
            Status = WalletReconciliationStatus.Drifted,
            ExpectedAmount = 0m,
            ActualAmount = 1m,
            DriftAmount = 1m,
            RepairSuggestion = "Wrapper coverage seeded reconciliation item",
            IsEnabled = true
        };

        db.Set<WalletReconciliationRun>().Add(run);
        db.Set<WalletReconciliationItem>().Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<Guid> LoadApprovalIdByReason(string reason)
    {
        await using var db = CreateDbContext();
        return await db.Set<WalletApprovalRequest>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == WalletsTestFixture.TestTenantId && x.Reason == reason)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task<Guid> LoadCaseIdByExternalReference(string externalReference)
    {
        await using var db = CreateDbContext();
        return await db.Set<WalletCase>()
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == WalletsTestFixture.TestTenantId && x.ExternalReference == externalReference)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static XFramework.Domain.Shared.BusinessObjects.RequestMetadata Metadata(Guid? credentialId = null)
    {
        return CreateMetadata();
    }

    private static async Task<T> AsActor<T>(Guid credentialId, Func<Task<T>> action)
    {
        using var actorScope = WalletsTestFixture.PushActor(credentialId);
        return await action();
    }

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string SignWebhookPayload(string payload)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes("wallets-webhook-test-secret"));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}

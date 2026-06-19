using System.Diagnostics;
using System.Text;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Api.Events;
using XFramework.Core.Loggers;
using XFramework.Core.Observability;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Api.Services;

/// <summary>
/// Service for managing wallet operations including balance changes, transfers, and conversions.
/// Consolidates all wallet operation logic previously handled by MediatR command handlers.
/// </summary>
public sealed class WalletOperationsService : IWalletOperationsService
{
    private readonly IDataContext _dataContext;
    private readonly ITenantResolver _tenantService;
    private readonly IHelperService _helperService;
    private readonly ILogger<WalletOperationsService> _logger;
    private readonly IWalletEventPublisher _eventPublisher;
    private readonly IWalletLedgerService _ledgerService;
    private readonly IWalletRequestContextResolver _contextResolver;
    private readonly IWalletFeeCalculator _feeCalculator;
    private readonly IWalletFeatureGateService _featureGateService;

    public WalletOperationsService(
        IDataContext dataContext,
        ITenantResolver tenantService,
        IHelperService helperService,
        ILogger<WalletOperationsService> logger,
        IWalletEventPublisher eventPublisher,
        IWalletLedgerService ledgerService,
        IWalletRequestContextResolver contextResolver,
        IWalletFeeCalculator feeCalculator,
        IWalletFeatureGateService featureGateService)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _helperService = helperService ?? throw new ArgumentNullException(nameof(helperService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
        _contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        _feeCalculator = feeCalculator ?? throw new ArgumentNullException(nameof(feeCalculator));
        _featureGateService = featureGateService ?? throw new ArgumentNullException(nameof(featureGateService));
    }

    /// <inheritdoc />
    public async Task<Result<Wallet>> CreateWalletAsync(
        Guid credentialId,
        Guid walletTypeId,
        decimal initialBalance,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(tenantId);

            if (initialBalance < 0)
            {
                return Result<Wallet>.Failure("Initial balance cannot be negative", 400);
            }

            var walletType = await _dataContext.Query<Wallets.Domain.Shared.Contracts.WalletType>()
                .IgnoreQueryFilters()
                .Where(x => x.Id == walletTypeId && x.TenantId == tenant.Id && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (walletType is null)
            {
                _logger.EntityNotFound("WalletType", walletTypeId);
                return Result<Wallet>.Failure("Wallet type not found", 404);
            }

            // Generate unique account number
            string accountNumber;
            bool accountNumberExists;
            do
            {
                accountNumber = $"{_helperService.GenerateRandomNumber(1000_0000_0000, 9999_9999_9999)}";
                accountNumberExists = await _dataContext.Query<Wallet>()
                    .IgnoreQueryFilters()
                    .AnyAsync(x => x.TenantId == tenant.Id && !x.IsDeleted && x.AccountNumber == accountNumber, cancellationToken);
            } while (accountNumberExists);

            var wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CredentialId = credentialId,
                WalletTypeId = walletType.Id,
                Balance = 0,
                BondBalanceRule = walletType.BondBalanceRule,
                MaintainingBalanceRule = walletType.MaintainingBalanceRule,
                MinTransferRule = walletType.MinTransferRule,
                MaxTransferRule = walletType.MaxTransferRule,
                AccountNumber = accountNumber,
                DebitOnHoldBalance = 0,
                CreditOnHoldBalance = 0,
                TransferableBalance = 0,
                IsEnabled = true
            };

            if (initialBalance > 0)
            {
                var referenceNumber = $"wallet-create:{wallet.Id}";
                var transaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    CredentialId = credentialId,
                    WalletId = wallet.Id,
                    Amount = initialBalance,
                    TransactionFee = 0,
                    Remarks = "Initial wallet balance",
                    Description = "Initial wallet balance",
                    TransactionType = TransactionType.Credit,
                    Held = false,
                    Released = true,
                    ReferenceNumber = referenceNumber
                };

                var ledgerResult = await _ledgerService.ExecuteAsync(
                    new WalletLedgerExecutionRequest
                    {
                        TenantId = tenant.Id,
                        OperationType = WalletOperationType.Credit,
                        ActorCredentialId = credentialId,
                        ReferenceNumber = referenceNumber,
                        Reason = "Initial wallet balance",
                        NewWallets = [wallet],
                        Postings =
                        [
                            new WalletLedgerPostingRequest
                            {
                                Direction = WalletLedgerDirection.Debit,
                                BalanceBucket = WalletBalanceBucket.External,
                                EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                                Amount = initialBalance,
                                WalletTypeId = wallet.WalletTypeId,
                                ReferenceNumber = referenceNumber,
                                CounterpartyType = "initial-balance-source",
                                CounterpartyReference = "wallet-create",
                                Description = "Initial balance source"
                            },
                            new WalletLedgerPostingRequest
                            {
                                WalletId = wallet.Id,
                                WalletTransaction = transaction,
                                Direction = WalletLedgerDirection.Credit,
                                BalanceBucket = WalletBalanceBucket.Available,
                                EntryKind = WalletLedgerEntryKind.Principal,
                                Amount = initialBalance,
                                WalletTypeId = wallet.WalletTypeId,
                                ReferenceNumber = referenceNumber,
                                Description = "Initial wallet balance"
                            }
                        ],
                        ReadModels = [transaction]
                    },
                    cancellationToken);

                if (!ledgerResult.IsSuccess)
                {
                    return Result<Wallet>.Failure(
                        ledgerResult.Message ?? "Wallet ledger operation failed",
                        ledgerResult.StatusCode);
                }
            }
            else
            {
                _dataContext.Add(wallet);
                await _dataContext.SaveChangesAsync(cancellationToken);
            }

            _logger.EntityCreated("Wallet", wallet.Id);

            return Result<Wallet>.Success(wallet);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CreateWallet", "Wallet", Guid.Empty, ex.Message, ex);
            return Result<Wallet>.Failure("An error occurred while creating the wallet", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Wallet>> GetWalletAsync(
        Guid walletId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(tenantId);

            var wallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Where(w => w.TenantId == tenant.Id && !w.IsDeleted && w.Id == walletId)
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet is null)
            {
                return Result<Wallet>.NotFound($"Wallet {walletId} not found");
            }

            return Result<Wallet>.Success(wallet);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("GetWallet", "Wallet", walletId, ex.Message, ex);
            return Result<Wallet>.Failure("An error occurred while retrieving the wallet", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result<List<Wallet>>> GetWalletsByCredentialAsync(
        Guid credentialId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(tenantId);

            var wallets = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Where(w => w.TenantId == tenant.Id && !w.IsDeleted && w.CredentialId == credentialId)
                .ToListAsync(cancellationToken);

            return Result<List<Wallet>>.Success(wallets);
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("GetWalletsByCredential", "Wallet", Guid.Empty, ex.Message, ex);
            return Result<List<Wallet>>.Failure("An error occurred while retrieving wallets", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> IncrementBalanceAsync(
        IncrementWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySources.Wallet.StartActivity("Wallet.IncrementBalance");
        activity?.SetTag("wallet.id", request.WalletId);
        activity?.SetTag("wallet.amount", request.TotalAmount);
        activity?.SetTag("wallet.on_hold", request.OnHold);
        activity?.SetTag("credential.id", request.CredentialId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var tenantResult = ResolveTrustedTenantId(request, request.CredentialId);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsDeposits, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);
            activity?.SetTag("tenant.id", tenant.Id);

            if (request.TotalAmount <= 0 || request.TotalFee < 0)
            {
                _logger.ValidationFailed("IncrementWallet", "Invalid increment amount");
                return Result.Failure("Invalid increment amount", 400);
            }

            if (request.TotalFee > request.TotalAmount)
            {
                _logger.BusinessRuleViolation("IncrementWallet", "Total fee exceeds increment amount");
                return Result.Failure("Total fee cannot exceed increment amount", 400);
            }

            var wallet = await FindWalletForTransactionAsync(
                tenant.Id,
                request.WalletId,
                request.CredentialId,
                request.WalletTypeId,
                cancellationToken);

            if (wallet is null)
            {
                // Auto-create wallet if WalletTypeId provided
                if (request.WalletId == Guid.Empty && request.WalletTypeId != Guid.Empty)
                {
                    var createResult = await CreateWalletAsync(
                        request.CredentialId,
                        request.WalletTypeId,
                        0,
                        tenant.Id,
                        cancellationToken);

                    if (!createResult.IsSuccess)
                    {
                        _logger.OperationFailed("AutoCreateWallet", "Wallet", Guid.Empty, "Wallet creation failed during increment");
                        return Result.Failure("Error creating wallet", 500);
                    }

                    wallet = createResult.Data;
                    if (wallet is null)
                    {
                        _logger.OperationFailed("AutoCreateWallet", "Wallet", Guid.Empty, "Wallet creation returned no wallet during increment");
                        return Result.Failure("Error creating wallet", 500);
                    }
                }
                else
                {
                    _logger.EntityNotFound("Wallet", request.WalletId);
                    return Result.NotFound("Wallet not found");
                }
            }

            var targetCheck = CheckWalletRequestTarget(wallet, request.CredentialId, request.WalletTypeId);
            if (targetCheck is not null) return targetCheck;

            var statusCheck = CheckWalletStatus(wallet, "IncrementWallet");
            if (statusCheck is not null) return statusCheck;

            // Validate min transfer rule
            if (wallet.MinTransferRule.HasValue && request.TotalAmount < wallet.MinTransferRule.Value)
            {
                _logger.BusinessRuleViolation("IncrementWallet", $"Amount {request.TotalAmount} below minimum {wallet.MinTransferRule.Value}");
                return Result.Failure($"Amount must be at least {wallet.MinTransferRule.Value}", 400);
            }

            // Validate max transfer rule
            if (wallet.MaxTransferRule.HasValue && request.TotalAmount > wallet.MaxTransferRule.Value)
            {
                _logger.BusinessRuleViolation("IncrementWallet", $"Amount {request.TotalAmount} exceeds maximum {wallet.MaxTransferRule.Value}");
                return Result.Failure($"Amount must not exceed {wallet.MaxTransferRule.Value}", 400);
            }

            var feeResult = await CalculateFeeAsync(
                tenant.Id,
                WalletOperationType.Credit,
                wallet.WalletTypeId,
                request.CurrencyId,
                request.TotalAmount,
                request.TotalFee,
                cancellationToken);
            if (!feeResult.IsSuccess)
            {
                return Result.Failure(feeResult.Message!, feeResult.StatusCode);
            }

            var fee = feeResult.Data!.RequestedFee;
            if (fee > request.TotalAmount)
            {
                return Result.Failure("Total fee cannot exceed increment amount", 400);
            }

            var netCredit = request.TotalAmount - fee;
            var referenceNumber = CreateReferenceNumber(request);
            var transaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = wallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = fee,
                Remarks = request.Remarks,
                TransactionType = TransactionType.Credit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            var postings = new List<WalletLedgerPostingRequest>
            {
                new()
                {
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = request.TotalAmount,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    CounterpartyType = "external-funding-source",
                    CounterpartyReference = request.ReferenceNumber,
                    Description = "External funding source"
                }
            };

            if (netCredit > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    WalletTransaction = transaction,
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = request.OnHold ? WalletBalanceBucket.CreditHold : WalletBalanceBucket.Available,
                    EntryKind = request.OnHold ? WalletLedgerEntryKind.Hold : WalletLedgerEntryKind.Principal,
                    Amount = netCredit,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    Description = request.OnHold ? "Credit held funds" : "Credit available funds"
                });
            }

            if (fee > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.Fee,
                    EntryKind = WalletLedgerEntryKind.Fee,
                    Amount = fee,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    CounterpartyType = "platform-fee",
                    CounterpartyReference = "wallets",
                    Description = "Platform fee"
                });
            }

            var ledgerResult = await _ledgerService.ExecuteAsync(
                new WalletLedgerExecutionRequest
                {
                    TenantId = tenant.Id,
                    OperationType = WalletOperationType.Credit,
                    ActorCredentialId = request.CredentialId,
                    IdempotencyKey = request.IdempotencyKey,
                    ReferenceNumber = referenceNumber,
                    Reason = request.Remarks,
                    RequestedFee = request.TotalFee,
                    CalculatedFee = feeResult.Data.CalculatedFee,
                    Postings = postings,
                    ReadModels = [transaction]
                },
                cancellationToken);

            if (!ledgerResult.IsSuccess)
            {
                return Result.Failure(ledgerResult.Message ?? "Wallet ledger operation failed", ledgerResult.StatusCode);
            }

            if (ledgerResult.Data?.AlreadyProcessed == true)
            {
                return Result.Success("Transaction already processed");
            }

            stopwatch.Stop();
            var runningBalance = ledgerResult.Data?.Wallets.TryGetValue(wallet.Id, out var balanceResult) == true
                ? balanceResult.Balance
                : wallet.Balance;

            // Record metrics
            XFrameworkMetrics.WalletIncrements.Add(1,
                new KeyValuePair<string, object?>("tenant.id", tenant.Id.ToString()),
                new KeyValuePair<string, object?>("on_hold", request.OnHold));
            XFrameworkMetrics.WalletOperationDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("operation", "increment"),
                new KeyValuePair<string, object?>("result", "success"));
            XFrameworkMetrics.WalletTransactionAmount.Record(request.TotalAmount,
                new KeyValuePair<string, object?>("operation", "increment"));

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("operation.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("wallet.new_balance", runningBalance);

            _logger.WalletIncremented(wallet.Id, request.TotalAmount, "Primary", runningBalance);
            _logger.TransactionCreated(transaction.Id, wallet.Id, "Credit", request.TotalAmount);

            await _eventPublisher.PublishAsync(new TransactionCompletedEvent
            {
                EventType = nameof(TransactionCompletedEvent),
                WalletId = wallet.Id,
                CredentialId = request.CredentialId,
                TenantId = tenant.Id,
                Amount = request.TotalAmount,
                TransactionType = "Credit",
                ReferenceNumber = transaction.ReferenceNumber,
                RunningBalance = runningBalance
            });

            return Result.Success();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            XFrameworkMetrics.WalletOperationDuration.Record(stopwatch.ElapsedMilliseconds,
                new KeyValuePair<string, object?>("operation", "increment"),
                new KeyValuePair<string, object?>("result", "error"));

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            activity?.SetTag("exception.stacktrace", ex.StackTrace);

            _logger.OperationFailed("IncrementWallet", "Wallet", request.WalletId, ex.Message, ex);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> DecrementBalanceAsync(
        DecrementWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantResult = ResolveTrustedTenantId(request, request.CredentialId);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsWithdrawals, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);

            if (request.TotalAmount <= 0 || request.TotalFee < 0)
            {
                _logger.ValidationFailed("DecrementWallet", "Invalid decrement amount");
                return Result.Failure("Invalid decrement amount", 400);
            }

            var wallet = await FindWalletForTransactionAsync(
                tenant.Id,
                request.WalletId,
                request.CredentialId,
                request.WalletTypeId,
                cancellationToken);

            if (wallet == null)
            {
                _logger.EntityNotFound("Wallet", request.WalletId);
                return Result.NotFound("Wallet not found");
            }

            var targetCheck = CheckWalletRequestTarget(wallet, request.CredentialId, request.WalletTypeId);
            if (targetCheck is not null) return targetCheck;

            var statusCheck = CheckWalletStatus(wallet, "DecrementWallet");
            if (statusCheck is not null) return statusCheck;

            var feeResult = await CalculateFeeAsync(
                tenant.Id,
                WalletOperationType.Debit,
                wallet.WalletTypeId,
                request.CurrencyId,
                request.TotalAmount,
                request.TotalFee,
                cancellationToken);
            if (!feeResult.IsSuccess)
            {
                return Result.Failure(feeResult.Message!, feeResult.StatusCode);
            }

            var fee = feeResult.Data!.RequestedFee;
            var totalDebit = request.TotalAmount + fee;

            // Check sufficient balance
            if (wallet.AvailableBalance < totalDebit)
            {
                _logger.InsufficientBalance(wallet.Id, totalDebit, wallet.AvailableBalance);
                return Result.Failure("Insufficient funds", 400);
            }

            var referenceNumber = CreateReferenceNumber(request);
            var transaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = wallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = fee,
                Remarks = request.Remarks,
                TransactionType = TransactionType.Debit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            var postings = new List<WalletLedgerPostingRequest>
            {
                new()
                {
                    WalletId = wallet.Id,
                    WalletTransaction = transaction,
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = request.OnHold ? WalletBalanceBucket.DebitHold : WalletBalanceBucket.Available,
                    EntryKind = request.OnHold ? WalletLedgerEntryKind.Hold : WalletLedgerEntryKind.Principal,
                    Amount = totalDebit,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    Description = request.OnHold ? "Debit held funds" : "Debit available funds"
                },
                new()
                {
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.External,
                    EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                    Amount = request.TotalAmount,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    CounterpartyType = "external-payout-destination",
                    CounterpartyReference = request.ReferenceNumber,
                    Description = "External payout destination"
                }
            };

            if (fee > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.Fee,
                    EntryKind = WalletLedgerEntryKind.Fee,
                    Amount = fee,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    CounterpartyType = "platform-fee",
                    CounterpartyReference = "wallets",
                    Description = "Platform fee"
                });
            }

            var ledgerResult = await _ledgerService.ExecuteAsync(
                new WalletLedgerExecutionRequest
                {
                    TenantId = tenant.Id,
                    OperationType = WalletOperationType.Debit,
                    ActorCredentialId = request.CredentialId,
                    IdempotencyKey = request.IdempotencyKey,
                    ReferenceNumber = referenceNumber,
                    Reason = request.Remarks,
                    RequestedFee = request.TotalFee,
                    CalculatedFee = feeResult.Data.CalculatedFee,
                    Postings = postings,
                    ReadModels = [transaction]
                },
                cancellationToken);

            if (!ledgerResult.IsSuccess)
            {
                return Result.Failure(ledgerResult.Message ?? "Wallet ledger operation failed", ledgerResult.StatusCode);
            }

            if (ledgerResult.Data?.AlreadyProcessed == true)
            {
                return Result.Success("Transaction already processed");
            }

            var runningBalance = ledgerResult.Data?.Wallets.TryGetValue(wallet.Id, out var balanceResult) == true
                ? balanceResult.Balance
                : wallet.Balance;

            _logger.WalletDecremented(wallet.Id, request.TotalAmount, "Primary", runningBalance);
            _logger.TransactionCreated(transaction.Id, wallet.Id, "Debit", request.TotalAmount);

            await _eventPublisher.PublishAsync(new TransactionCompletedEvent
            {
                EventType = nameof(TransactionCompletedEvent),
                WalletId = wallet.Id,
                CredentialId = request.CredentialId,
                TenantId = tenant.Id,
                Amount = request.TotalAmount,
                TransactionType = "Debit",
                ReferenceNumber = transaction.ReferenceNumber,
                RunningBalance = runningBalance
            });

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("DecrementWallet", "Wallet", request.WalletId, ex.Message, ex);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> TransferAsync(
        TransferWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantResult = ResolveTrustedTenantId(request, request.CredentialId);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsTransfers, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);

            // Validate amount and fee
            if (request.TotalAmount <= 0 || request.Fee < 0)
            {
                _logger.ValidationFailed("TransferWallet", "Invalid amount or fee");
                return Result.Failure("Invalid amount or fee", 400);
            }

            // Validate wallet type ID
            if (request.WalletTypeId == Guid.Empty)
            {
                _logger.ValidationFailed("TransferWallet", "Wallet type ID is required");
                return Result.Failure("Wallet type ID is required", 400);
            }

            // Fetch sender and recipient wallets
            var senderWallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => !x.IsDeleted)
                .Where(x => x.CredentialId == request.CredentialId)
                .Where(x => x.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            var recipientWallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => !x.IsDeleted)
                .Where(x => x.CredentialId == request.RecipientCredentialId)
                .Where(x => x.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            // Fetch user information for masking (direct DB query — shared database in VSA)
            var senderCredential = await _dataContext.Query<IdentityCredential>()
                .IgnoreQueryFilters()
                .Include(c => c.IdentityInfo)
                .Where(c => c.Id == request.CredentialId && c.TenantId == tenant.Id && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            var recipientCredential = await _dataContext.Query<IdentityCredential>()
                .IgnoreQueryFilters()
                .Include(c => c.IdentityInfo)
                .Where(c => c.Id == request.RecipientCredentialId && c.TenantId == tenant.Id && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (senderCredential is null)
            {
                _logger.EntityNotFound("Sender", request.CredentialId);
                return Result.NotFound("Sender not found");
            }

            if (recipientCredential is null)
            {
                _logger.EntityNotFound("Recipient", request.RecipientCredentialId);
                return Result.NotFound("Recipient not found");
            }

            if (senderWallet == null)
            {
                _logger.EntityNotFound("SenderWallet", Guid.Empty);
                return Result.NotFound("Wallet not found");
            }

            var statusCheck = CheckWalletStatus(senderWallet, "Transfer");
            if (statusCheck is not null) return statusCheck;

            // Auto-create recipient wallet if it doesn't exist
            if (recipientWallet is null)
            {
                var createResult = await CreateWalletAsync(
                    request.RecipientCredentialId,
                    request.WalletTypeId,
                    0,
                    tenant.Id,
                    cancellationToken);

                if (!createResult.IsSuccess)
                {
                    _logger.OperationFailed("AutoCreateRecipientWallet", "Wallet", Guid.Empty, "Recipient wallet creation failed during transfer");
                    return Result.Failure("Recipient wallet not found and could not be created", 404);
                }

                recipientWallet = createResult.Data;
                if (recipientWallet is null)
                {
                    _logger.OperationFailed("AutoCreateRecipientWallet", "Wallet", Guid.Empty, "Recipient wallet creation returned no wallet during transfer");
                    return Result.Failure("Recipient wallet not found and could not be created", 404);
                }
            }

            // Check for self-transfer
            if (request.CredentialId == request.RecipientCredentialId)
            {
                _logger.BusinessRuleViolation("TransferWallet", "Cannot transfer to the same wallet");
                return Result.Failure("Cannot transfer to the same wallet", 400);
            }

            // Calculate deduction amounts based on transfer deduction type
            decimal totalDecrement;
            decimal totalIncrement;
            TransferDeductionType transferDeductionType;

            if (request.TransferDeductionType == TransferDeductionType.Default)
            {
                // Fetch config from registry
                var transferDeductionTypeConfig = await _dataContext.Query<RegistryConfiguration>()
                    .Where(x => x.TenantId == tenant.Id)
                    .Where(x => x.Key == "Settings:Wallet:Transfer:DeductionType")
                    .FirstOrDefaultAsync(cancellationToken);

                if (transferDeductionTypeConfig is null)
                {
                    _logger.ValidationFailed("TransferWallet", "Transfer deduction type configuration not found");
                    return Result.Failure("Transfer deduction type configuration not found", 400);
                }

                if (!Enum.TryParse<TransferDeductionType>(transferDeductionTypeConfig.Value, out var transferDeductionTypeFromConfig))
                {
                    _logger.ValidationFailed("TransferWallet", "Invalid transfer deduction type configuration");
                    return Result.Failure("Invalid transfer deduction type configuration", 400);
                }

                transferDeductionType = transferDeductionTypeFromConfig;
            }
            else
            {
                transferDeductionType = request.TransferDeductionType;
            }

            var feeResult = await CalculateFeeAsync(
                tenant.Id,
                WalletOperationType.Transfer,
                senderWallet.WalletTypeId,
                request.CurrencyId,
                request.TotalAmount,
                request.TotalFee,
                cancellationToken);
            if (!feeResult.IsSuccess)
            {
                return Result.Failure(feeResult.Message!, feeResult.StatusCode);
            }

            var fee = feeResult.Data!.RequestedFee;

            // Calculate amounts based on deduction type
            switch (transferDeductionType)
            {
                case TransferDeductionType.DeductFromSender:
                    totalDecrement = request.TotalAmount + fee;
                    totalIncrement = request.TotalAmount;
                    break;
                case TransferDeductionType.DeductFromRecipient:
                    totalDecrement = request.TotalAmount;
                    totalIncrement = request.TotalAmount - fee;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (totalIncrement < 0)
            {
                _logger.BusinessRuleViolation("TransferWallet", "Total fee exceeds transfer amount");
                return Result.Failure("Total fee cannot exceed transfer amount when deducted from recipient", 400);
            }

            // Validate sender has enough balance
            if (senderWallet.AvailableBalance < totalDecrement)
            {
                _logger.InsufficientBalance(senderWallet.Id, totalDecrement, senderWallet.AvailableBalance);
                return Result.Failure("Insufficient balance", 400);
            }

            // Validate transferable balance
            if (totalDecrement > senderWallet.TransferableBalance)
            {
                _logger.BusinessRuleViolation("TransferWallet", "Amount exceeds transferable balance");
                return Result.Failure("Amount exceeds transferable balance", 400);
            }

            // Validate min transfer rule
            if (request.TotalAmount < senderWallet.MinTransferRule)
            {
                _logger.BusinessRuleViolation("TransferWallet", $"Amount {request.TotalAmount} below sender minimum {senderWallet.MinTransferRule}");
                return Result.Failure($"Amount must be at least {senderWallet.MinTransferRule}", 400);
            }

            // Validate max transfer rule
            if (request.TotalAmount > senderWallet.MaxTransferRule)
            {
                _logger.BusinessRuleViolation("TransferWallet", $"Amount {request.TotalAmount} exceeds sender maximum {senderWallet.MaxTransferRule}");
                return Result.Failure($"Amount must not exceed {senderWallet.MaxTransferRule}", 400);
            }

            // Validate bond balance rule
            if (senderWallet.BondBalanceRule.HasValue && request.TotalAmount > senderWallet.BondBalanceRule)
            {
                _logger.BusinessRuleViolation("TransferWallet", $"Amount {request.TotalAmount} exceeds bond balance {senderWallet.BondBalanceRule}");
                return Result.Failure($"Amount must not exceed {senderWallet.BondBalanceRule}", 400);
            }

            // Validate maintaining balance rule
            if (senderWallet.MaintainingBalanceRule.HasValue && senderWallet.Balance - totalDecrement < senderWallet.MaintainingBalanceRule)
            {
                _logger.BusinessRuleViolation("TransferWallet", $"Transfer violates maintaining balance {senderWallet.MaintainingBalanceRule}");
                return Result.Failure($"Balance after transfer must not drop below {senderWallet.MaintainingBalanceRule}", 400);
            }

            // Validate recipient wallet min transfer rule
            if (request.TotalAmount < recipientWallet.MinTransferRule)
            {
                _logger.BusinessRuleViolation("TransferWallet", $"Amount {request.TotalAmount} below recipient minimum {recipientWallet.MinTransferRule}");
                return Result.Failure($"Amount must be at least {recipientWallet.MinTransferRule}", 400);
            }

            // Validate recipient wallet max transfer rule
            if (request.TotalAmount > recipientWallet.MaxTransferRule)
            {
                _logger.BusinessRuleViolation("TransferWallet", $"Amount {request.TotalAmount} exceeds recipient maximum {recipientWallet.MaxTransferRule}");
                return Result.Failure($"Amount must not exceed {recipientWallet.MaxTransferRule}", 400);
            }

            var referenceNumber = CreateReferenceNumber(request);
            var senderTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = senderWallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = transferDeductionType is TransferDeductionType.DeductFromSender ? fee : 0,
                Remarks = request.Remarks,
                Description = $"Transferred to {MaskFullName(recipientCredential.IdentityInfo?.FullName)}",
                TransactionType = TransactionType.Debit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            var recipientTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CredentialId = request.RecipientCredentialId,
                WalletId = recipientWallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = transferDeductionType is TransferDeductionType.DeductFromRecipient ? fee : 0,
                Remarks = request.Remarks,
                Description = $"Received from {MaskFullName(senderCredential.IdentityInfo?.FullName)}",
                TransactionType = TransactionType.Credit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            // Add tenant IDs to line items
            foreach (var lineItem in request.LineItems)
            {
                lineItem.TenantId = tenant.Id;
            }

            // Create WalletTransfer entity
            var walletTransfer = new WalletTransfer
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                SenderTransactionId = senderTransaction.Id,
                RecipientTransactionId = recipientTransaction.Id,
                SenderTransaction = senderTransaction,
                RecipientTransaction = recipientTransaction,
                LineItems = request.LineItems,
                TransactionPurpose = request.TransactionPurpose,
                TransactionFee = fee
            };

            var postings = new List<WalletLedgerPostingRequest>
            {
                new()
                {
                    WalletId = senderWallet.Id,
                    WalletTransaction = senderTransaction,
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = request.OnHold ? WalletBalanceBucket.DebitHold : WalletBalanceBucket.Available,
                    EntryKind = request.OnHold ? WalletLedgerEntryKind.Hold : WalletLedgerEntryKind.Principal,
                    Amount = totalDecrement,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = senderWallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    Description = "Transfer debit"
                }
            };

            if (totalIncrement > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    WalletId = recipientWallet.Id,
                    WalletTransaction = recipientTransaction,
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = request.OnHold ? WalletBalanceBucket.CreditHold : WalletBalanceBucket.Available,
                    EntryKind = request.OnHold ? WalletLedgerEntryKind.Hold : WalletLedgerEntryKind.Principal,
                    Amount = totalIncrement,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = recipientWallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    Description = "Transfer credit"
                });
            }

            if (fee > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.Fee,
                    EntryKind = WalletLedgerEntryKind.Fee,
                    Amount = fee,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = senderWallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    CounterpartyType = "platform-fee",
                    CounterpartyReference = "wallets",
                    Description = "Transfer platform fee"
                });
            }

            var ledgerResult = await _ledgerService.ExecuteAsync(
                new WalletLedgerExecutionRequest
                {
                    TenantId = tenant.Id,
                    OperationType = WalletOperationType.Transfer,
                    ActorCredentialId = request.CredentialId,
                    IdempotencyKey = request.IdempotencyKey,
                    ReferenceNumber = referenceNumber,
                    Reason = request.Remarks,
                    RequestedFee = request.TotalFee,
                    CalculatedFee = feeResult.Data.CalculatedFee,
                    ApprovalId = request.ApprovalId,
                    Postings = postings,
                    ReadModels = [senderTransaction, recipientTransaction, walletTransfer]
                },
                cancellationToken);

            if (!ledgerResult.IsSuccess)
            {
                return Result.Failure(ledgerResult.Message ?? "Wallet ledger operation failed", ledgerResult.StatusCode);
            }

            if (ledgerResult.Data?.AlreadyProcessed == true)
            {
                return Result.Success("Transaction already processed");
            }

            var senderRunningBalance = ledgerResult.Data?.Wallets.TryGetValue(senderWallet.Id, out var senderBalanceResult) == true
                ? senderBalanceResult.Balance
                : senderWallet.Balance;
            var recipientRunningBalance = ledgerResult.Data?.Wallets.TryGetValue(recipientWallet.Id, out var recipientBalanceResult) == true
                ? recipientBalanceResult.Balance
                : recipientWallet.Balance;

            _logger.WalletTransfer(senderWallet.Id, recipientWallet.Id, request.TotalAmount, "Primary");

            await _eventPublisher.PublishAsync(new TransactionCompletedEvent
            {
                EventType = nameof(TransactionCompletedEvent),
                WalletId = senderWallet.Id,
                CredentialId = request.CredentialId,
                TenantId = tenant.Id,
                Amount = request.TotalAmount,
                TransactionType = "Debit",
                ReferenceNumber = senderTransaction.ReferenceNumber,
                RunningBalance = senderRunningBalance
            });

            await _eventPublisher.PublishAsync(new TransactionCompletedEvent
            {
                EventType = nameof(TransactionCompletedEvent),
                WalletId = recipientWallet.Id,
                CredentialId = request.RecipientCredentialId,
                TenantId = tenant.Id,
                Amount = request.TotalAmount,
                TransactionType = "Credit",
                ReferenceNumber = recipientTransaction.ReferenceNumber,
                RunningBalance = recipientRunningBalance
            });

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("TransferWallet", "Wallet", Guid.Empty, ex.Message, ex);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ConvertWalletAsync(
        ConvertWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var tenantResult = ResolveTrustedTenantId(request, request.CredentialId);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsTransfers, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);

            // Validate amount and fees
            if (request.TotalAmount <= 0 || request.Fee < 0)
            {
                _logger.ValidationFailed("ConvertWallet", "Invalid amount or fee");
                return Result.Failure("Invalid amount or fee", 400);
            }

            // Validate wallet type IDs
            if (request.SourceWalletTypeId == Guid.Empty || request.TargetWalletTypeId == Guid.Empty)
            {
                _logger.ValidationFailed("ConvertWallet", "Source and target wallet type IDs are required");
                return Result.Failure("Source and target wallet type IDs are required", 400);
            }

            // Fetch source wallet
            var sourceWallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Include(x => x.WalletType)
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => !x.IsDeleted)
                .Where(x => x.CredentialId == request.CredentialId)
                .Where(x => x.WalletTypeId == request.SourceWalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceWallet == null)
            {
                _logger.EntityNotFound("SourceWallet", Guid.Empty);
                return Result.NotFound("Source wallet not found");
            }

            var statusCheck = CheckWalletStatus(sourceWallet, "ConvertWallet");
            if (statusCheck is not null) return statusCheck;

            // Fetch or create target wallet
            var targetWallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Include(x => x.WalletType)
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => !x.IsDeleted)
                .Where(x => x.CredentialId == request.CredentialId)
                .Where(x => x.WalletTypeId == request.TargetWalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (targetWallet == null)
            {
                var createResult = await CreateWalletAsync(
                    request.CredentialId,
                    request.TargetWalletTypeId,
                    0,
                    tenant.Id,
                    cancellationToken);

                if (!createResult.IsSuccess)
                {
                    _logger.OperationFailed("AutoCreateTargetWallet", "Wallet", Guid.Empty, "Target wallet creation failed during conversion");
                    return Result.Failure("Target wallet could not be created", 500);
                }

                targetWallet = createResult.Data;
                if (targetWallet is null)
                {
                    _logger.OperationFailed("AutoCreateTargetWallet", "Wallet", Guid.Empty, "Target wallet creation returned no wallet during conversion");
                    return Result.Failure("Target wallet could not be created", 500);
                }
            }

            var targetStatusCheck = CheckWalletStatus(targetWallet, "ConvertWallet");
            if (targetStatusCheck is not null) return targetStatusCheck;

            // Calculate deduction amounts
            decimal totalDecrement;
            decimal totalIncrement;
            TransferDeductionType transferDeductionType;

            if (request.TransferDeductionType == TransferDeductionType.Default)
            {
                // Fetch config from registry
                var transferDeductionTypeConfig = await _dataContext.Query<RegistryConfiguration>()
                    .Where(x => x.TenantId == tenant.Id)
                    .Where(x => x.Key == "Settings:Wallet:Convert:DeductionType")
                    .FirstOrDefaultAsync(cancellationToken);

                if (transferDeductionTypeConfig is null)
                {
                    _logger.ValidationFailed("ConvertWallet", "Transfer deduction type configuration not found");
                    return Result.Failure("Transfer deduction type configuration not found", 400);
                }

                if (!Enum.TryParse<TransferDeductionType>(transferDeductionTypeConfig.Value, out var transferDeductionTypeFromConfig))
                {
                    _logger.ValidationFailed("ConvertWallet", "Invalid transfer deduction type configuration");
                    return Result.Failure("Invalid transfer deduction type configuration", 400);
                }

                transferDeductionType = transferDeductionTypeFromConfig;
            }
            else
            {
                transferDeductionType = request.TransferDeductionType;
            }

            var feeResult = await CalculateFeeAsync(
                tenant.Id,
                WalletOperationType.Conversion,
                sourceWallet.WalletTypeId,
                request.CurrencyId,
                request.TotalAmount,
                request.TotalFee,
                cancellationToken);
            if (!feeResult.IsSuccess)
            {
                return Result.Failure(feeResult.Message!, feeResult.StatusCode);
            }

            var fee = feeResult.Data!.RequestedFee;

            // Calculate amounts based on deduction type
            switch (transferDeductionType)
            {
                case TransferDeductionType.DeductFromSender:
                    totalDecrement = request.TotalAmount + fee;
                    totalIncrement = request.TotalAmount;
                    break;
                case TransferDeductionType.DeductFromRecipient:
                    totalDecrement = request.TotalAmount;
                    totalIncrement = request.TotalAmount - fee;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (totalIncrement < 0)
            {
                _logger.BusinessRuleViolation("ConvertWallet", "Total fee exceeds conversion amount");
                return Result.Failure("Total fee cannot exceed conversion amount when deducted from recipient", 400);
            }

            // Validate source wallet has enough balance
            if (sourceWallet.AvailableBalance < totalDecrement)
            {
                _logger.InsufficientBalance(sourceWallet.Id, totalDecrement, sourceWallet.AvailableBalance);
                return Result.Failure("Insufficient balance", 400);
            }

            if (totalDecrement > sourceWallet.TransferableBalance)
            {
                _logger.BusinessRuleViolation("ConvertWallet", "Amount exceeds transferable balance");
                return Result.Failure("Amount exceeds transferable balance", 400);
            }

            if (sourceWallet.MaintainingBalanceRule.HasValue &&
                sourceWallet.Balance - totalDecrement < sourceWallet.MaintainingBalanceRule)
            {
                _logger.BusinessRuleViolation("ConvertWallet", $"Conversion violates maintaining balance {sourceWallet.MaintainingBalanceRule}");
                return Result.Failure($"Balance after conversion must not drop below {sourceWallet.MaintainingBalanceRule}", 400);
            }

            // Create transaction records
            var referenceNumber = CreateReferenceNumber(request);
            var sourceTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = sourceWallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = transferDeductionType is TransferDeductionType.DeductFromSender ? fee : 0,
                Remarks = request.Remarks,
                Description = $"Converted to {targetWallet.WalletType?.Name}",
                TransactionType = TransactionType.Debit,
                Held = false,
                Released = true,
                ReferenceNumber = referenceNumber
            };

            var targetTransaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = targetWallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = transferDeductionType is TransferDeductionType.DeductFromRecipient ? fee : 0,
                Remarks = request.Remarks,
                Description = $"Converted from {sourceWallet.WalletType?.Name}",
                TransactionType = TransactionType.Credit,
                Held = false,
                Released = true,
                ReferenceNumber = referenceNumber
            };

            var postings = new List<WalletLedgerPostingRequest>
            {
                new()
                {
                    WalletId = sourceWallet.Id,
                    WalletTransaction = sourceTransaction,
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = WalletLedgerEntryKind.Principal,
                    Amount = totalDecrement,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = sourceWallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    Description = "Conversion source debit"
                }
            };

            if (totalIncrement > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    WalletId = targetWallet.Id,
                    WalletTransaction = targetTransaction,
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = WalletLedgerEntryKind.Principal,
                    Amount = totalIncrement,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = targetWallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    Description = "Conversion target credit"
                });
            }
            else
            {
                targetTransaction.PreviousBalance = targetWallet.Balance;
                targetTransaction.PreviousTotalBalance = targetWallet.TotalBalance ?? targetWallet.Balance;
                targetTransaction.PreviousDebitOnHoldBalance = targetWallet.DebitOnHoldBalance;
                targetTransaction.PreviousCreditOnHoldBalance = targetWallet.CreditOnHoldBalance;
                targetTransaction.RunningBalance = targetWallet.Balance;
                targetTransaction.RunningTotalBalance = targetWallet.TotalBalance;
                targetTransaction.RunningAvailableBalance = targetWallet.AvailableBalance;
                targetTransaction.RunningDebitOnHoldBalance = targetWallet.DebitOnHoldBalance;
                targetTransaction.RunningCreditOnHoldBalance = targetWallet.CreditOnHoldBalance;
            }

            if (fee > 0)
            {
                postings.Add(new WalletLedgerPostingRequest
                {
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.Fee,
                    EntryKind = WalletLedgerEntryKind.Fee,
                    Amount = fee,
                    CurrencyId = request.CurrencyId == Guid.Empty ? null : request.CurrencyId,
                    WalletTypeId = sourceWallet.WalletTypeId,
                    ReferenceNumber = referenceNumber,
                    CounterpartyType = "platform-fee",
                    CounterpartyReference = "wallets",
                    Description = "Conversion platform fee"
                });
            }

            var ledgerResult = await _ledgerService.ExecuteAsync(
                new WalletLedgerExecutionRequest
                {
                    TenantId = tenant.Id,
                    OperationType = WalletOperationType.Conversion,
                    ActorCredentialId = request.CredentialId,
                    IdempotencyKey = request.IdempotencyKey,
                    ReferenceNumber = referenceNumber,
                    Reason = request.Remarks,
                    RequestedFee = request.TotalFee,
                    CalculatedFee = feeResult.Data.CalculatedFee,
                    Postings = postings,
                    ReadModels = [sourceTransaction, targetTransaction]
                },
                cancellationToken);

            if (!ledgerResult.IsSuccess)
            {
                return Result.Failure(ledgerResult.Message ?? "Wallet ledger operation failed", ledgerResult.StatusCode);
            }

            if (ledgerResult.Data?.AlreadyProcessed == true)
            {
                return Result.Success("Transaction already processed");
            }

            stopwatch.Stop();
            _logger.OperationCompleted("ConvertWallet", stopwatch.ElapsedMilliseconds);

            var sourceRunningBalance = ledgerResult.Data?.Wallets.TryGetValue(sourceWallet.Id, out var sourceBalanceResult) == true
                ? sourceBalanceResult.Balance
                : sourceWallet.Balance;
            var targetRunningBalance = ledgerResult.Data?.Wallets.TryGetValue(targetWallet.Id, out var targetBalanceResult) == true
                ? targetBalanceResult.Balance
                : targetWallet.Balance;

            await _eventPublisher.PublishAsync(new TransactionCompletedEvent
            {
                EventType = nameof(TransactionCompletedEvent),
                WalletId = sourceWallet.Id,
                CredentialId = request.CredentialId,
                TenantId = tenant.Id,
                Amount = totalDecrement,
                TransactionType = "Debit",
                ReferenceNumber = sourceTransaction.ReferenceNumber,
                RunningBalance = sourceRunningBalance
            });

            await _eventPublisher.PublishAsync(new TransactionCompletedEvent
            {
                EventType = nameof(TransactionCompletedEvent),
                WalletId = targetWallet.Id,
                CredentialId = request.CredentialId,
                TenantId = tenant.Id,
                Amount = totalIncrement,
                TransactionType = "Credit",
                ReferenceNumber = targetTransaction.ReferenceNumber,
                RunningBalance = targetRunningBalance
            });

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("ConvertWallet", "Wallet", Guid.Empty, ex.Message, ex);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ReleaseTransactionAsync(
        ReleaseTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantResult = ResolveTrustedTenantId(request);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsPolicy, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);

            // Fetch the transaction
            var transaction = await _dataContext.Query<WalletTransaction>()
                .IgnoreQueryFilters()
                .Where(t => t.Id == request.Id && t.TenantId == tenant.Id && !t.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (transaction == null)
            {
                _logger.EntityNotFound("WalletTransaction", request.Id);
                return Result.NotFound("Transaction not found");
            }

            if (!transaction.Held)
            {
                _logger.BusinessRuleViolation("ReleaseTransaction", "Transaction is not on hold");
                return Result.Failure("Transaction is not on hold", 400);
            }

            var wallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Where(i => i.Id == transaction.WalletId && i.TenantId == tenant.Id && !i.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet is null)
            {
                _logger.EntityNotFound("Wallet", transaction.WalletId);
                return Result.NotFound("Wallet not found");
            }

            var statusCheck = CheckWalletStatus(wallet, "ReleaseTransaction");
            if (statusCheck is not null) return statusCheck;

            var heldAmount = GetHeldReleaseAmount(transaction);
            if (heldAmount <= 0)
            {
                return Result.Failure("Held transaction amount is invalid", 400);
            }

            var postings = new List<WalletLedgerPostingRequest>();
            if (transaction.TransactionType is TransactionType.Credit)
            {
                if (wallet.CreditOnHoldBalance < heldAmount)
                {
                    return Result.Failure("Held credit balance is insufficient for release", 400);
                }

                postings.Add(new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    WalletTransactionId = transaction.Id,
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.CreditHold,
                    EntryKind = WalletLedgerEntryKind.Hold,
                    Amount = heldAmount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = transaction.ReferenceNumber,
                    Description = "Release held credit"
                });
                postings.Add(new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    WalletTransactionId = transaction.Id,
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = WalletLedgerEntryKind.Principal,
                    Amount = heldAmount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = transaction.ReferenceNumber,
                    Description = "Apply released credit"
                });
            }
            else if (transaction.TransactionType is TransactionType.Debit)
            {
                if (wallet.DebitOnHoldBalance < heldAmount)
                {
                    return Result.Failure("Held debit balance is insufficient for release", 400);
                }

                if (wallet.Balance < heldAmount)
                {
                    return Result.Failure("Insufficient balance to release held debit", 400);
                }

                if (wallet.MaintainingBalanceRule.HasValue &&
                    wallet.Balance - heldAmount < wallet.MaintainingBalanceRule)
                {
                    return Result.Failure($"Balance after release must not drop below {wallet.MaintainingBalanceRule}", 400);
                }

                postings.Add(new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    WalletTransactionId = transaction.Id,
                    Direction = WalletLedgerDirection.Credit,
                    BalanceBucket = WalletBalanceBucket.DebitHold,
                    EntryKind = WalletLedgerEntryKind.Hold,
                    Amount = heldAmount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = transaction.ReferenceNumber,
                    Description = "Release held debit"
                });
                postings.Add(new WalletLedgerPostingRequest
                {
                    WalletId = wallet.Id,
                    WalletTransactionId = transaction.Id,
                    Direction = WalletLedgerDirection.Debit,
                    BalanceBucket = WalletBalanceBucket.Available,
                    EntryKind = WalletLedgerEntryKind.Principal,
                    Amount = heldAmount,
                    WalletTypeId = wallet.WalletTypeId,
                    ReferenceNumber = transaction.ReferenceNumber,
                    Description = "Capture held debit"
                });
            }
            else
            {
                return Result.Failure("Unsupported held transaction type", 400);
            }

            var ledgerResult = await _ledgerService.ExecuteAsync(
                new WalletLedgerExecutionRequest
                {
                    TenantId = tenant.Id,
                    OperationType = WalletOperationType.Release,
                    ActorCredentialId = transaction.CredentialId,
                    IdempotencyKey = $"release:{transaction.Id}",
                    ReferenceNumber = transaction.ReferenceNumber,
                    Reason = request.Id.ToString(),
                    Postings = postings,
                    TransactionUpdates =
                    [
                        new WalletTransactionStateUpdateRequest
                        {
                            Transaction = transaction,
                            WalletId = wallet.Id,
                            Held = false,
                            Released = true
                        }
                    ]
                },
                cancellationToken);

            if (!ledgerResult.IsSuccess)
            {
                return Result.Failure(ledgerResult.Message ?? "Wallet ledger operation failed", ledgerResult.StatusCode);
            }

            _logger.OperationCompleted("ReleaseTransaction", 0);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("ReleaseTransaction", "WalletTransaction", request.Id, ex.Message, ex);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }

    /// <summary>
    /// Masks a full name for privacy, showing only first and last characters of each word.
    /// </summary>
    /// <param name="fullname">The full name to mask</param>
    /// <returns>Masked full name (e.g., "John Doe" becomes "J**n D*e")</returns>
    private static string MaskFullName(string? fullname)
    {
        if (string.IsNullOrWhiteSpace(fullname))
        {
            return "Unknown";
        }

        var names = fullname.Split(' ', StringSplitOptions.RemoveEmptyEntries).AsSpan();
        var maskedNameBuilder = new StringBuilder();

        for (int i = 0; i < names.Length; i++)
        {
            var word = names[i];
            if (word.Length > 1)
            {
                // Append first character
                maskedNameBuilder.Append(word[0]);

                // Append '*' for each middle character
                maskedNameBuilder.Append('*', word.Length - 2);

                // Append last character
                maskedNameBuilder.Append(word[^1]);
            }
            else
            {
                // If the word is a single character, just append it
                maskedNameBuilder.Append(word);
            }

            // Add a space after each word, but avoid it after the last word
            if (i < names.Length - 1)
            {
                maskedNameBuilder.Append(' ');
            }
        }

        return maskedNameBuilder.ToString();
    }

    private Result? CheckWalletStatus(Wallet wallet, string operation)
    {
        return wallet.Status switch
        {
            WalletStatus.Frozen => Result.Failure("Wallet is frozen. No operations allowed.", 403),
            WalletStatus.Suspended => Result.Failure("Wallet is suspended. No operations allowed.", 403),
            WalletStatus.Closed => Result.Failure("Wallet is closed. No operations allowed.", 403),
            _ => null
        };
    }

    private async Task<Wallet?> FindWalletForTransactionAsync(
        Guid tenantId,
        Guid walletId,
        Guid credentialId,
        Guid walletTypeId,
        CancellationToken cancellationToken)
    {
        var query = _dataContext.Query<Wallet>()
            .IgnoreQueryFilters()
            .Where(w => w.TenantId == tenantId && !w.IsDeleted);

        if (walletId != Guid.Empty)
        {
            return await query
                .Where(w => w.Id == walletId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (credentialId == Guid.Empty || walletTypeId == Guid.Empty)
        {
            return null;
        }

        return await query
            .Where(w => w.CredentialId == credentialId && w.WalletTypeId == walletTypeId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static Result? CheckWalletRequestTarget(Wallet wallet, Guid credentialId, Guid walletTypeId)
    {
        if (credentialId != Guid.Empty && wallet.CredentialId != credentialId)
        {
            return Result.Failure("Wallet does not belong to the requested credential", 400);
        }

        if (walletTypeId != Guid.Empty && wallet.WalletTypeId != walletTypeId)
        {
            return Result.Failure("Wallet does not match requested wallet type", 400);
        }

        return null;
    }

    private async Task<bool> HasProcessedIdempotencyKey(
        Guid tenantId,
        TransactionRequestBase request,
        CancellationToken cancellationToken) =>
        await _ledgerService.HasProcessedAsync(tenantId, request.IdempotencyKey, cancellationToken);

    private static string CreateReferenceNumber(TransactionRequestBase request)
    {
        if (!string.IsNullOrWhiteSpace(request.ReferenceNumber))
        {
            return request.ReferenceNumber;
        }

        return string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString()
            : request.IdempotencyKey;
    }

    private static decimal GetHeldReleaseAmount(WalletTransaction transaction)
    {
        var amount = Math.Abs(transaction.Amount);

        return transaction.TransactionType switch
        {
            TransactionType.Credit => amount - transaction.TransactionFee,
            TransactionType.Debit => amount + transaction.TransactionFee,
            _ => 0
        };
    }

    private static decimal GetAppliedBalanceDelta(WalletTransaction transaction)
    {
        if (transaction.RunningBalance.HasValue)
        {
            return transaction.RunningBalance.Value - transaction.PreviousBalance;
        }

        var amount = Math.Abs(transaction.Amount);

        return transaction.TransactionType switch
        {
            TransactionType.Credit => amount - transaction.TransactionFee,
            TransactionType.Debit => -(amount + transaction.TransactionFee),
            _ => 0
        };
    }

    private static Result? EnsureWalletCanApplyDebit(Wallet wallet, decimal amount, string operation)
    {
        if (amount <= 0)
        {
            return null;
        }

        if (wallet.AvailableBalance < amount)
        {
            return Result.Failure("Insufficient funds", 400);
        }

        if (wallet.MaintainingBalanceRule.HasValue &&
            wallet.Balance - amount < wallet.MaintainingBalanceRule)
        {
            return Result.Failure($"Balance after {operation} must not drop below {wallet.MaintainingBalanceRule}", 400);
        }

        return null;
    }

    private async Task<bool> HasExistingSingleTransactionReversal(
        Guid tenantId,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        var prefix = $"Reversal of {transactionId}:";

        return await _dataContext.Query<WalletTransaction>()
            .IgnoreQueryFilters()
            .AnyAsync(t =>
                t.TenantId == tenantId &&
                !t.IsDeleted &&
                t.Description != null &&
                t.Description.StartsWith(prefix),
                cancellationToken);
    }

    private async Task<bool> HasExistingTransferReversal(
        Guid tenantId,
        Guid transferId,
        CancellationToken cancellationToken)
    {
        var prefix = $"Reversal of transfer {transferId}:";

        return await _dataContext.Query<WalletTransaction>()
            .IgnoreQueryFilters()
            .AnyAsync(t =>
                t.TenantId == tenantId &&
                !t.IsDeleted &&
                t.Description != null &&
                t.Description.StartsWith(prefix),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> ReverseTransactionAsync(
        ReverseTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantResult = ResolveTrustedTenantId(request);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsPolicy, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);

            if (request.WalletTransferId != Guid.Empty)
            {
                return await ReversePairedTransfer(tenant.Id, request, cancellationToken);
            }

            if (request.TransactionId != Guid.Empty)
            {
                return await ReverseSingleTransaction(tenant.Id, request, cancellationToken);
            }

            return Result.Failure("Either TransactionId or WalletTransferId must be provided", 400);
        }
        catch (Exception ex)
        {
            var id = request.TransactionId != Guid.Empty ? request.TransactionId : request.WalletTransferId;
            _logger.OperationFailed("ReverseTransaction", "WalletTransaction", id, ex.Message, ex);
            return Result.Failure("An error occurred while reversing the transaction", 500);
        }
    }

    private async Task<Result> ReverseSingleTransaction(
        Guid tenantId, ReverseTransactionRequest request, CancellationToken ct)
    {
        var transaction = await _dataContext.Query<WalletTransaction>()
            .IgnoreQueryFilters()
            .Where(t => t.Id == request.TransactionId && t.TenantId == tenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (transaction is null)
        {
            _logger.EntityNotFound("WalletTransaction", request.TransactionId);
            return Result.NotFound("Transaction not found");
        }

        if (transaction.Description?.StartsWith("Reversal of") == true)
        {
            return Result.Failure("Transaction has already been reversed", 400);
        }

        if (transaction.Held)
        {
            return Result.Failure("Cannot reverse a held transaction. Release it first.", 400);
        }

        if (await HasExistingSingleTransactionReversal(tenantId, transaction.Id, ct))
        {
            return Result.Failure("Transaction has already been reversed", 400);
        }

        var wallet = await _dataContext.Query<Wallet>()
            .IgnoreQueryFilters()
            .Where(w => w.Id == transaction.WalletId && w.TenantId == tenantId && !w.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (wallet is null)
        {
            _logger.EntityNotFound("Wallet", transaction.WalletId);
            return Result.NotFound("Wallet not found");
        }

        var statusCheck = CheckWalletStatus(wallet, "ReverseTransaction");
        if (statusCheck is not null) return statusCheck;

        var appliedDelta = GetAppliedBalanceDelta(transaction);
        if (appliedDelta == 0)
        {
            return Result.Failure("Transaction has no reversible balance effect", 400);
        }

        var reversalDelta = -appliedDelta;
        var debitCheck = EnsureWalletCanApplyDebit(
            wallet,
            reversalDelta < 0 ? Math.Abs(reversalDelta) : 0,
            "reversal");
        if (debitCheck is not null) return debitCheck;

        var reversalAmount = Math.Abs(reversalDelta);
        var reversalType = reversalDelta >= 0
            ? TransactionType.Credit
            : TransactionType.Debit;

        var reversalTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CredentialId = transaction.CredentialId,
            WalletId = transaction.WalletId,
            Amount = reversalAmount,
            TransactionType = reversalType,
            TransactionFee = 0,
            ReferenceNumber = transaction.ReferenceNumber,
            Description = $"Reversal of {transaction.Id}: {request.Reason}",
            Remarks = request.Reason
        };

        var postings = new List<WalletLedgerPostingRequest>();
        if (reversalType is TransactionType.Credit)
        {
            postings.Add(new WalletLedgerPostingRequest
            {
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = WalletBalanceBucket.External,
                EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                Amount = reversalAmount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = transaction.ReferenceNumber,
                CounterpartyType = "reversal-counterparty",
                CounterpartyReference = transaction.Id.ToString(),
                Description = "Reversal counterparty debit"
            });
            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = wallet.Id,
                WalletTransaction = reversalTransaction,
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.Available,
                EntryKind = WalletLedgerEntryKind.Reversal,
                Amount = reversalAmount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = transaction.ReferenceNumber,
                Description = "Reversal credit"
            });
        }
        else
        {
            postings.Add(new WalletLedgerPostingRequest
            {
                WalletId = wallet.Id,
                WalletTransaction = reversalTransaction,
                Direction = WalletLedgerDirection.Debit,
                BalanceBucket = WalletBalanceBucket.Available,
                EntryKind = WalletLedgerEntryKind.Reversal,
                Amount = reversalAmount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = transaction.ReferenceNumber,
                Description = "Reversal debit"
            });
            postings.Add(new WalletLedgerPostingRequest
            {
                Direction = WalletLedgerDirection.Credit,
                BalanceBucket = WalletBalanceBucket.External,
                EntryKind = WalletLedgerEntryKind.SystemCounterparty,
                Amount = reversalAmount,
                WalletTypeId = wallet.WalletTypeId,
                ReferenceNumber = transaction.ReferenceNumber,
                CounterpartyType = "reversal-counterparty",
                CounterpartyReference = transaction.Id.ToString(),
                Description = "Reversal counterparty credit"
            });
        }

        var ledgerResult = await _ledgerService.ExecuteAsync(
            new WalletLedgerExecutionRequest
            {
                TenantId = tenantId,
                OperationType = WalletOperationType.Reversal,
                ActorCredentialId = transaction.CredentialId,
                IdempotencyKey = $"reversal:{transaction.Id}",
                ReferenceNumber = transaction.ReferenceNumber,
                Reason = request.Reason,
                ExternalReference = transaction.Id.ToString(),
                ApprovalId = request.ApprovalId,
                Postings = postings,
                ReadModels = [reversalTransaction]
            },
            ct);

        if (!ledgerResult.IsSuccess)
        {
            return Result.Failure(ledgerResult.Message ?? "Wallet ledger operation failed", ledgerResult.StatusCode);
        }

        _logger.EntityCreated("ReversalTransaction", reversalTransaction.Id);

        await _eventPublisher.PublishAsync(new TransactionReversedEvent
        {
            EventType = nameof(TransactionReversedEvent),
            WalletId = wallet.Id,
            CredentialId = transaction.CredentialId,
            TenantId = tenantId,
            OriginalTransactionId = transaction.Id,
            ReversalTransactionId = reversalTransaction.Id,
            Amount = reversalAmount
        });

        return Result.Success("Transaction reversed successfully");
    }

    private async Task<Result> ReversePairedTransfer(
        Guid tenantId, ReverseTransactionRequest request, CancellationToken ct)
    {
        var transfer = await _dataContext.Query<WalletTransfer>()
            .IgnoreQueryFilters()
            .Where(t => t.Id == request.WalletTransferId && t.TenantId == tenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (transfer is null)
        {
            _logger.EntityNotFound("WalletTransfer", request.WalletTransferId);
            return Result.NotFound("Transfer not found");
        }

        if (transfer.TransactionPurpose == TransactionPurpose.Reversal)
        {
            return Result.Failure("This transfer is already a reversal", 400);
        }

        if (await HasExistingTransferReversal(tenantId, transfer.Id, ct))
        {
            return Result.Failure("Transfer has already been reversed", 400);
        }

        var senderTx = await _dataContext.Query<WalletTransaction>()
            .IgnoreQueryFilters()
            .Where(t => t.Id == transfer.SenderTransactionId && t.TenantId == tenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        var recipientTx = await _dataContext.Query<WalletTransaction>()
            .IgnoreQueryFilters()
            .Where(t => t.Id == transfer.RecipientTransactionId && t.TenantId == tenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (senderTx is null || recipientTx is null)
        {
            return Result.Failure("Transfer transactions not found", 404);
        }

        var senderWallet = await _dataContext.Query<Wallet>()
            .IgnoreQueryFilters()
            .Where(w => w.Id == senderTx.WalletId && w.TenantId == tenantId && !w.IsDeleted)
            .FirstOrDefaultAsync(ct);

        var recipientWallet = await _dataContext.Query<Wallet>()
            .IgnoreQueryFilters()
            .Where(w => w.Id == recipientTx.WalletId && w.TenantId == tenantId && !w.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (senderWallet is null || recipientWallet is null)
        {
            return Result.Failure("Wallets not found", 404);
        }

        var senderCheck = CheckWalletStatus(senderWallet, "ReverseTransfer");
        if (senderCheck is not null) return senderCheck;

        var recipientCheck = CheckWalletStatus(recipientWallet, "ReverseTransfer");
        if (recipientCheck is not null) return recipientCheck;

        var senderReversalDelta = -GetAppliedBalanceDelta(senderTx);
        var recipientReversalDelta = -GetAppliedBalanceDelta(recipientTx);

        if (senderReversalDelta == 0 || recipientReversalDelta == 0)
        {
            return Result.Failure("Transfer has no reversible balance effect", 400);
        }

        var senderDebitCheck = EnsureWalletCanApplyDebit(
            senderWallet,
            senderReversalDelta < 0 ? Math.Abs(senderReversalDelta) : 0,
            "transfer reversal");
        if (senderDebitCheck is not null) return senderDebitCheck;

        var recipientDebitCheck = EnsureWalletCanApplyDebit(
            recipientWallet,
            recipientReversalDelta < 0 ? Math.Abs(recipientReversalDelta) : 0,
            "transfer reversal");
        if (recipientDebitCheck is not null) return recipientDebitCheck;

        // Reverse sender: was Debit → now Credit (money back)
        var senderReversalAmount = Math.Abs(senderReversalDelta);

        var senderReversalTx = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CredentialId = senderTx.CredentialId,
            WalletId = senderTx.WalletId,
            Amount = senderReversalAmount,
            TransactionType = senderReversalDelta >= 0 ? TransactionType.Credit : TransactionType.Debit,
            TransactionFee = 0,
            ReferenceNumber = senderTx.ReferenceNumber,
            Description = $"Reversal of transfer {transfer.Id}: {request.Reason}",
            Remarks = request.Reason
        };

        // Reverse recipient: was Credit → now Debit (money taken back)
        var recipientReversalAmount = Math.Abs(recipientReversalDelta);

        var recipientReversalTx = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CredentialId = recipientTx.CredentialId,
            WalletId = recipientTx.WalletId,
            Amount = recipientReversalAmount,
            TransactionType = recipientReversalDelta >= 0 ? TransactionType.Credit : TransactionType.Debit,
            TransactionFee = 0,
            ReferenceNumber = recipientTx.ReferenceNumber,
            Description = $"Reversal of transfer {transfer.Id}: {request.Reason}",
            Remarks = request.Reason
        };

        var reversalTransfer = new WalletTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransactionPurpose = TransactionPurpose.Reversal,
            SenderTransactionId = senderReversalTx.Id,
            RecipientTransactionId = recipientReversalTx.Id,
            SenderTransaction = senderReversalTx,
            RecipientTransaction = recipientReversalTx,
            TransactionFee = 0
        };

        var postings = new List<WalletLedgerPostingRequest>();
        AddReversalPosting(postings, senderWallet, senderReversalTx, senderReversalDelta, senderTx.ReferenceNumber);
        AddReversalPosting(postings, recipientWallet, recipientReversalTx, recipientReversalDelta, recipientTx.ReferenceNumber);
        BalanceNonWalletReversalPostings(postings, transfer.Id, senderTx.ReferenceNumber);

        var ledgerResult = await _ledgerService.ExecuteAsync(
            new WalletLedgerExecutionRequest
            {
                TenantId = tenantId,
                OperationType = WalletOperationType.Reversal,
                ActorCredentialId = senderTx.CredentialId,
                IdempotencyKey = $"reversal:transfer:{transfer.Id}",
                ReferenceNumber = senderTx.ReferenceNumber,
                Reason = request.Reason,
                ExternalReference = transfer.Id.ToString(),
                ApprovalId = request.ApprovalId,
                Postings = postings,
                ReadModels = [senderReversalTx, recipientReversalTx, reversalTransfer]
            },
            ct);

        if (!ledgerResult.IsSuccess)
        {
            return Result.Failure(ledgerResult.Message ?? "Wallet ledger operation failed", ledgerResult.StatusCode);
        }

        _logger.EntityCreated("ReversalTransfer", reversalTransfer.Id);

        await _eventPublisher.PublishAsync(new TransactionReversedEvent
        {
            EventType = nameof(TransactionReversedEvent),
            WalletId = senderWallet.Id,
            CredentialId = senderTx.CredentialId,
            TenantId = tenantId,
            OriginalTransactionId = senderTx.Id,
            ReversalTransactionId = senderReversalTx.Id,
            Amount = senderReversalAmount
        });

        await _eventPublisher.PublishAsync(new TransactionReversedEvent
        {
            EventType = nameof(TransactionReversedEvent),
            WalletId = recipientWallet.Id,
            CredentialId = recipientTx.CredentialId,
            TenantId = tenantId,
            OriginalTransactionId = recipientTx.Id,
            ReversalTransactionId = recipientReversalTx.Id,
            Amount = recipientReversalAmount
        });

        return Result.Success("Transfer reversed successfully");
    }

    private static void AddReversalPosting(
        List<WalletLedgerPostingRequest> postings,
        Wallet wallet,
        WalletTransaction transaction,
        decimal balanceDelta,
        string? referenceNumber)
    {
        var amount = Math.Abs(balanceDelta);
        postings.Add(new WalletLedgerPostingRequest
        {
            WalletId = wallet.Id,
            WalletTransaction = transaction,
            Direction = balanceDelta >= 0 ? WalletLedgerDirection.Credit : WalletLedgerDirection.Debit,
            BalanceBucket = WalletBalanceBucket.Available,
            EntryKind = WalletLedgerEntryKind.Reversal,
            Amount = amount,
            WalletTypeId = wallet.WalletTypeId,
            ReferenceNumber = referenceNumber,
            Description = balanceDelta >= 0 ? "Transfer reversal credit" : "Transfer reversal debit"
        });
    }

    private static void BalanceNonWalletReversalPostings(
        List<WalletLedgerPostingRequest> postings,
        Guid transferId,
        string? referenceNumber)
    {
        var debitTotal = postings
            .Where(static p => p.Direction == WalletLedgerDirection.Debit)
            .Sum(static p => p.Amount);
        var creditTotal = postings
            .Where(static p => p.Direction == WalletLedgerDirection.Credit)
            .Sum(static p => p.Amount);

        if (debitTotal == creditTotal)
        {
            return;
        }

        postings.Add(new WalletLedgerPostingRequest
        {
            Direction = debitTotal < creditTotal ? WalletLedgerDirection.Debit : WalletLedgerDirection.Credit,
            BalanceBucket = WalletBalanceBucket.External,
            EntryKind = WalletLedgerEntryKind.SystemCounterparty,
            Amount = Math.Abs(creditTotal - debitTotal),
            ReferenceNumber = referenceNumber,
            CounterpartyType = "transfer-reversal-counterparty",
            CounterpartyReference = transferId.ToString(),
            Description = "Transfer reversal balancing entry"
        });
    }

    /// <inheritdoc />
    public async Task<Result> FreezeWalletAsync(
        FreezeWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantResult = ResolveTrustedTenantId(request);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsPolicy, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);

            var wallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Where(w => w.Id == request.WalletId && w.TenantId == tenant.Id && !w.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet is null)
            {
                _logger.EntityNotFound("Wallet", request.WalletId);
                return Result.NotFound("Wallet not found");
            }

            if (wallet.Status == WalletStatus.Frozen)
                return Result.Failure("Wallet is already frozen", 400);

            if (wallet.Status == WalletStatus.Closed)
                return Result.Failure("Cannot freeze a closed wallet", 400);

            var approvalCheck = await ValidateApprovedWalletActionAsync(
                tenant.Id,
                wallet.Id,
                WalletOperationType.Freeze,
                request.ApprovalId,
                cancellationToken);
            if (!approvalCheck.IsSuccess) return approvalCheck;

            wallet.Status = WalletStatus.Frozen;
            wallet.ModifiedAt = DateTime.UtcNow;
            _dataContext.Update(wallet);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.EntityUpdated("Wallet", wallet.Id);

            await _eventPublisher.PublishAsync(new WalletFrozenEvent
            {
                EventType = nameof(WalletFrozenEvent),
                WalletId = wallet.Id,
                CredentialId = wallet.CredentialId,
                TenantId = tenant.Id,
                Reason = request.Reason
            });

            return Result.Success("Wallet frozen successfully");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("FreezeWallet", "Wallet", request.WalletId, ex.Message, ex);
            return Result.Failure("An error occurred while freezing the wallet", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> UnfreezeWalletAsync(
        UnfreezeWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantResult = ResolveTrustedTenantId(request);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsPolicy, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);

            var wallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Where(w => w.Id == request.WalletId && w.TenantId == tenant.Id && !w.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet is null)
            {
                _logger.EntityNotFound("Wallet", request.WalletId);
                return Result.NotFound("Wallet not found");
            }

            if (wallet.Status != WalletStatus.Frozen)
                return Result.Failure("Wallet is not frozen", 400);

            var approvalCheck = await ValidateApprovedWalletActionAsync(
                tenant.Id,
                wallet.Id,
                WalletOperationType.Unfreeze,
                request.ApprovalId,
                cancellationToken);
            if (!approvalCheck.IsSuccess) return approvalCheck;

            wallet.Status = WalletStatus.Active;
            wallet.ModifiedAt = DateTime.UtcNow;
            _dataContext.Update(wallet);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.EntityUpdated("Wallet", wallet.Id);

            await _eventPublisher.PublishAsync(new WalletUnfrozenEvent
            {
                EventType = nameof(WalletUnfrozenEvent),
                WalletId = wallet.Id,
                CredentialId = wallet.CredentialId,
                TenantId = tenant.Id
            });

            return Result.Success("Wallet unfrozen successfully");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("UnfreezeWallet", "Wallet", request.WalletId, ex.Message, ex);
            return Result.Failure("An error occurred while unfreezing the wallet", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> CloseWalletAsync(
        CloseWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantResult = ResolveTrustedTenantId(request);
            if (!tenantResult.IsSuccess) return Result.Failure(tenantResult.Message!, tenantResult.StatusCode);
            var feature = await _featureGateService.EnsureEnabledAsync(tenantResult.Data, TenantModuleFeatureKeys.WalletsPolicy, cancellationToken);
            if (!feature.IsSuccess) return Result.Failure(feature.Message!, feature.StatusCode);
            var tenant = await _tenantService.GetTenant(tenantResult.Data);

            var wallet = await _dataContext.Query<Wallet>()
                .IgnoreQueryFilters()
                .Where(w => w.Id == request.WalletId && w.TenantId == tenant.Id && !w.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet is null)
            {
                _logger.EntityNotFound("Wallet", request.WalletId);
                return Result.NotFound("Wallet not found");
            }

            if (wallet.Status == WalletStatus.Closed)
                return Result.Failure("Wallet is already closed", 400);

            if (wallet.Balance != 0)
                return Result.Failure("Cannot close wallet with a remaining balance", 400);

            if (wallet.DebitOnHoldBalance != 0 || wallet.CreditOnHoldBalance != 0)
                return Result.Failure("Cannot close wallet with held funds", 400);

            var approvalCheck = await ValidateApprovedWalletActionAsync(
                tenant.Id,
                wallet.Id,
                WalletOperationType.Close,
                request.ApprovalId,
                cancellationToken);
            if (!approvalCheck.IsSuccess) return approvalCheck;

            wallet.Status = WalletStatus.Closed;
            wallet.ModifiedAt = DateTime.UtcNow;
            _dataContext.Update(wallet);
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.EntityUpdated("Wallet", wallet.Id);

            await _eventPublisher.PublishAsync(new WalletClosedEvent
            {
                EventType = nameof(WalletClosedEvent),
                WalletId = wallet.Id,
                CredentialId = wallet.CredentialId,
                TenantId = tenant.Id,
                Reason = request.Reason
            });

            return Result.Success("Wallet closed successfully");
        }
        catch (Exception ex)
        {
            _logger.OperationFailed("CloseWallet", "Wallet", request.WalletId, ex.Message, ex);
            return Result.Failure("An error occurred while closing the wallet", 500);
        }
    }

    private Result<Guid> ResolveTrustedTenantId(RequestBase request, Guid? requestCredentialId = null)
    {
        var contextResult = _contextResolver.Resolve(request, requestCredentialId);
        return contextResult.IsSuccess
            ? Result<Guid>.Success(contextResult.Data!.TenantId)
            : Result<Guid>.Failure(contextResult.Message!, contextResult.StatusCode);
    }

    private async Task<Result> ValidateApprovedWalletActionAsync(
        Guid tenantId,
        Guid walletId,
        WalletOperationType operationType,
        Guid? approvalId,
        CancellationToken ct)
    {
        if (!approvalId.HasValue)
        {
            return Result.Failure("Wallet action requires maker-checker approval", 409);
        }

        var approval = await _dataContext.Query<WalletApprovalRequest>()
            .IgnoreQueryFilters()
            .Where(x =>
                x.Id == approvalId.Value &&
                x.TenantId == tenantId &&
                !x.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (approval is null)
        {
            return Result.NotFound("Wallet approval was not found");
        }

        if (approval.Status != WalletApprovalStatus.Approved)
        {
            return Result.Failure("Wallet approval must be approved before the action can be completed", 409);
        }

        if (approval.OperationType != operationType)
        {
            return Result.Failure("Wallet approval does not match the requested action", 400);
        }

        if (approval.WalletId.HasValue && approval.WalletId != walletId)
        {
            return Result.Failure("Wallet approval does not match the target wallet", 400);
        }

        if (approval.ApproverCredentialId.HasValue &&
            approval.ApproverCredentialId == approval.RequesterCredentialId)
        {
            return Result.Failure("Wallet approval requires a different approver", 409);
        }

        return Result.Success();
    }

    private Task<Result<WalletFeeCalculation>> CalculateFeeAsync(
        Guid tenantId,
        WalletOperationType operationType,
        Guid? walletTypeId,
        Guid? currencyId,
        decimal amount,
        decimal requestedFee,
        CancellationToken ct) =>
        _feeCalculator.CalculateAsync(
            tenantId,
            operationType,
            walletTypeId,
            currencyId == Guid.Empty ? null : currencyId,
            amount,
            requestedFee,
            ct);
}

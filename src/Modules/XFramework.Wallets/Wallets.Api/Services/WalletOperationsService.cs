using System.Diagnostics;
using System.Text;
using Wallets.Api.Events;
using XFramework.Core.Loggers;
using XFramework.Core.Observability;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
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

    public WalletOperationsService(
        IDataContext dataContext,
        ITenantResolver tenantService,
        IHelperService helperService,
        ILogger<WalletOperationsService> logger,
        IWalletEventPublisher eventPublisher)
    {
        _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _helperService = helperService ?? throw new ArgumentNullException(nameof(helperService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
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

            var walletType = await _dataContext.Query<Wallets.Domain.Shared.Contracts.WalletType>()
                .Where(x => x.Id == walletTypeId)
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
                    .AnyAsync(x => x.AccountNumber == accountNumber, cancellationToken);
            } while (accountNumberExists);

            var wallet = new Wallet
            {
                TenantId = tenant.Id,
                CredentialId = credentialId,
                WalletTypeId = walletType.Id,
                Balance = initialBalance,
                BondBalanceRule = walletType.BondBalanceRule,
                MaintainingBalanceRule = walletType.MaintainingBalanceRule,
                MinTransferRule = walletType.MinTransferRule,
                MaxTransferRule = walletType.MaxTransferRule,
                AccountNumber = accountNumber,
                DebitOnHoldBalance = 0,
                CreditOnHoldBalance = 0,
                TransferableBalance = initialBalance
            };

            _dataContext.Add(wallet);
            await _dataContext.SaveChangesAsync(cancellationToken);

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
                .Where(w => w.TenantId == tenant.Id && w.Id == walletId)
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
                .Where(w => w.TenantId == tenant.Id && w.CredentialId == credentialId)
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
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);
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

            if (await HasProcessedIdempotencyKey(tenant.Id, request, cancellationToken))
            {
                return Result.Success("Transaction already processed");
            }

            // Fetch wallet
            var wallet = request.WalletTypeId != Guid.Empty
                ? await _dataContext.Query<Wallet>()
                    .Where(w => w.TenantId == tenant.Id && w.WalletTypeId == request.WalletTypeId && w.CredentialId == request.CredentialId)
                    .FirstOrDefaultAsync(cancellationToken)
                : await _dataContext.Query<Wallet>()
                    .Where(w => w.TenantId == tenant.Id && w.Id == request.WalletId)
                    .FirstOrDefaultAsync(cancellationToken);

            if (wallet is null)
            {
                // Auto-create wallet if WalletTypeId provided
                if (request.WalletTypeId != Guid.Empty)
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
                }
                else
                {
                    _logger.EntityNotFound("Wallet", request.WalletId);
                    return Result.NotFound("Wallet not found");
                }
            }

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

            // Store previous balances
            var previousBalance = wallet.Balance;
            var previousTotalBalance = wallet.TotalBalance.Value;
            var previousCreditOnHoldBalance = wallet.CreditOnHoldBalance;
            var previousDebitOnHoldBalance = wallet.DebitOnHoldBalance;
            var netCredit = request.TotalAmount - request.TotalFee;

            // Update wallet balance
            if (request.OnHold)
            {
                wallet.CreditOnHoldBalance += netCredit;
            }
            else
            {
                wallet.Balance += netCredit;
                wallet.TransferableBalance += netCredit;
            }

            // Validate maintaining balance rule
            if (wallet.MaintainingBalanceRule.HasValue && wallet.Balance < wallet.MaintainingBalanceRule.Value)
            {
                _logger.BusinessRuleViolation("IncrementWallet", $"Balance {wallet.Balance} below maintaining rule {wallet.MaintainingBalanceRule.Value}");
                return Result.Failure($"Balance after increment must not drop below {wallet.MaintainingBalanceRule.Value}", 400);
            }

            // Create transaction record
            var referenceNumber = CreateReferenceNumber(request);
            var transaction = new WalletTransaction
            {
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = wallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = request.TotalFee,
                PreviousBalance = previousBalance,
                PreviousTotalBalance = previousTotalBalance,
                PreviousDebitOnHoldBalance = previousDebitOnHoldBalance,
                PreviousCreditOnHoldBalance = previousCreditOnHoldBalance,
                RunningBalance = wallet.Balance,
                RunningTotalBalance = wallet.TotalBalance,
                RunningAvailableBalance = wallet.AvailableBalance,
                RunningCreditOnHoldBalance = wallet.CreditOnHoldBalance,
                RunningDebitOnHoldBalance = wallet.DebitOnHoldBalance,
                Remarks = request.Remarks,
                TransactionType = TransactionType.Credit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            _dataContext.Update(wallet);
            _dataContext.Add(transaction);

            await _dataContext.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

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
            activity?.SetTag("wallet.new_balance", wallet.Balance);

            _logger.WalletIncremented(wallet.Id, request.TotalAmount, "Primary", wallet.Balance);
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
                RunningBalance = wallet.Balance
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
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            if (request.TotalAmount <= 0 || request.TotalFee < 0)
            {
                _logger.ValidationFailed("DecrementWallet", "Invalid decrement amount");
                return Result.Failure("Invalid decrement amount", 400);
            }

            if (await HasProcessedIdempotencyKey(tenant.Id, request, cancellationToken))
            {
                return Result.Success("Transaction already processed");
            }

            // Fetch wallet
            var wallet = request.WalletTypeId != Guid.Empty
                ? await _dataContext.Query<Wallet>()
                    .Where(w => w.TenantId == tenant.Id && w.WalletTypeId == request.WalletTypeId && w.CredentialId == request.CredentialId)
                    .FirstOrDefaultAsync(cancellationToken)
                : await _dataContext.Query<Wallet>()
                    .Where(w => w.TenantId == tenant.Id && w.Id == request.WalletId)
                    .FirstOrDefaultAsync(cancellationToken);

            if (wallet == null)
            {
                _logger.EntityNotFound("Wallet", request.WalletId);
                return Result.NotFound("Wallet not found");
            }

            var statusCheck = CheckWalletStatus(wallet, "DecrementWallet");
            if (statusCheck is not null) return statusCheck;

            var totalDebit = request.TotalAmount + request.TotalFee;

            // Check sufficient balance
            if (wallet.AvailableBalance < totalDebit)
            {
                _logger.InsufficientBalance(wallet.Id, totalDebit, wallet.AvailableBalance);
                return Result.Failure("Insufficient funds", 400);
            }

            // Store previous balances
            var previousBalance = wallet.Balance;
            var previousTotalBalance = wallet.TotalBalance.Value;
            var previousDebitOnHoldBalance = wallet.DebitOnHoldBalance;
            var previousCreditOnHoldBalance = wallet.CreditOnHoldBalance;

            // Update wallet balance
            if (request.OnHold)
            {
                wallet.DebitOnHoldBalance += totalDebit;
            }
            else
            {
                wallet.Balance -= totalDebit;
                wallet.TransferableBalance -= totalDebit;
            }

            // Validate maintaining balance rule
            if (wallet.MaintainingBalanceRule.HasValue && wallet.Balance < wallet.MaintainingBalanceRule.Value)
            {
                _logger.BusinessRuleViolation("DecrementWallet", $"Balance {wallet.Balance} below maintaining rule {wallet.MaintainingBalanceRule.Value}");
                return Result.Failure($"Balance after decrement must not drop below {wallet.MaintainingBalanceRule.Value}", 400);
            }

            // Create transaction record
            var referenceNumber = CreateReferenceNumber(request);
            var transaction = new WalletTransaction
            {
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = wallet.Id,
                Amount = -request.TotalAmount, // Negative for debit
                TransactionFee = request.TotalFee,
                PreviousBalance = previousBalance,
                PreviousTotalBalance = previousTotalBalance,
                PreviousDebitOnHoldBalance = previousDebitOnHoldBalance,
                PreviousCreditOnHoldBalance = previousCreditOnHoldBalance,
                RunningBalance = wallet.Balance,
                RunningTotalBalance = wallet.TotalBalance,
                RunningAvailableBalance = wallet.AvailableBalance,
                RunningCreditOnHoldBalance = wallet.CreditOnHoldBalance,
                RunningDebitOnHoldBalance = wallet.DebitOnHoldBalance,
                Remarks = request.Remarks,
                TransactionType = TransactionType.Debit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            _dataContext.Update(wallet);
            _dataContext.Add(transaction);

            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.WalletDecremented(wallet.Id, request.TotalAmount, "Primary", wallet.Balance);
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
                RunningBalance = wallet.Balance
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
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

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

            if (await HasProcessedIdempotencyKey(tenant.Id, request, cancellationToken))
            {
                return Result.Success("Transaction already processed");
            }

            // Fetch sender and recipient wallets
            var senderWallet = await _dataContext.Query<Wallet>()
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => x.CredentialId == request.CredentialId)
                .Where(x => x.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            var recipientWallet = await _dataContext.Query<Wallet>()
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => x.CredentialId == request.RecipientCredentialId)
                .Where(x => x.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            // Fetch user information for masking (direct DB query — shared database in VSA)
            var senderCredential = await _dataContext.Query<IdentityCredential>()
                .Include(c => c.IdentityInfo)
                .Where(c => c.Id == request.CredentialId && c.TenantId == tenant.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var recipientCredential = await _dataContext.Query<IdentityCredential>()
                .Include(c => c.IdentityInfo)
                .Where(c => c.Id == request.RecipientCredentialId && c.TenantId == tenant.Id)
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

            // Calculate amounts based on deduction type
            switch (transferDeductionType)
            {
                case TransferDeductionType.DeductFromSender:
                    totalDecrement = request.TotalAmount + request.TotalFee;
                    totalIncrement = request.TotalAmount;
                    break;
                case TransferDeductionType.DeductFromRecipient:
                    totalDecrement = request.TotalAmount;
                    totalIncrement = request.TotalAmount - request.TotalFee;
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

            // Store previous balances
            var previousSenderBalance = senderWallet.Balance;
            var previousSenderTotalBalance = senderWallet.TotalBalance;
            var previousSenderDebitOnHoldBalance = senderWallet.DebitOnHoldBalance;
            var previousSenderCreditOnHoldBalance = senderWallet.CreditOnHoldBalance;

            var previousRecipientBalance = recipientWallet.Balance;
            var previousRecipientTotalBalance = recipientWallet.TotalBalance;
            var previousRecipientDebitOnHoldBalance = recipientWallet.DebitOnHoldBalance;
            var previousRecipientCreditOnHoldBalance = recipientWallet.CreditOnHoldBalance;

            // Update wallet balances
            if (request.OnHold)
            {
                senderWallet.DebitOnHoldBalance += totalDecrement;
                senderWallet.TransferableBalance -= totalDecrement;

                recipientWallet.CreditOnHoldBalance += totalIncrement;
            }
            else
            {
                senderWallet.Balance -= totalDecrement;
                senderWallet.TransferableBalance -= totalDecrement;

                recipientWallet.Balance += totalIncrement;
                recipientWallet.TransferableBalance += totalIncrement;
            }

            // Create transaction records
            var referenceNumber = CreateReferenceNumber(request);
            var senderTransaction = new WalletTransaction
            {
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = senderWallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = transferDeductionType is TransferDeductionType.DeductFromSender ? request.TotalFee : 0,
                PreviousBalance = previousSenderBalance,
                PreviousTotalBalance = previousSenderTotalBalance.Value,
                PreviousDebitOnHoldBalance = previousSenderDebitOnHoldBalance,
                PreviousCreditOnHoldBalance = previousSenderCreditOnHoldBalance,
                RunningBalance = senderWallet.Balance,
                RunningDebitOnHoldBalance = senderWallet.DebitOnHoldBalance,
                RunningCreditOnHoldBalance = senderWallet.CreditOnHoldBalance,
                RunningTotalBalance = senderWallet.TotalBalance,
                RunningAvailableBalance = senderWallet.AvailableBalance,
                Remarks = request.Remarks,
                Description = $"Transferred to {MaskFullName(recipientCredential.IdentityInfo?.FullName)}",
                TransactionType = TransactionType.Debit,
                Held = request.OnHold,
                Released = !request.OnHold,
                ReferenceNumber = referenceNumber
            };

            var recipientTransaction = new WalletTransaction
            {
                TenantId = tenant.Id,
                CredentialId = request.RecipientCredentialId,
                WalletId = recipientWallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = transferDeductionType is TransferDeductionType.DeductFromRecipient ? request.TotalFee : 0,
                PreviousBalance = previousRecipientBalance,
                PreviousTotalBalance = previousRecipientTotalBalance.Value,
                PreviousDebitOnHoldBalance = previousRecipientDebitOnHoldBalance,
                PreviousCreditOnHoldBalance = previousRecipientCreditOnHoldBalance,
                RunningBalance = recipientWallet.Balance,
                RunningTotalBalance = recipientWallet.TotalBalance,
                RunningAvailableBalance = recipientWallet.AvailableBalance,
                RunningCreditOnHoldBalance = recipientWallet.CreditOnHoldBalance,
                RunningDebitOnHoldBalance = recipientWallet.DebitOnHoldBalance,
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
                TenantId = tenant.Id,
                SenderTransactionId = senderTransaction.Id,
                RecipientTransactionId = recipientTransaction.Id,
                SenderTransaction = senderTransaction,
                RecipientTransaction = recipientTransaction,
                LineItems = request.LineItems,
                TransactionPurpose = request.TransactionPurpose,
                TransactionFee = request.TotalFee
            };

            _dataContext.Update(senderWallet);
            _dataContext.Update(recipientWallet);
            _dataContext.Add(walletTransfer);

            await _dataContext.SaveChangesAsync(cancellationToken);

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
                RunningBalance = senderWallet.Balance
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
                RunningBalance = recipientWallet.Balance
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
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

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

            if (await HasProcessedIdempotencyKey(tenant.Id, request, cancellationToken))
            {
                return Result.Success("Transaction already processed");
            }

            // Fetch source wallet
            var sourceWallet = await _dataContext.Query<Wallet>()
                .Include(x => x.WalletType)
                .Where(x => x.TenantId == tenant.Id)
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
                .Include(x => x.WalletType)
                .Where(x => x.TenantId == tenant.Id)
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
            }

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

            // Calculate amounts based on deduction type
            switch (transferDeductionType)
            {
                case TransferDeductionType.DeductFromSender:
                    totalDecrement = request.TotalAmount + request.TotalFee;
                    totalIncrement = request.TotalAmount;
                    break;
                case TransferDeductionType.DeductFromRecipient:
                    totalDecrement = request.TotalAmount;
                    totalIncrement = request.TotalAmount - request.TotalFee;
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

            // Store previous balances
            var previousSourceBalance = sourceWallet.Balance;
            var previousSourceTotalBalance = sourceWallet.TotalBalance.Value;
            var previousSourceCreditOnHoldBalance = sourceWallet.CreditOnHoldBalance;
            var previousSourceDebitOnHoldBalance = sourceWallet.DebitOnHoldBalance;

            var previousTargetBalance = targetWallet.Balance;
            var previousTargetTotalBalance = targetWallet.TotalBalance.Value;
            var previousTargetCreditOnHoldBalance = targetWallet.CreditOnHoldBalance;
            var previousTargetDebitOnHoldBalance = targetWallet.DebitOnHoldBalance;

            // Update wallet balances
            sourceWallet.Balance -= totalDecrement;
            sourceWallet.TransferableBalance -= totalDecrement;

            targetWallet.Balance += totalIncrement;
            targetWallet.TransferableBalance += totalIncrement;

            // Create transaction records
            var referenceNumber = CreateReferenceNumber(request);
            var sourceTransaction = new WalletTransaction
            {
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = sourceWallet.Id,
                Amount = totalDecrement,
                TransactionFee = transferDeductionType is TransferDeductionType.DeductFromSender ? request.TotalFee : 0,
                PreviousBalance = previousSourceBalance,
                PreviousTotalBalance = previousSourceTotalBalance,
                PreviousDebitOnHoldBalance = previousSourceDebitOnHoldBalance,
                PreviousCreditOnHoldBalance = previousSourceCreditOnHoldBalance,
                RunningBalance = sourceWallet.Balance,
                RunningDebitOnHoldBalance = sourceWallet.DebitOnHoldBalance,
                RunningCreditOnHoldBalance = sourceWallet.CreditOnHoldBalance,
                RunningTotalBalance = sourceWallet.TotalBalance,
                RunningAvailableBalance = sourceWallet.AvailableBalance,
                Remarks = request.Remarks,
                Description = $"Converted to {targetWallet.WalletType?.Name}",
                TransactionType = TransactionType.Debit,
                Held = false,
                Released = true,
                ReferenceNumber = referenceNumber
            };

            var targetTransaction = new WalletTransaction
            {
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = targetWallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = transferDeductionType is TransferDeductionType.DeductFromRecipient ? request.TotalFee : 0,
                PreviousBalance = previousTargetBalance,
                PreviousTotalBalance = previousTargetTotalBalance,
                PreviousDebitOnHoldBalance = previousTargetDebitOnHoldBalance,
                PreviousCreditOnHoldBalance = previousTargetCreditOnHoldBalance,
                RunningBalance = targetWallet.Balance,
                RunningDebitOnHoldBalance = targetWallet.DebitOnHoldBalance,
                RunningCreditOnHoldBalance = targetWallet.CreditOnHoldBalance,
                RunningTotalBalance = targetWallet.TotalBalance,
                RunningAvailableBalance = targetWallet.AvailableBalance,
                Remarks = request.Remarks,
                Description = $"Converted from {sourceWallet.WalletType?.Name}",
                TransactionType = TransactionType.Credit,
                Held = false,
                Released = true,
                ReferenceNumber = referenceNumber
            };

            _dataContext.Update(sourceWallet);
            _dataContext.Update(targetWallet);
            _dataContext.Add(sourceTransaction);
            _dataContext.Add(targetTransaction);

            await _dataContext.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            _logger.OperationCompleted("ConvertWallet", stopwatch.ElapsedMilliseconds);

            await _eventPublisher.PublishAsync(new TransactionCompletedEvent
            {
                EventType = nameof(TransactionCompletedEvent),
                WalletId = sourceWallet.Id,
                CredentialId = request.CredentialId,
                TenantId = tenant.Id,
                Amount = totalDecrement,
                TransactionType = "Debit",
                ReferenceNumber = sourceTransaction.ReferenceNumber,
                RunningBalance = sourceWallet.Balance
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
                RunningBalance = targetWallet.Balance
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
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            // Fetch the transaction
            var transaction = await _dataContext.Query<WalletTransaction>()
                .Where(t => t.Id == request.Id && t.TenantId == tenant.Id)
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

            // Update the transaction
            transaction.Held = false;
            transaction.Released = true;

            // Fetch and update the wallet balance
            var wallet = await _dataContext.Query<Wallet>()
                .Where(i => i.Id == transaction.WalletId)
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet != null)
            {
                var statusCheck = CheckWalletStatus(wallet, "ReleaseTransaction");
                if (statusCheck is not null) return statusCheck;

                var heldAmount = GetHeldReleaseAmount(transaction);
                if (heldAmount <= 0)
                {
                    return Result.Failure("Held transaction amount is invalid", 400);
                }

                var isHeldTransferDebit = transaction.TransactionType is TransactionType.Debit &&
                    await _dataContext.Query<WalletTransfer>()
                        .AnyAsync(t =>
                            t.TenantId == tenant.Id &&
                            t.SenderTransactionId == transaction.Id,
                            cancellationToken);

                // Update balances based on transaction type
                if (transaction.TransactionType is TransactionType.Credit)
                {
                    if (wallet.CreditOnHoldBalance < heldAmount)
                    {
                        return Result.Failure("Held credit balance is insufficient for release", 400);
                    }

                    wallet.Balance += heldAmount;
                    wallet.TransferableBalance += heldAmount;
                    wallet.CreditOnHoldBalance -= heldAmount;
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

                    wallet.Balance -= heldAmount;
                    if (!isHeldTransferDebit)
                    {
                        wallet.TransferableBalance -= heldAmount;
                    }
                    wallet.DebitOnHoldBalance -= heldAmount;
                }

                transaction.RunningBalance = wallet.Balance;
                transaction.RunningTotalBalance = wallet.TotalBalance;
                transaction.RunningAvailableBalance = wallet.AvailableBalance;
                transaction.RunningCreditOnHoldBalance = wallet.CreditOnHoldBalance;
                transaction.RunningDebitOnHoldBalance = wallet.DebitOnHoldBalance;

                _dataContext.Update(transaction);
                _dataContext.Update(wallet);
            }

            await _dataContext.SaveChangesAsync(cancellationToken);

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

    private async Task<bool> HasProcessedIdempotencyKey(
        Guid tenantId,
        TransactionRequestBase request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return false;
        }

        return await _dataContext.Query<WalletTransaction>()
            .AnyAsync(t =>
                t.TenantId == tenantId &&
                t.ReferenceNumber == request.IdempotencyKey,
                cancellationToken);
    }

    private static string CreateReferenceNumber(TransactionRequestBase request)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return request.IdempotencyKey;
        }

        return string.IsNullOrWhiteSpace(request.ReferenceNumber)
            ? Guid.NewGuid().ToString()
            : request.ReferenceNumber;
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
            .AnyAsync(t =>
                t.TenantId == tenantId &&
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
            .AnyAsync(t =>
                t.TenantId == tenantId &&
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
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

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
            .Where(t => t.Id == request.TransactionId && t.TenantId == tenantId)
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
            .Where(w => w.Id == transaction.WalletId)
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

        // Snapshot previous balances
        var previousBalance = wallet.Balance;
        var previousTotalBalance = wallet.TotalBalance!.Value;
        var previousDebitOnHold = wallet.DebitOnHoldBalance;
        var previousCreditOnHold = wallet.CreditOnHoldBalance;

        wallet.Balance += reversalDelta;
        wallet.TransferableBalance += reversalDelta;

        var reversalAmount = Math.Abs(reversalDelta);
        var reversalType = reversalDelta >= 0
            ? TransactionType.Credit
            : TransactionType.Debit;

        var reversalTransaction = new WalletTransaction
        {
            TenantId = tenantId,
            CredentialId = transaction.CredentialId,
            WalletId = transaction.WalletId,
            Amount = reversalAmount,
            TransactionType = reversalType,
            TransactionFee = 0,
            ReferenceNumber = transaction.ReferenceNumber,
            Description = $"Reversal of {transaction.Id}: {request.Reason}",
            Remarks = request.Reason,
            PreviousBalance = previousBalance,
            PreviousTotalBalance = previousTotalBalance,
            PreviousDebitOnHoldBalance = previousDebitOnHold,
            PreviousCreditOnHoldBalance = previousCreditOnHold,
            RunningBalance = wallet.Balance,
            RunningTotalBalance = wallet.TotalBalance,
            RunningAvailableBalance = wallet.AvailableBalance,
            RunningDebitOnHoldBalance = wallet.DebitOnHoldBalance,
            RunningCreditOnHoldBalance = wallet.CreditOnHoldBalance
        };

        _dataContext.Add(reversalTransaction);
        _dataContext.Update(wallet);
        await _dataContext.SaveChangesAsync(ct);

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
            .Where(t => t.Id == request.WalletTransferId && t.TenantId == tenantId)
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
            .Where(t => t.Id == transfer.SenderTransactionId)
            .FirstOrDefaultAsync(ct);

        var recipientTx = await _dataContext.Query<WalletTransaction>()
            .Where(t => t.Id == transfer.RecipientTransactionId)
            .FirstOrDefaultAsync(ct);

        if (senderTx is null || recipientTx is null)
        {
            return Result.Failure("Transfer transactions not found", 404);
        }

        var senderWallet = await _dataContext.Query<Wallet>()
            .Where(w => w.Id == senderTx.WalletId)
            .FirstOrDefaultAsync(ct);

        var recipientWallet = await _dataContext.Query<Wallet>()
            .Where(w => w.Id == recipientTx.WalletId)
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
        var senderPrevBalance = senderWallet.Balance;
        var senderPrevTotal = senderWallet.TotalBalance!.Value;
        var senderPrevDebitOnHold = senderWallet.DebitOnHoldBalance;
        var senderPrevCreditOnHold = senderWallet.CreditOnHoldBalance;

        senderWallet.Balance += senderReversalDelta;
        senderWallet.TransferableBalance += senderReversalDelta;

        var senderReversalAmount = Math.Abs(senderReversalDelta);

        var senderReversalTx = new WalletTransaction
        {
            TenantId = tenantId,
            CredentialId = senderTx.CredentialId,
            WalletId = senderTx.WalletId,
            Amount = senderReversalAmount,
            TransactionType = senderReversalDelta >= 0 ? TransactionType.Credit : TransactionType.Debit,
            TransactionFee = 0,
            ReferenceNumber = senderTx.ReferenceNumber,
            Description = $"Reversal of transfer {transfer.Id}: {request.Reason}",
            Remarks = request.Reason,
            PreviousBalance = senderPrevBalance,
            PreviousTotalBalance = senderPrevTotal,
            PreviousDebitOnHoldBalance = senderPrevDebitOnHold,
            PreviousCreditOnHoldBalance = senderPrevCreditOnHold,
            RunningBalance = senderWallet.Balance,
            RunningTotalBalance = senderWallet.TotalBalance,
            RunningAvailableBalance = senderWallet.AvailableBalance,
            RunningDebitOnHoldBalance = senderWallet.DebitOnHoldBalance,
            RunningCreditOnHoldBalance = senderWallet.CreditOnHoldBalance
        };

        // Reverse recipient: was Credit → now Debit (money taken back)
        var recipientPrevBalance = recipientWallet.Balance;
        var recipientPrevTotal = recipientWallet.TotalBalance!.Value;
        var recipientPrevDebitOnHold = recipientWallet.DebitOnHoldBalance;
        var recipientPrevCreditOnHold = recipientWallet.CreditOnHoldBalance;

        recipientWallet.Balance += recipientReversalDelta;
        recipientWallet.TransferableBalance += recipientReversalDelta;

        var recipientReversalAmount = Math.Abs(recipientReversalDelta);

        var recipientReversalTx = new WalletTransaction
        {
            TenantId = tenantId,
            CredentialId = recipientTx.CredentialId,
            WalletId = recipientTx.WalletId,
            Amount = recipientReversalAmount,
            TransactionType = recipientReversalDelta >= 0 ? TransactionType.Credit : TransactionType.Debit,
            TransactionFee = 0,
            ReferenceNumber = recipientTx.ReferenceNumber,
            Description = $"Reversal of transfer {transfer.Id}: {request.Reason}",
            Remarks = request.Reason,
            PreviousBalance = recipientPrevBalance,
            PreviousTotalBalance = recipientPrevTotal,
            PreviousDebitOnHoldBalance = recipientPrevDebitOnHold,
            PreviousCreditOnHoldBalance = recipientPrevCreditOnHold,
            RunningBalance = recipientWallet.Balance,
            RunningTotalBalance = recipientWallet.TotalBalance,
            RunningAvailableBalance = recipientWallet.AvailableBalance,
            RunningDebitOnHoldBalance = recipientWallet.DebitOnHoldBalance,
            RunningCreditOnHoldBalance = recipientWallet.CreditOnHoldBalance
        };

        _dataContext.Add(senderReversalTx);
        _dataContext.Add(recipientReversalTx);

        // Create reversal WalletTransfer linking the reversal transactions
        var reversalTransfer = new WalletTransfer
        {
            TenantId = tenantId,
            TransactionPurpose = TransactionPurpose.Reversal,
            SenderTransactionId = senderReversalTx.Id,
            RecipientTransactionId = recipientReversalTx.Id,
            TransactionFee = 0
        };

        _dataContext.Add(reversalTransfer);
        _dataContext.Update(senderWallet);
        _dataContext.Update(recipientWallet);
        await _dataContext.SaveChangesAsync(ct);

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

    /// <inheritdoc />
    public async Task<Result> FreezeWalletAsync(
        FreezeWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            var wallet = await _dataContext.Query<Wallet>()
                .Where(w => w.Id == request.WalletId && w.TenantId == tenant.Id)
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
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            var wallet = await _dataContext.Query<Wallet>()
                .Where(w => w.Id == request.WalletId && w.TenantId == tenant.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (wallet is null)
            {
                _logger.EntityNotFound("Wallet", request.WalletId);
                return Result.NotFound("Wallet not found");
            }

            if (wallet.Status != WalletStatus.Frozen)
                return Result.Failure("Wallet is not frozen", 400);

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
}

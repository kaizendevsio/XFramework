using System.Text;
using IdentityServer.Integration.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.Interfaces;
using XFramework.Integration.Abstractions;

namespace Wallets.Core.Services;

/// <summary>
/// Service for managing wallet operations including balance changes, transfers, and conversions.
/// Consolidates all wallet operation logic previously handled by MediatR command handlers.
/// </summary>
public class WalletService : IWalletService
{
    private readonly DbContext _dbContext;
    private readonly ITenantService _tenantService;
    private readonly IIdentityServerServiceWrapper _identityServerService;
    private readonly IHelperService _helperService;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        DbContext dbContext,
        ITenantService tenantService,
        IIdentityServerServiceWrapper identityServerService,
        IHelperService helperService,
        ILogger<WalletService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _identityServerService = identityServerService ?? throw new ArgumentNullException(nameof(identityServerService));
        _helperService = helperService ?? throw new ArgumentNullException(nameof(helperService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            var walletType = await _dbContext.Set<XFramework.Domain.Shared.Contracts.WalletType>()
                .Where(x => x.Id == walletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            if (walletType is null)
            {
                _logger.LogWarning("Error creating wallet. Wallet type not found, wallet type ID {WalletTypeId} credential ID {CredentialId}",
                    walletTypeId, credentialId);
                return Result<Wallet>.Failure("Wallet type not found", 404);
            }

            // Generate unique account number
            string accountNumber;
            bool accountNumberExists;
            do
            {
                accountNumber = $"{_helperService.GenerateRandomNumber(1000_0000_0000, 9999_9999_9999)}";
                accountNumberExists = await _dbContext.Set<Wallet>()
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

            _dbContext.Set<Wallet>().Add(wallet);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Wallet created successfully. WalletId: {WalletId}, CredentialId: {CredentialId}",
                wallet.Id, credentialId);

            return Result<Wallet>.Success(wallet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating wallet for CredentialId: {CredentialId}", credentialId);
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

            var wallet = await _dbContext.Set<Wallet>()
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
            _logger.LogError(ex, "Error retrieving wallet. WalletId: {WalletId}", walletId);
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

            var wallets = await _dbContext.Set<Wallet>()
                .Where(w => w.TenantId == tenant.Id && w.CredentialId == credentialId)
                .ToListAsync(cancellationToken);

            return Result<List<Wallet>>.Success(wallets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving wallets for CredentialId: {CredentialId}", credentialId);
            return Result<List<Wallet>>.Failure("An error occurred while retrieving wallets", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> IncrementBalanceAsync(
        IncrementWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            if (request.TotalAmount <= 0)
            {
                _logger.LogWarning("Invalid increment amount for wallet ID {WalletId}, wallet type ID {WalletTypeId}, credential ID {CredentialId}",
                    request.WalletId, request.WalletTypeId, request.CredentialId);
                return Result.Failure("Invalid increment amount", 400);
            }

            // Fetch wallet
            var wallet = request.WalletTypeId != Guid.Empty
                ? await _dbContext.Set<Wallet>()
                    .Where(w => w.TenantId == tenant.Id && w.WalletTypeId == request.WalletTypeId && w.CredentialId == request.CredentialId)
                    .FirstOrDefaultAsync(cancellationToken)
                : await _dbContext.Set<Wallet>()
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
                        _logger.LogWarning("Error creating wallet for wallet type ID {WalletTypeId}, credential ID {CredentialId}",
                            request.WalletTypeId, request.CredentialId);
                        return Result.Failure("Error creating wallet", 500);
                    }

                    wallet = createResult.Data;
                }
                else
                {
                    _logger.LogWarning("Wallet not found for wallet ID {WalletId}, wallet type ID {WalletTypeId}, credential ID {CredentialId}",
                        request.WalletId, request.WalletTypeId, request.CredentialId);
                    return Result.NotFound("Wallet not found");
                }
            }

            // Validate min transfer rule
            if (wallet.MinTransferRule.HasValue && request.TotalAmount < wallet.MinTransferRule.Value)
            {
                _logger.LogWarning("Amount is below the minimum transferable amount for Wallet ID {WalletId}", wallet.Id);
                return Result.Failure($"Amount must be at least {wallet.MinTransferRule.Value}", 400);
            }

            // Validate max transfer rule
            if (wallet.MaxTransferRule.HasValue && request.TotalAmount > wallet.MaxTransferRule.Value)
            {
                _logger.LogWarning("Amount exceeds the maximum transferable amount for Wallet ID {WalletId}", wallet.Id);
                return Result.Failure($"Amount must not exceed {wallet.MaxTransferRule.Value}", 400);
            }

            // Store previous balances
            var previousBalance = wallet.Balance;
            var previousTotalBalance = wallet.TotalBalance.Value;
            var previousCreditOnHoldBalance = wallet.CreditOnHoldBalance;
            var previousDebitOnHoldBalance = wallet.DebitOnHoldBalance;

            // Update wallet balance
            if (request.OnHold)
            {
                wallet.CreditOnHoldBalance += request.TotalAmount;
            }
            else
            {
                wallet.Balance += request.TotalAmount;
                wallet.TransferableBalance += request.TotalAmount;
            }

            // Validate maintaining balance rule
            if (wallet.MaintainingBalanceRule.HasValue && wallet.Balance < wallet.MaintainingBalanceRule.Value)
            {
                _logger.LogWarning("Incrementing would violate maintaining balance rule for Wallet ID {WalletId}", wallet.Id);
                return Result.Failure($"Balance after increment must not drop below {wallet.MaintainingBalanceRule.Value}", 400);
            }

            // Create transaction record
            var transaction = new WalletTransaction
            {
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = wallet.Id,
                Amount = request.TotalAmount,
                TransactionFee = request.Fee,
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
                ReferenceNumber = string.IsNullOrEmpty(request.ReferenceNumber) ? Guid.NewGuid().ToString() : request.ReferenceNumber
            };

            _dbContext.Set<WalletTransaction>().Add(transaction);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Wallet balance incremented successfully for Wallet ID {WalletId}", wallet.Id);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict occurred while incrementing wallet balance for Wallet ID {WalletId}", request.WalletId);
            return Result.Failure("A concurrency conflict occurred, please try again", 409);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while incrementing wallet balance for Wallet ID {WalletId}", request.WalletId);
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

            if (request.TotalAmount <= 0)
            {
                _logger.LogWarning("Invalid decrement amount for wallet ID {WalletId}, wallet type ID {WalletTypeId}, credential ID {CredentialId}",
                    request.WalletId, request.WalletTypeId, request.CredentialId);
                return Result.Failure("Invalid decrement amount", 400);
            }

            // Fetch wallet
            var wallet = request.WalletTypeId != Guid.Empty
                ? await _dbContext.Set<Wallet>()
                    .Where(w => w.TenantId == tenant.Id && w.WalletTypeId == request.WalletTypeId && w.CredentialId == request.CredentialId)
                    .FirstOrDefaultAsync(cancellationToken)
                : await _dbContext.Set<Wallet>()
                    .Where(w => w.TenantId == tenant.Id && w.Id == request.WalletId)
                    .FirstOrDefaultAsync(cancellationToken);

            if (wallet == null)
            {
                _logger.LogWarning("Wallet not found for wallet ID {WalletId}, wallet type ID {WalletTypeId}, credential ID {CredentialId}",
                    request.WalletId, request.WalletTypeId, request.CredentialId);
                return Result.NotFound("Wallet not found");
            }

            // Check sufficient balance
            if (wallet.AvailableBalance < request.TotalAmount)
            {
                _logger.LogWarning("Insufficient funds for decrement for Wallet ID {WalletId}", wallet.Id);
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
                wallet.DebitOnHoldBalance += request.TotalAmount;
            }
            else
            {
                wallet.Balance -= request.TotalAmount;
                wallet.TransferableBalance -= request.TotalAmount;
            }

            // Validate maintaining balance rule
            if (wallet.MaintainingBalanceRule.HasValue && wallet.Balance < wallet.MaintainingBalanceRule.Value)
            {
                _logger.LogWarning("Decrementing would violate maintaining balance rule for Wallet ID {WalletId}", wallet.Id);
                return Result.Failure($"Balance after decrement must not drop below {wallet.MaintainingBalanceRule.Value}", 400);
            }

            // Create transaction record
            var transaction = new WalletTransaction
            {
                TenantId = tenant.Id,
                CredentialId = request.CredentialId,
                WalletId = wallet.Id,
                Amount = -request.TotalAmount, // Negative for debit
                TransactionFee = request.Fee,
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
                ReferenceNumber = string.IsNullOrEmpty(request.ReferenceNumber) ? Guid.NewGuid().ToString() : request.ReferenceNumber
            };

            _dbContext.Set<WalletTransaction>().Add(transaction);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Wallet balance decremented successfully for Wallet ID {WalletId}", wallet.Id);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict occurred while decrementing wallet balance for Wallet ID {WalletId}", request.WalletId);
            return Result.Failure("A concurrency conflict occurred, please try again", 409);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while decrementing wallet balance for Wallet ID {WalletId}", request.WalletId);
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
                _logger.LogWarning("Invalid amount or fee while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure("Invalid amount or fee", 400);
            }

            // Validate wallet type ID
            if (request.WalletTypeId == Guid.Empty)
            {
                _logger.LogWarning("Wallet type ID is required while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure("Wallet type ID is required", 400);
            }

            // Fetch sender and recipient wallets
            var query = _dbContext.Set<Wallet>();

            var senderWallet = await query
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => x.CredentialId == request.CredentialId)
                .Where(x => x.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            var recipientWallet = await query
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => x.CredentialId == request.RecipientCredentialId)
                .Where(x => x.WalletTypeId == request.WalletTypeId)
                .FirstOrDefaultAsync(cancellationToken);

            // Fetch user information for masking
            var senderUserTask = _identityServerService.IdentityCredential.Get(
                id: request.CredentialId,
                includeNavigations: true,
                includes: [$"{nameof(IdentityCredential.IdentityInfo)}"],
                tenantId: tenant.Id);

            var recipientUserTask = _identityServerService.IdentityCredential.Get(
                id: request.RecipientCredentialId,
                includeNavigations: true,
                includes: [$"{nameof(IdentityCredential.IdentityInfo)}"],
                tenantId: tenant.Id);

            await Task.WhenAll(senderUserTask, recipientUserTask);

            var senderUser = await senderUserTask;
            var recipientUser = await recipientUserTask;

            if (senderUser.HttpStatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning("Sender not found while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.NotFound("Sender not found");
            }

            if (recipientUser.HttpStatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning("Recipient not found while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.NotFound("Recipient not found");
            }

            if (senderWallet == null)
            {
                _logger.LogWarning("Sender wallet not found while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.NotFound("Wallet not found");
            }

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
                    _logger.LogWarning("Recipient wallet not found and could not be created while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                        request.CredentialId, request.RecipientCredentialId);
                    return Result.Failure("Recipient wallet not found and could not be created", 404);
                }

                recipientWallet = createResult.Data;
            }

            // Check for self-transfer
            if (request.CredentialId == request.RecipientCredentialId)
            {
                _logger.LogWarning("Cannot transfer to the same wallet while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure("Cannot transfer to the same wallet", 400);
            }

            // Calculate deduction amounts based on transfer deduction type
            decimal totalDecrement;
            decimal totalIncrement;
            TransferDeductionType transferDeductionType;

            if (request.TransferDeductionType == TransferDeductionType.Default)
            {
                // Fetch config from registry
                var transferDeductionTypeConfig = await _dbContext.Set<RegistryConfiguration>()
                    .Where(x => x.TenantId == tenant.Id)
                    .Where(x => x.Key == "Settings:Wallet:Transfer:DeductionType")
                    .FirstOrDefaultAsync(cancellationToken);

                if (transferDeductionTypeConfig is null)
                {
                    _logger.LogWarning("Transfer deduction type configuration not found while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                        request.CredentialId, request.RecipientCredentialId);
                    return Result.Failure("Transfer deduction type configuration not found", 400);
                }

                if (!Enum.TryParse<TransferDeductionType>(transferDeductionTypeConfig.Value, out var transferDeductionTypeFromConfig))
                {
                    _logger.LogWarning("Invalid transfer deduction type configuration while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                        request.CredentialId, request.RecipientCredentialId);
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

            // Validate sender has enough balance
            if (senderWallet.Balance < totalDecrement)
            {
                _logger.LogWarning("Insufficient balance while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure("Insufficient balance", 400);
            }

            // Validate transferable balance
            if (request.TotalAmount > senderWallet.TransferableBalance)
            {
                _logger.LogWarning("Amount exceeds transferable balance while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure("Amount exceeds transferable balance", 400);
            }

            // Validate min transfer rule
            if (request.TotalAmount < senderWallet.MinTransferRule)
            {
                _logger.LogWarning("Amount is below the minimum transferable amount while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure($"Amount must be at least {senderWallet.MinTransferRule}", 400);
            }

            // Validate max transfer rule
            if (request.TotalAmount > senderWallet.MaxTransferRule)
            {
                _logger.LogWarning("Amount exceeds the maximum transferable amount while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure($"Amount must not exceed {senderWallet.MaxTransferRule}", 400);
            }

            // Validate bond balance rule
            if (senderWallet.BondBalanceRule.HasValue && request.TotalAmount > senderWallet.BondBalanceRule)
            {
                _logger.LogWarning("Amount exceeds the bond balance rule while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure($"Amount must not exceed {senderWallet.BondBalanceRule}", 400);
            }

            // Validate maintaining balance rule
            if (senderWallet.MaintainingBalanceRule.HasValue && senderWallet.Balance - totalDecrement < senderWallet.MaintainingBalanceRule)
            {
                _logger.LogWarning("Amount exceeds the maintaining balance rule while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure($"Balance after transfer must not drop below {senderWallet.MaintainingBalanceRule}", 400);
            }

            // Validate recipient wallet min transfer rule
            if (request.TotalAmount < recipientWallet.MinTransferRule)
            {
                _logger.LogWarning("Amount is below the minimum transferable amount for recipient while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
                return Result.Failure($"Amount must be at least {recipientWallet.MinTransferRule}", 400);
            }

            // Validate recipient wallet max transfer rule
            if (request.TotalAmount > recipientWallet.MaxTransferRule)
            {
                _logger.LogWarning("Amount exceeds the maximum transferable amount for recipient while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                    request.CredentialId, request.RecipientCredentialId);
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
                senderWallet.DebitOnHoldBalance += request.TotalAmount;
                senderWallet.TransferableBalance -= request.TotalAmount;

                recipientWallet.CreditOnHoldBalance += request.TotalAmount;
            }
            else
            {
                senderWallet.Balance -= totalDecrement;
                senderWallet.TransferableBalance -= totalDecrement;

                recipientWallet.Balance += totalIncrement;
                recipientWallet.TransferableBalance += totalIncrement;
            }

            // Create transaction records
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
                Description = $"Transferred to {MaskFullName(recipientUser.Response.IdentityInfo.FullName)}",
                TransactionType = TransactionType.Debit,
                Held = request.OnHold,
                Released = false,
                ReferenceNumber = string.IsNullOrEmpty(request.ReferenceNumber) ? Guid.NewGuid().ToString() : request.ReferenceNumber
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
                Description = $"Received from {MaskFullName(senderUser.Response.IdentityInfo.FullName)}",
                TransactionType = TransactionType.Credit,
                Held = request.OnHold,
                Released = false,
                ReferenceNumber = string.IsNullOrEmpty(request.ReferenceNumber) ? Guid.NewGuid().ToString() : request.ReferenceNumber
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
                TransactionFee = request.Fee
            };

            _dbContext.Set<WalletTransfer>().Add(walletTransfer);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Wallet transfer from {SenderCredentialId} to {RecipientCredentialId} was successful",
                request.CredentialId, request.RecipientCredentialId);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict occurred while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                request.CredentialId, request.RecipientCredentialId);
            return Result.Failure("A concurrency conflict occurred, please try again", 409);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while transferring wallet from {SenderCredentialId} to {RecipientCredentialId}",
                request.CredentialId, request.RecipientCredentialId);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ConvertWalletAsync(
        ConvertWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenant = await _tenantService.GetTenant(request.Metadata.TenantId);

            // Validate amount and fees
            if (request.TotalAmount <= 0 || request.Fee < 0)
            {
                _logger.LogWarning("Invalid amount or fee while converting wallet for {CredentialId}", request.CredentialId);
                return Result.Failure("Invalid amount or fee", 400);
            }

            // Validate wallet type IDs
            if (request.SourceWalletTypeId == Guid.Empty || request.TargetWalletTypeId == Guid.Empty)
            {
                _logger.LogWarning("Source and target wallet type IDs are required for converting wallet for {CredentialId}", request.CredentialId);
                return Result.Failure("Source and target wallet type IDs are required", 400);
            }

            // Fetch source wallet
            var sourceWallet = await _dbContext.Set<Wallet>()
                .Include(x => x.WalletType)
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => x.CredentialId == request.CredentialId)
                .Where(x => x.WalletTypeId == request.SourceWalletTypeId)
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceWallet == null)
            {
                _logger.LogWarning("Source wallet not found for converting wallet for {CredentialId}", request.CredentialId);
                return Result.NotFound("Source wallet not found");
            }

            // Fetch or create target wallet
            var targetWallet = await _dbContext.Set<Wallet>()
                .Include(x => x.WalletType)
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => x.CredentialId == request.CredentialId)
                .Where(x => x.WalletTypeId == request.TargetWalletTypeId)
                .AsSplitQuery()
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
                    _logger.LogWarning("Target wallet could not be created for converting wallet for {CredentialId}", request.CredentialId);
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
                var transferDeductionTypeConfig = await _dbContext.Set<RegistryConfiguration>()
                    .Where(x => x.TenantId == tenant.Id)
                    .Where(x => x.Key == "Settings:Wallet:Convert:DeductionType")
                    .FirstOrDefaultAsync(cancellationToken);

                if (transferDeductionTypeConfig is null)
                {
                    _logger.LogWarning("Transfer deduction type configuration not found for converting wallet for {CredentialId}", request.CredentialId);
                    return Result.Failure("Transfer deduction type configuration not found", 400);
                }

                if (!Enum.TryParse<TransferDeductionType>(transferDeductionTypeConfig.Value, out var transferDeductionTypeFromConfig))
                {
                    _logger.LogWarning("Invalid transfer deduction type configuration for converting wallet for {CredentialId}", request.CredentialId);
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

            // Validate source wallet has enough balance
            if (sourceWallet.Balance < totalDecrement)
            {
                _logger.LogWarning("Insufficient balance in source wallet for converting wallet for {CredentialId}", request.CredentialId);
                return Result.Failure("Insufficient balance", 400);
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
                ReferenceNumber = string.IsNullOrEmpty(request.ReferenceNumber) ? Guid.NewGuid().ToString() : request.ReferenceNumber
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
                ReferenceNumber = string.IsNullOrEmpty(request.ReferenceNumber) ? Guid.NewGuid().ToString() : request.ReferenceNumber
            };

            _dbContext.Set<WalletTransaction>().Add(sourceTransaction);
            _dbContext.Set<WalletTransaction>().Add(targetTransaction);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Wallet type conversion successful for {CredentialId}", request.CredentialId);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict occurred while converting wallet for {CredentialId}", request.CredentialId);
            return Result.Failure("A concurrency conflict occurred, please try again", 409);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while converting wallet for {CredentialId}", request.CredentialId);
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
            var transaction = await _dbContext.Set<WalletTransaction>()
                .FirstOrDefaultAsync(t => t.Id == request.Id && t.TenantId == tenant.Id, cancellationToken);

            if (transaction == null)
            {
                _logger.LogInformation("Transaction with Id {Id} not found", request.Id);
                return Result.NotFound("Transaction not found");
            }

            if (!transaction.Held)
            {
                _logger.LogInformation("Transaction with Id {Id} is not on hold", request.Id);
                return Result.Failure("Transaction is not on hold", 400);
            }

            // Update the transaction
            transaction.Held = false;
            transaction.Released = true;

            // Fetch and update the wallet balance
            var wallet = await _dbContext.Set<Wallet>()
                .FirstOrDefaultAsync(i => i.Id == transaction.WalletId, cancellationToken);

            if (wallet != null)
            {
                // Update balances based on transaction type
                if (transaction.TransactionType is TransactionType.Credit)
                {
                    wallet.Balance += transaction.Amount;
                    wallet.TransferableBalance += transaction.Amount;
                    wallet.CreditOnHoldBalance -= transaction.Amount;
                }
                else if (transaction.TransactionType is TransactionType.Debit)
                {
                    wallet.Balance -= transaction.Amount;
                    wallet.TransferableBalance -= transaction.Amount;
                    wallet.DebitOnHoldBalance -= transaction.Amount;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Transaction with Id {Id} released successfully", request.Id);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "A concurrency conflict occurred while releasing transaction with Id {Id}", request.Id);
            return Result.Failure("A concurrency conflict occurred, please try again", 409);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while releasing transaction with Id {Id}", request.Id);
            return Result.Failure("An error occurred while processing your request", 500);
        }
    }

    /// <summary>
    /// Masks a full name for privacy, showing only first and last characters of each word.
    /// </summary>
    /// <param name="fullname">The full name to mask</param>
    /// <returns>Masked full name (e.g., "John Doe" becomes "J**n D*e")</returns>
    private static string MaskFullName(string fullname)
    {
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
}
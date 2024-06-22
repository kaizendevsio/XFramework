using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Core.Commands.Wallet;

public class ConvertWallet(
    DbContext dbContext,
    ILogger<ConvertWallet> logger,
    ITenantService tenantService,
    IMediator mediator
)
    : IRequestHandler<ConvertWalletRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(ConvertWalletRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        // Validate the amount and fees
        if (request.TotalAmount <= 0 || request.Fee < 0)
        {
            logger.LogWarning("Invalid amount or fee while converting wallet for {CredentialId}", request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid amount or fee" };
        }

        // Check if Source and Target wallet type IDs are provided
        if (request.SourceWalletTypeId == Guid.Empty || request.TargetWalletTypeId == Guid.Empty)
        {
            logger.LogWarning("Source and target wallet type IDs are required for converting wallet for {CredentialId}", request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Source and target wallet type IDs are required" };
        }

        // Fetch the source wallet
        var sourceWallet = await dbContext.Set<XFramework.Domain.Shared.Contracts.Wallet>()
            .Include(x => x.WalletType)
            .Where(x => x.TenantId == tenant.Id)
            .Where(x => x.CredentialId == request.CredentialId)
            .Where(x => x.WalletTypeId == request.SourceWalletTypeId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (sourceWallet == null)
        {
            logger.LogWarning("Source wallet not found for converting wallet for {CredentialId}", request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Source wallet not found" };
        }

        // Fetch or create the target wallet
        var targetWallet = await dbContext.Set<XFramework.Domain.Shared.Contracts.Wallet>()
            .Include(x => x.WalletType)
            .Where(x => x.TenantId == tenant.Id)
            .Where(x => x.CredentialId == request.CredentialId)
            .Where(x => x.WalletTypeId == request.TargetWalletTypeId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

        if (targetWallet == null)
        {
            var createWallet = await mediator.Send(new Create<XFramework.Domain.Shared.Contracts.Wallet>(new()
            {
                CredentialId = request.CredentialId,
                WalletTypeId = request.TargetWalletTypeId,
                Balance = 0
            })
            {
                Metadata = request.Metadata
            });
            if (!createWallet.IsSuccess)
            {
                logger.LogWarning("Target wallet could not be created for converting wallet for {CredentialId}", request.CredentialId);
                return new CmdResponse { HttpStatusCode = HttpStatusCode.InternalServerError, Message = "Target wallet could not be created" };
            }

            targetWallet = createWallet.Response;
        }
        
        decimal totalDecrement = 0;
        decimal totalIncrement = 0;
        TransferDeductionType transferDeductionType;
        
        switch (request.TransferDeductionType)
        {
            // Check Transfer Deduction Type
            case TransferDeductionType.Default:
                var transferDeductionTypeConfig = await dbContext.Set<RegistryConfiguration>()
                    .Where(x => x.TenantId == tenant.Id)
                    .Where(x => x.Key == "Settings:Wallet:Convert:DeductionType")
                    .FirstOrDefaultAsync(cancellationToken);

                if (transferDeductionTypeConfig is null)
                {
                    logger.LogWarning("Transfer deduction type configuration not found for converting wallet for {CredentialId}", request.CredentialId);
                    return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Transfer deduction type configuration not found" };
                }
                
                if (!Enum.TryParse<TransferDeductionType>(transferDeductionTypeConfig.Value, out var transferDeductionTypeFromConfig))
                {
                    logger.LogWarning("Invalid transfer deduction type configuration for converting wallet for {CredentialId}", request.CredentialId);                    
                    return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid transfer deduction type configuration" };
                }
                
                switch (transferDeductionTypeFromConfig)
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
                transferDeductionType = transferDeductionTypeFromConfig;
                break;
            case TransferDeductionType.DeductFromSender:
                totalDecrement = request.TotalAmount + request.TotalFee;
                totalIncrement = request.TotalAmount;
                transferDeductionType = TransferDeductionType.DeductFromSender;
                break;
            case TransferDeductionType.DeductFromRecipient:
                totalDecrement = request.TotalAmount;
                totalIncrement = request.TotalAmount - request.TotalFee;
                transferDeductionType = TransferDeductionType.DeductFromRecipient;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        // Check if source wallet has enough balance
        if (sourceWallet.Balance < totalDecrement)
        {
            logger.LogWarning("Insufficient balance in source wallet for converting wallet for {CredentialId}", request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Insufficient balance" };
        }

        // Perform the conversion
        var previousSourceBalance = sourceWallet.Balance;
        var previousSourceTotalBalance = sourceWallet.TotalBalance.Value;
        var previousSourceCreditOnHoldBalance = sourceWallet.CreditOnHoldBalance;
        var previousSourceDebitOnHoldBalance = sourceWallet.DebitOnHoldBalance;
        
        var previousTargetBalance = targetWallet.Balance;
        var previousTargetTotalBalance = targetWallet.TotalBalance.Value;
        var previousTargetCreditOnHoldBalance = targetWallet.CreditOnHoldBalance;
        var previousTargetDebitOnHoldBalance = targetWallet.DebitOnHoldBalance;

        sourceWallet.Balance -= totalDecrement;
        sourceWallet.TransferableBalance -= totalDecrement;
        
        targetWallet.Balance += totalIncrement;
        targetWallet.TransferableBalance += totalIncrement;

        // Record the transactions
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

        dbContext.Set<WalletTransaction>().Add(sourceTransaction);
        dbContext.Set<WalletTransaction>().Add(targetTransaction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Wallet type conversion successful for {CredentialId}", request.CredentialId);

            return new CmdResponse { HttpStatusCode = HttpStatusCode.OK, Message = "Wallet type conversion successful" };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Concurrency conflict occurred while converting wallet for {CredentialId}", request.CredentialId);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while converting wallet for {CredentialId}", request.CredentialId);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}

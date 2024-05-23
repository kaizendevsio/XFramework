using System.Text;
using Microsoft.Extensions.Logging;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services;
using Microsoft.EntityFrameworkCore;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Core.DataAccess.Commands.Wallet;
using XFramework.Domain.Shared.Contracts;

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
        if (request.Amount <= 0 || request.Fee < 0 || request.ConvenienceFee < 0)
        {
            logger.LogWarning("Invalid amount or fee while converting wallet type for {CredentialId}", request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid amount or fee" };
        }

        // Check if Source and Target wallet type IDs are provided
        if (request.SourceWalletTypeId == Guid.Empty || request.TargetWalletTypeId == Guid.Empty)
        {
            logger.LogWarning("Source and target wallet type IDs are required for converting wallet type for {CredentialId}", request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Source and target wallet type IDs are required" };
        }

        // Fetch the source wallet
        var sourceWallet = await dbContext.Set<Wallet>()
            .Where(x => x.TenantId == tenant.Id)
            .Where(x => x.CredentialId == request.CredentialId)
            .Where(x => x.WalletTypeId == request.SourceWalletTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sourceWallet == null)
        {
            logger.LogWarning("Source wallet not found for converting wallet type for {CredentialId}", request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Source wallet not found" };
        }

        // Fetch or create the target wallet
        var targetWallet = await dbContext.Set<Wallet>()
            .Where(x => x.TenantId == tenant.Id)
            .Where(x => x.CredentialId == request.CredentialId)
            .Where(x => x.WalletTypeId == request.TargetWalletTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetWallet == null)
        {
            var createWallet = await mediator.Send(new Create<Wallet>(new()
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
                logger.LogWarning("Target wallet could not be created for converting wallet type for {CredentialId}", request.CredentialId);
                return new CmdResponse { HttpStatusCode = HttpStatusCode.InternalServerError, Message = "Target wallet could not be created" };
            }

            targetWallet = createWallet.Response;
        }

        // Check if source wallet has enough balance
        var totalDeduction = request.Amount + request.Fee + request.ConvenienceFee;
        if (sourceWallet.Balance < totalDeduction)
        {
            logger.LogWarning("Insufficient balance in source wallet for converting wallet type for {CredentialId}", request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Insufficient balance" };
        }

        // Perform the conversion
        var previousSourceBalance = sourceWallet.Balance;
        var previousTargetBalance = targetWallet.Balance;

        sourceWallet.Balance -= totalDeduction;
        targetWallet.Balance += request.Amount;

        // Record the transactions
        var sourceTransaction = new WalletTransaction
        {
            TenantId = tenant.Id,
            CredentialId = request.CredentialId,
            WalletId = sourceWallet.Id,
            Amount = totalDeduction,
            TransactionFee = request.Fee,
            ConvenienceFee = request.ConvenienceFee,
            PreviousBalance = previousSourceBalance,
            RunningBalance = sourceWallet.Balance,
            Remarks = request.Remarks,
            Description = $"Converted to {request.TargetWalletTypeId}",
            TransactionType = TransactionType.Debit,
            Held = false,
            Released = true,
            ReferenceNumber = request.ReferenceNumber
        };

        var targetTransaction = new WalletTransaction
        {
            TenantId = tenant.Id,
            CredentialId = request.CredentialId,
            WalletId = targetWallet.Id,
            Amount = request.Amount,
            PreviousBalance = previousTargetBalance,
            RunningBalance = targetWallet.Balance,
            Remarks = request.Remarks,
            Description = $"Converted from {request.SourceWalletTypeId}",
            TransactionType = TransactionType.Credit,
            Held = false,
            Released = true,
            ReferenceNumber = request.ReferenceNumber
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
            logger.LogError(ex, "Concurrency conflict occurred while converting wallet type for {CredentialId}", request.CredentialId);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while converting wallet type for {CredentialId}", request.CredentialId);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}

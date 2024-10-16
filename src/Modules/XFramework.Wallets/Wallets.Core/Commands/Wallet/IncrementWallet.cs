﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wallets.Domain.Shared.Contracts.Requests;
using XFramework.Core.Services;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Core.Commands.Wallet;

public class IncrementWallet(
    DbContext dbContext,
    ILogger<ReleaseTransaction> logger,
    ITenantService tenantService,
    IMediator mediator
)
    : IRequestHandler<IncrementWalletRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(IncrementWalletRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        if (request.TotalAmount <= 0)
        {
            logger.LogWarning("Invalid increment amount for wallet ID {WalletId}, wallet type ID {WalletTypeId}, credential ID {CredentialId}", request.WalletId, request.WalletTypeId, request.CredentialId);
            return new CmdResponse { HttpStatusCode = HttpStatusCode.BadRequest, Message = "Invalid increment amount" };
        }

        var wallet = request.WalletTypeId != Guid.Empty
            ? await dbContext.Set<XFramework.Domain.Shared.Contracts.Wallet>()
                .Where(w => w.TenantId == tenant.Id && w.WalletTypeId == request.WalletTypeId && w.CredentialId == request.CredentialId)
                .FirstOrDefaultAsync(cancellationToken)
            : await dbContext.Set<XFramework.Domain.Shared.Contracts.Wallet>()
                .Where(w => w.TenantId == tenant.Id && w.Id == request.WalletId)
                .FirstOrDefaultAsync(cancellationToken);

        if (wallet is null)
        {
            // if wallet does not exist, create a new wallet if wallet type ID is provided
            if (request.WalletTypeId != Guid.Empty)
            {
                var createWallet = await mediator.Send(new Create<XFramework.Domain.Shared.Contracts.Wallet>(new()
                {
                    CredentialId = request.CredentialId,
                    WalletTypeId = request.WalletTypeId,
                    Balance = 0
                })
                {
                    Metadata = request.Metadata
                });
               
                if (createWallet.IsSuccess is false)
                {
                    logger.LogWarning("Error creating wallet for wallet type ID {WalletTypeId}, credential ID {CredentialId}", request.WalletTypeId, request.CredentialId);
                    return new CmdResponse { HttpStatusCode = HttpStatusCode.InternalServerError, Message = "Error creating wallet" };
                }
                
                wallet = createWallet.Response;
            }
            else
            {
                logger.LogWarning("Wallet not found for wallet ID {WalletId}, wallet type ID {WalletTypeId}, credential ID {CredentialId}", request.WalletId, request.WalletTypeId, request.CredentialId);
                return new CmdResponse { HttpStatusCode = HttpStatusCode.NotFound, Message = "Wallet not found" };
            }
        }

        // Check if the amount is within the min transferable amount
        if (wallet.MinTransferRule.HasValue && request.TotalAmount < wallet.MinTransferRule.Value)
        {
            logger.LogWarning("Amount is below the minimum transferable amount for Wallet ID {WalletId}", wallet.Id);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must be at least {wallet.MinTransferRule.Value}"
            };
        }

        // Check if the amount is within the max transferable amount
        if (wallet.MaxTransferRule.HasValue && request.TotalAmount > wallet.MaxTransferRule.Value)
        {
            logger.LogWarning("Amount exceeds the maximum transferable amount for Wallet ID {WalletId}", wallet.Id);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Amount must not exceed {wallet.MaxTransferRule.Value}"
            };
        }

        // Increment the wallet balance
        var previousBalance = wallet.Balance;
        var previousTotalBalance = wallet.TotalBalance.Value;
        var previousCreditOnHoldBalance = wallet.CreditOnHoldBalance;
        var previousDebitOnHoldBalance = wallet.DebitOnHoldBalance;
        
        if (request.OnHold)
        {
            wallet.CreditOnHoldBalance += request.TotalAmount;
        }
        else
        {
            wallet.Balance += request.TotalAmount;
            wallet.TransferableBalance += request.TotalAmount;
        }

        // Ensure maintaining balance is upheld
        if (wallet.MaintainingBalanceRule.HasValue && wallet.Balance < wallet.MaintainingBalanceRule.Value)
        {
            logger.LogWarning("Incrementing would violate maintaining balance rule for Wallet ID {WalletId}", wallet.Id);
            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = $"Balance after increment must not drop below {wallet.MaintainingBalanceRule.Value}"
            };
        }

        // Record the transaction
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

        dbContext.Set<WalletTransaction>().Add(transaction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Wallet balance incremented successfully for Wallet ID {WalletId}", wallet.Id);

            return new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Wallet balance incremented successfully"
            };
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "Concurrency conflict occurred while incrementing wallet balance for Wallet ID {WalletId}", wallet.Id);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while incrementing wallet balance for Wallet ID {WalletId}", wallet.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
    }
}
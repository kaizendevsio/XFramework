using HealthEssentials.Domain.Generics.Constants;
using Messaging.Domain.Generic;
using Wallets.Integration.Drivers;
using XFramework.Domain.Contexts;
using XFramework.Domain.Generic.Contracts;
using XFramework.Integration.Abstractions;

namespace HealthEssentials.Core.DataAccess.Commands.Consultation;

public class ConcludeLiveConsultationHandler(
    DbContext dbContext,
    IIdentityServerServiceWrapper identityServerService,
    IMessageBusWrapper messageBusWrapper,
    IMessagingServiceWrapper messagingServiceWrapper,
    IWalletsServiceWrapper walletsServiceWrapper,
    IHostEnvironment hostEnvironment,
    ITenantService tenantService,
    ILogger<ConcludeLiveConsultationHandler> logger,
    IHelperService helperService
)
    : IRequestHandler<ConcludeLiveConsultationRequest, CmdResponse>
{
  
    public async Task<CmdResponse> Handle(ConcludeLiveConsultationRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);
        
        var dbTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        var jobOrder = await dbContext.Set<ConsultationJobOrder>()
            .Where(c => c.TenantId == tenant.Id)
            .Include(i => i.PatientConsultations)
            .ThenInclude(i => i.Patient)
            .Include(i => i.DoctorConsultationJobOrders)
            .ThenInclude(i => i.Doctor)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == request.Id, CancellationToken.None);
        if (jobOrder is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Id {request.Id} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            }; 
        }

        var credentialResponse = await identityServerService.IdentityCredential.Get(
            id: jobOrder.PatientConsultations.FirstOrDefault().Patient.CredentialId,
            includeNavigations: true,
            includes: new List<string>()
            {
                $"{nameof(IdentityCredential.IdentityContacts)}.{nameof(IdentityContact.Type)}",
            },
            tenantId: tenant.Id);
        if (credentialResponse.HttpStatusCode is not HttpStatusCode.OK)
        {
            return credentialResponse.Adapt<CmdResponse>();
        }
        
        var credential = credentialResponse.Response;
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Id {jobOrder.PatientConsultations.FirstOrDefault().Patient.CredentialId} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var contact = credential.IdentityContacts.FirstOrDefault(i => i.Type?.Name == "Phone")?.Value;
        if (contact is null)
        {
            return new ()
            {
                Message = $"Credential with Id {jobOrder.PatientConsultations.FirstOrDefault().Patient.CredentialId} has no phone contact",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        jobOrder.CompletedAt = DateTime.UtcNow;
        jobOrder.Status = (short?) TransactionStatus.Completed;
        
        // Get wallet transaction to release from onhold

        var walletTransactions = await walletsServiceWrapper.WalletTransaction.GetList(
            pageNumber: 1,
            pageSize: 10,
            filter: new List<QueryFilter>()
            {
                new QueryFilter
                {
                    PropertyName = nameof(WalletTransaction.ReferenceNumber),
                    Operation = QueryFilterOperation.Equal,
                    Value = jobOrder.ReferenceNumber
                }
            }
        );

        if (walletTransactions.IsSuccess is false)
        {
            logger.LogError("Failed to get wallet transactions for reference number {ReferenceNumber}", jobOrder.ReferenceNumber);
            return new ()
            {
                Message = $"Failed to process payment, please try again",
                HttpStatusCode = HttpStatusCode.InternalServerError
            };
        }
        
        if (walletTransactions.Response?.TotalItems == 0)
        {
            logger.LogError("No wallet transactions for reference number {ReferenceNumber}", jobOrder.ReferenceNumber);
            return new ()
            {
                Message = $"Failed to process payment, please try again",
                HttpStatusCode = HttpStatusCode.InternalServerError
            };
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogError(ex, "A concurrency conflict occurred while concluding consultation job order with Id {Id}", request.Id);
            throw new InvalidOperationException("A concurrency conflict occurred, please try again");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while concluding consultation job order with Id {Id}", request.Id);
            throw new InvalidOperationException("An error occurred while processing your request");
        }
        
        var tasks = new List<Task<CmdResponse>>();
        
        foreach (var walletTransaction in walletTransactions.Response?.Items)
        {
            logger.LogInformation("Releasing wallet transaction with Id {WalletTransactionId}", walletTransaction.Id);
            tasks.Add(walletsServiceWrapper.ReleaseTransaction(new() { Id = walletTransaction.Id }));
        }

        await Task.WhenAll(tasks);

        if (tasks.Any(i => i.Result.IsSuccess is false))
        {
            logger.LogError("Failed to release wallet transactions for reference number {ReferenceNumber}", jobOrder.ReferenceNumber);
            return new ()
            {
                Message = $"Failed to process payment, please try again",
                HttpStatusCode = HttpStatusCode.InternalServerError
            };
        }
        
        await dbTransaction.CommitAsync(cancellationToken);
        
        var publishObject = new PublishRequest<ConsultationJobOrder>(jobOrder);
        publishObject = helperService.RemoveCircularReference(publishObject);

        messageBusWrapper.PublishAsync(HealthEssentialsConstants.Event.ConcludeConsultation, credential.Id.ToString(), publishObject);
        
        if (!hostEnvironment.IsProduction())
        {
            return new ()
            {
                Message = $"Consultation Job Order with Id {request.Id} concluded successfully",
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }

        messagingServiceWrapper.CreateDirectMessage(new()
        {
            MessageType = MessageTypes.Sms,
            Sender = GenericSender.System,
            Recipient = contact,
            Subject = "Consultation Concluded",
            Intent = MessageIntents.Notification,
            Message = $"Thank you for using MyHealthInfo. Your consultation with {jobOrder.DoctorConsultationJobOrders.FirstOrDefault().Doctor.Name} has ended. Please check your MyHealthInfo account for more details.",
            IsScheduled = false
        });
        
        return new()
        {
            Message = $"Consultation Job Order with Id {request.Id} concluded successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
        };
    }
}
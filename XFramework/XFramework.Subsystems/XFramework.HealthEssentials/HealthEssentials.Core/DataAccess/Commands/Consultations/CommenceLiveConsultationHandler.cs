using HealthEssentials.Domain.Generics.Constants;
using Messaging.Domain.Generic;
using XFramework.Domain.Generic.Contracts;
using XFramework.Integration.Abstractions;

namespace HealthEssentials.Core.DataAccess.Commands.Consultations;

public class CommenceLiveConsultationHandler(
    DbContext healthEssentialsContext,
    ITenantService tenantService,
    IIdentityServerServiceWrapper identityServerService,
    IHostEnvironment hostEnvironment,
    IMessageBusWrapper messageBusWrapper,
    ILogger<CommenceLiveConsultationHandler> logger,
    IMessagingServiceWrapper messagingServiceWrapper,
    IHelperService helperService
    )
    : IRequestHandler<CommenceLiveConsultationRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(CommenceLiveConsultationRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);
        
        var jobOrder = await healthEssentialsContext.Set<ConsultationJobOrder>()
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

        if (jobOrder.PaymentStatus is not (short?) DepositStatus.Paid)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Id {request.Id} is not paid",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }
        
        jobOrder.StartedAt = DateTime.UtcNow;
        jobOrder.Status = (short?) TransactionStatus.OnGoing;
        
        await healthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        var publishObject = new PublishRequest<ConsultationJobOrder>(jobOrder);
        publishObject = helperService.RemoveCircularReference(publishObject);
        
        messageBusWrapper.PublishAsync(HealthEssentialsConstants.Event.CommenceConsultation, credential.Id.ToString(), publishObject);
        
        if (!hostEnvironment.IsProduction())
        {
            return new ()
            {
                Message = $"Consultation Job Order with Id {request.Id} has commenced",
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
        
        await messagingServiceWrapper.CreateDirectMessage(new()
        {
            MessageType = MessageTypes.Sms,
            Sender = GenericSender.System,
            Recipient = contact,
            Subject = "Consultation Commenced",
            Intent = MessageIntents.Notification,
            Message = $"Your consultation with Dr. {jobOrder.DoctorConsultationJobOrders.FirstOrDefault()?.Doctor.Name} has started. Please open the app to start the consultation.",
            IsScheduled = false
        });
        
        return new()
        {
            Message = $"Consultation Job Order with Id {request.Id} has been commenced",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}
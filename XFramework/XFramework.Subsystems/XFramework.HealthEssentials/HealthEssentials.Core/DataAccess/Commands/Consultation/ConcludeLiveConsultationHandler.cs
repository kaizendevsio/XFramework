﻿using HealthEssentials.Domain.Generics.Constants;
using HealthEssentials.Domain.Generics.Contracts;
using HealthEssentials.Domain.Generics.Contracts.Requests;
using IdentityServer.Integration.Drivers;
using Messaging.Domain.Generic;
using Microsoft.Extensions.Logging;
using XFramework.Core.Services;
using XFramework.Domain.Contexts;
using XFramework.Domain.Generic.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Consultation;

public class ConcludeLiveConsultationHandler(
    DbContext dbContext,
    IIdentityServerServiceWrapper identityServerService,
    IMessageBusWrapper messageBusWrapper,
    IMessagingServiceWrapper messagingServiceWrapper,
    IHostEnvironment hostEnvironment,
    ITenantService tenantService,
    ILogger<ConcludeLiveConsultationHandler> logger
)
    : IRequestHandler<ConcludeLiveConsultationRequest, CmdResponse>
{
  
    public async Task<CmdResponse> Handle(ConcludeLiveConsultationRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);
        
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

        var credentialResponse = await identityServerService.IdentityCredential.Get(jobOrder.PatientConsultations.FirstOrDefault().Patient.CredentialId, tenantId: tenant.Id);
        if (credentialResponse.HttpStatusCode is not HttpStatusCode.Accepted)
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
        
        await dbContext.SaveChangesAsync(cancellationToken);

        messageBusWrapper.PublishAsync(HealthEssentialsEvent.ConcludeConsultation, credential.Id.ToString(), new PublishRequest<ConsultationJobOrder>(jobOrder));
        
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
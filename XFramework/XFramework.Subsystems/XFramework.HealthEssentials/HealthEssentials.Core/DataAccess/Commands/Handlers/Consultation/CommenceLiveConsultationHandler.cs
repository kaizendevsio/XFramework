using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.Generics.Enums;
using Messaging.Integration.Interfaces;
using Microsoft.Extensions.Hosting;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CommenceLiveConsultationHandler : CommandBaseHandler, IRequestHandler<CommenceLiveConsultationCmd, CmdResponse<CommenceLiveConsultationCmd>>
{
    private readonly IMessagingServiceWrapper _messagingServiceWrapper;
    private readonly IHostEnvironment _hostEnvironment;

    public CommenceLiveConsultationHandler(IDataLayer dataLayer, IMessagingServiceWrapper messagingServiceWrapper, IHostEnvironment hostEnvironment)
    {
        _messagingServiceWrapper = messagingServiceWrapper;
        _hostEnvironment = hostEnvironment;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CommenceLiveConsultationCmd>> Handle(CommenceLiveConsultationCmd request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders
            .Include(i => i.PatientConsultations)
            .ThenInclude(i => i.Patient)
            .Include(i => i.DoctorConsultationJobOrders)
            .ThenInclude(i => i.Doctor)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", CancellationToken.None);
        if (jobOrder is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            }; 
        }

        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .Include(i => i.IdentityContacts)
            .ThenInclude(i => i.Entity)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == jobOrder.PatientConsultations.FirstOrDefault().Patient.CredentialId, CancellationToken.None);
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {jobOrder.PatientConsultations.FirstOrDefault().Patient.CredentialId} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var contact = credential.IdentityContacts.FirstOrDefault(i => i.Entity?.Name == "Phone")?.Value;
        if (contact is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {jobOrder.PatientConsultations.FirstOrDefault().Patient.CredentialId} has no phone contact",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        if (jobOrder.PaymentStatus is not (short?) DepositStatus.Paid)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.Guid} is not paid",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }
        
        jobOrder.StartedAt = DateTime.UtcNow;
        jobOrder.Status = (short?) TransactionStatus.OnGoing;
        
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        if (!_hostEnvironment.IsProduction())
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.Guid} has commenced",
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
        
        await _messagingServiceWrapper.CreateDirectMessage(new()
        {
            MessageType = Guid.Parse("f4fca110-790d-41d7-a0be-b5c699c9a9db"),
            Sender = "+630000000000",
            Recipient = contact,
            Subject = "Consultation Commenced",
            Intent = "Notification",
            Message = $"Your consultation with Dr. {jobOrder.DoctorConsultationJobOrders.FirstOrDefault()?.Doctor.Name} has started. Please open the app to start the consultation.",
            IsScheduled = false
        });
        
        return new()
        {
            Message = $"Consultation Job Order with Guid {request.Guid} has been commenced",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}
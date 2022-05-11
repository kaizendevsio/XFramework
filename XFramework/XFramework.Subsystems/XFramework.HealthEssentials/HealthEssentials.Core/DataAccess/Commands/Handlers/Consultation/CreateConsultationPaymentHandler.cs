using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using XFramework.Integration.Interfaces.Wrappers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationPaymentHandler : CommandBaseHandler, IRequestHandler<CreateConsultationPaymentCmd, CmdResponse<CreateConsultationPaymentCmd>>
{
    public IWalletServiceWrapper WalletServiceWrapper { get; }

    public CreateConsultationPaymentHandler(IDataLayer dataLayer, IWalletServiceWrapper walletServiceWrapper)
    {
        WalletServiceWrapper = walletServiceWrapper;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationPaymentCmd>> Handle(CreateConsultationPaymentCmd request, CancellationToken cancellationToken)
    {
        var doctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(i => i.Guid == $"{request.DoctorGuid}", CancellationToken.None);
        if (doctor is null)
        {
            return new ()
            {
                Message = $"Doctor with Guid {request.ConsultationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var patient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(i => i.Guid == $"{request.PatientGuid}", CancellationToken.None);
        if (patient is null)
        {
            return new ()
            {
                Message = $"Patient with Guid {request.ConsultationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var doctorCredential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Id == doctor.CredentialId, CancellationToken.None);
        if (doctorCredential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.ConsultationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var patientCredential = await _dataLayer.XnelSystemsContext.IdentityCredentials.FirstOrDefaultAsync(i => i.Id == doctor.CredentialId, CancellationToken.None);
        if (patientCredential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.ConsultationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        await WalletServiceWrapper.TransferWallet(new()
        {
            Recipient = doctorCredential.Guid,
            CredentialGuid = Guid.Parse(patientCredential.Guid),
            WalletEntityGuid = Guid.Parse("31f579b7-fe70-478e-a73e-bf3534bda568"),
            Amount = request.Amount,
            Remarks = $"Consultation Payment for {request.ConsultationGuid} (Dr.{doctor.Name})"
        });

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };

    }
}
using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class UpdatePatientAilmentDetailHandler : CommandBaseHandler, IRequestHandler<UpdatePatientAilmentDetailCmd, CmdResponse<UpdatePatientAilmentDetailCmd>>
{
    public UpdatePatientAilmentDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePatientAilmentDetailCmd>> Handle(UpdatePatientAilmentDetailCmd request, CancellationToken cancellationToken)
    {
        var existingDetail = await _dataLayer.HealthEssentialsContext.PatientAilmentDetails.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDetail is null)
        {
            return new ()
            {
                Message = $"Patient Ailment Detail with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedDetail = request.Adapt(existingDetail);

        if (request.PatientAilmentGuid is null)
        {
            var patientAilment = await _dataLayer.HealthEssentialsContext.PatientAilments.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientAilmentGuid}", CancellationToken.None);
            if (patientAilment is null)
            {
                return new ()
                {
                    Message = $"Patient Ailment with Guid {request.PatientAilmentGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDetail.PatientAilment = patientAilment;
        }

        if (request.ConsultationJobOrderGuid is null)
        {
            var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
            if (consultationJobOrder is null)
            {
                return new ()
                {
                    Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDetail.ConsultationJobOrder = consultationJobOrder;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedDetail);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient Ailment Detail with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}
using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class UpdatePatientLaboratoryHandler : CommandBaseHandler, IRequestHandler<UpdatePatientLaboratoryCmd, CmdResponse<UpdatePatientLaboratoryCmd>>
{
    public UpdatePatientLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePatientLaboratoryCmd>> Handle(UpdatePatientLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var existingPatientLaboratory = await _dataLayer.HealthEssentialsContext.PatientLaboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingPatientLaboratory is null)
        {
            return new ()
            {
                Message = $"Patient Laboratory with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedPatientLaboratory = request.Adapt(existingPatientLaboratory);

        if (request.PatientGuid is null)
        {
            var patient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientGuid}", CancellationToken.None);
            if (patient is null)
            {
                return new ()
                {
                    Message = $"Patient with Guid {request.PatientGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedPatientLaboratory.Patient = patient;
        }

        if (request.LaboratoryJobOrderGuid is null)
        {
            var laboratoryJobOrder = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryJobOrderGuid}", CancellationToken.None);
            if (laboratoryJobOrder is null)
            {
                return new ()
                {
                    Message = $"Laboratory Job Order with Guid {request.LaboratoryJobOrderGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            } 
            updatedPatientLaboratory.LaboratoryJobOrder = laboratoryJobOrder;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedPatientLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient Laboratory with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
        
    }
}
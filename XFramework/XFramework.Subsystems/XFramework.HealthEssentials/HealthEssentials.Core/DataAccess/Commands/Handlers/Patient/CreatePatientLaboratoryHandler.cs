using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientLaboratoryHandler : CommandBaseHandler, IRequestHandler<CreatePatientLaboratoryCmd, CmdResponse<CreatePatientLaboratoryCmd>>
{
    public CreatePatientLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePatientLaboratoryCmd>> Handle(CreatePatientLaboratoryCmd request, CancellationToken cancellationToken)
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
        
        var laboratoryJobOrder = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryJobOrderGuid}", CancellationToken.None);
        if (laboratoryJobOrder is null)
        {
            return new ()
            {
                Message = $"Laboratory Job Order with Guid {request.LaboratoryJobOrderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var patientLaboratory = request.Adapt<PatientLaboratory>();
        patientLaboratory.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        patientLaboratory.Patient = patient;
        patientLaboratory.LaboratoryJobOrder = laboratoryJobOrder;
        
        await _dataLayer.HealthEssentialsContext.PatientLaboratories.AddAsync(patientLaboratory, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(patientLaboratory.Guid);
        return new()
        {
            Message = $"Patient Laboratory with Guid {patientLaboratory.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}
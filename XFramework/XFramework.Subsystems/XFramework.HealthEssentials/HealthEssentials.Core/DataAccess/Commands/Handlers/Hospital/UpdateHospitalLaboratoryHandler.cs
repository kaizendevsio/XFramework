using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalLaboratoryHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalLaboratoryCmd, CmdResponse<UpdateHospitalLaboratoryCmd>>
{
    public UpdateHospitalLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalLaboratoryCmd>> Handle(UpdateHospitalLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var existingHospitalLaboratory = await _dataLayer.HealthEssentialsContext.HospitalLaboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingHospitalLaboratory is null)
        {
            return new ()
            {
                Message = $"Hospital Laboratory with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedHospitalLaboratory = request.Adapt(existingHospitalLaboratory);

        if (request.HospitalGuid is null)
        {
            var hospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalGuid}", CancellationToken.None);
            if (hospital is null)
            {
                return new ()
                {
                    Message = $"Hospital with Guid {request.HospitalGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalLaboratory.Hospital = hospital;
        }

        if (request.LaboratoryGuid is null)
        {
            var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryGuid}", CancellationToken.None);
            if (laboratory is null)
            {
                return new ()
                {
                    Message = $"Laboratory with Guid {request.LaboratoryGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalLaboratory.Laboratory = laboratory;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedHospitalLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"HospitalLaboratory with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}
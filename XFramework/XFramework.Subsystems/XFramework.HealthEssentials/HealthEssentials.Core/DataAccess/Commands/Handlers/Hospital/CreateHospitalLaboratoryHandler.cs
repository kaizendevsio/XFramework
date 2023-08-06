using System.Security.AccessControl;
using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalLaboratoryHandler : CommandBaseHandler, IRequestHandler<CreateHospitalLaboratoryCmd, CmdResponse<CreateHospitalLaboratoryCmd>>
{
    public CreateHospitalLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalLaboratoryCmd>> Handle(CreateHospitalLaboratoryCmd request, CancellationToken cancellationToken)
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
        
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryGuid}", CancellationToken.None);
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.LaboratoryGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var hospitalLaboratory = request.Adapt<HospitalLaboratory>();
        hospitalLaboratory.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        hospitalLaboratory.Hospital = hospital;
        hospitalLaboratory.Laboratory = laboratory;
        
        await _dataLayer.HealthEssentialsContext.HospitalLaboratories.AddAsync(hospitalLaboratory, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(hospitalLaboratory.Guid);
        return new()
        {
            Message = $"Hospital Laboratory with Guid {hospitalLaboratory.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}
using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalLaboratoryHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalLaboratoryCmd, CmdResponse<DeleteHospitalLaboratoryCmd>>
{
    public DeleteHospitalLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalLaboratoryCmd>> Handle(DeleteHospitalLaboratoryCmd request, CancellationToken cancellationToken)
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
        
        existingHospitalLaboratory.IsDeleted = true;
        existingHospitalLaboratory.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingHospitalLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital Laboratory with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}
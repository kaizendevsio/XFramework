using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalConsultationHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalConsultationCmd, CmdResponse<DeleteHospitalConsultationCmd>>
{
    public DeleteHospitalConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalConsultationCmd>> Handle(DeleteHospitalConsultationCmd request, CancellationToken cancellationToken)
    {
        var existingHospitalConsultation = await _dataLayer.HealthEssentialsContext.HospitalConsultations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingHospitalConsultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingHospitalConsultation.IsDeleted = true;
        existingHospitalConsultation.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingHospitalConsultation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Consultation with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}
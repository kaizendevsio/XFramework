using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientAilmentDetailHandler : CommandBaseHandler, IRequestHandler<DeletePatientAilmentDetailCmd, CmdResponse<DeletePatientAilmentDetailCmd>>
{
    public DeletePatientAilmentDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePatientAilmentDetailCmd>> Handle(DeletePatientAilmentDetailCmd request, CancellationToken cancellationToken)
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
        
        existingDetail.IsDeleted = true;
        existingDetail.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingDetail);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient Ailment Detail with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}
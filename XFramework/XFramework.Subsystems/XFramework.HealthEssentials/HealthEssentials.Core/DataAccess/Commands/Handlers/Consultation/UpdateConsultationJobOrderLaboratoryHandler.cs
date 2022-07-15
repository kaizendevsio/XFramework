using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationJobOrderLaboratoryHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationJobOrderLaboratoryCmd, CmdResponse<UpdateConsultationJobOrderLaboratoryCmd>>
{
    public UpdateConsultationJobOrderLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateConsultationJobOrderLaboratoryCmd>> Handle(UpdateConsultationJobOrderLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrderLaboratory = await _dataLayer.HealthEssentialsContext.ConsultationJobOrderLaboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingJobOrderLaboratory is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order Laboratory with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedJobOrderLaboratory = request.Adapt(existingJobOrderLaboratory);

        if (request.ConsultationJobOrderGuid is not null)
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
            updatedJobOrderLaboratory.ConsultationJobOrder = consultationJobOrder;
        }

        if (request.LaboratoryServiceGuid is not null)
        {
            var laboratoryService = await _dataLayer.HealthEssentialsContext.LaboratoryServices.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryServiceGuid}", CancellationToken.None);
            if (laboratoryService is null)
            {
                return new ()
                {
                    Message = $"Laboratory Service with Guid {request.LaboratoryServiceGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrderLaboratory.LaboratoryService = laboratoryService;
        }

        if (request.SuggestedLaboratoryLocationGuid is not null)
        {
            var laboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.SuggestedLaboratoryLocationGuid}", CancellationToken.None);
            if (laboratoryLocation is null)
            {
                return new ()
                {
                    Message = $"Laboratory Location with Guid {request.SuggestedLaboratoryLocationGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrderLaboratory.SuggestedLaboratoryLocation = laboratoryLocation;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedJobOrderLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Consultation Job Order Laboratory with Guid {updatedJobOrderLaboratory.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}
using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationJobOrderLaboratoryHandler : CommandBaseHandler, IRequestHandler<CreateConsultationJobOrderLaboratoryCmd, CmdResponse<CreateConsultationJobOrderLaboratoryCmd>>
{
    public CreateConsultationJobOrderLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationJobOrderLaboratoryCmd>> Handle(CreateConsultationJobOrderLaboratoryCmd request, CancellationToken cancellationToken)
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
        var consultationJobOrderLaboratory = request.Adapt<ConsultationJobOrderLaboratory>();

        var laboratoryService = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryServiceGuid}", CancellationToken.None);
        if (laboratoryService is null)
        {
            return new ()
            {
                Message = $"Laboratory Service with Guid {request.LaboratoryServiceGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        if (request.SuggestedLaboratoryLocationGuid is not null)
        {
            var laboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.SuggestedLaboratoryLocationGuid}", CancellationToken.None);
            if (laboratoryLocation is null)
            {
                return new()
                {
                    Message = $"Laboratory Location with Guid {request.SuggestedLaboratoryLocationGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            consultationJobOrderLaboratory.SuggestedLaboratoryLocation = laboratoryLocation;
        }

        consultationJobOrderLaboratory.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        consultationJobOrderLaboratory.ConsultationJobOrder = consultationJobOrder;
        consultationJobOrderLaboratory.LaboratoryService = laboratoryService;

        await _dataLayer.HealthEssentialsContext.ConsultationJobOrderLaboratories.AddAsync(consultationJobOrderLaboratory, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(consultationJobOrderLaboratory.Guid);
        return new ()
        {
            Message = $"Consultation Job Order Laboratory with Guid {consultationJobOrderLaboratory.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}
using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Core.Interfaces;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationTypeGroupHandler : CommandBaseHandler, IRequestHandler<CreateConsultationTypeGroupCmd, CmdResponse<CreateConsultationTypeGroupCmd>>
{
    public CreateConsultationTypeGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationTypeGroupCmd>> Handle(CreateConsultationTypeGroupCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.ConsultationEntityGroup>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        _dataLayer.HealthEssentialsContext.ConsultationEntityGroups.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation type group with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}
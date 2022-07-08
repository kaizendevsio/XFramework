using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateConsultationEntityGroupCmd, CmdResponse<CreateConsultationEntityGroupCmd>>
{
    public CreateConsultationEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationEntityGroupCmd>> Handle(CreateConsultationEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.ConsultationEntityGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        await _dataLayer.HealthEssentialsContext.ConsultationEntityGroups.AddAsync(group,CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Consultation entity group with Guid {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}
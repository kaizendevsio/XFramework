using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class CreateAilmentHandler : CommandBaseHandler, IRequestHandler<CreateAilmentCmd, CmdResponse<CreateAilmentCmd>>
{
    public CreateAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateAilmentCmd>> Handle(CreateAilmentCmd request, CancellationToken cancellationToken)
    {
        var ailmentEntity = await _dataLayer.HealthEssentialsContext.AilmentEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (ailmentEntity == null)
        {
            return new ()
            {
                Message = $"Ailment entity with Guid {request.EntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var ailment = request.Adapt<Domain.Generics.Contracts.Ailment>();
        ailment.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        ailment.Type = ailmentEntity;

        await _dataLayer.HealthEssentialsContext.Ailments.AddAsync(ailment, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(ailment.Guid);
        return new()
        {
            Message = $"Ailment with Guid {ailment.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}
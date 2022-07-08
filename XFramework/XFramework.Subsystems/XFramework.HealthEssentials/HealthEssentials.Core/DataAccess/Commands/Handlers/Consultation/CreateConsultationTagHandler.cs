using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationTagHandler : CommandBaseHandler, IRequestHandler<CreateConsultationTagCmd, CmdResponse<CreateConsultationTagCmd>>
{
    public CreateConsultationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationTagCmd>> Handle(CreateConsultationTagCmd request, CancellationToken cancellationToken)
    {
        var consultation = await _dataLayer.HealthEssentialsContext.Consultations
            .FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationGuid}", CancellationToken.None);
        
        if (consultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.ConsultationGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags
            .FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.TagGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var consulationTag = request.Adapt<ConsultationTag>();
        consulationTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        consulationTag.Consultation = consultation;
        consulationTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.ConsultationTags.AddAsync(consulationTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(consulationTag.Guid);
        return new ()
        {
            Message = $"Consultation Tag with Guid {request.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}
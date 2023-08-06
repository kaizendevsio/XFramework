using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationTagHandler : CommandBaseHandler, IRequestHandler<CreateConsultationTagCmd, CmdResponse<CreateConsultationTagCmd>>
{
    public CreateConsultationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationTagCmd>> Handle(CreateConsultationTagCmd request, CancellationToken cancellationToken)
    {
        var consultation = await _dataLayer.HealthEssentialsContext.Consultations.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationGuid}", CancellationToken.None);
        if (consultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.ConsultationGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.TagGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var consultationTag = request.Adapt<ConsultationTag>();
        consultationTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        consultationTag.Consultation = consultation;
        consultationTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.ConsultationTags.AddAsync(consultationTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(consultationTag.Guid);
        return new ()
        {
            Message = $"Consultation Tag with Guid {consultationTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}
using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationTagHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationTagCmd, CmdResponse<UpdateConsultationTagCmd>>
{
    public UpdateConsultationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateConsultationTagCmd>> Handle(UpdateConsultationTagCmd request, CancellationToken cancellationToken)
    {
        var existingTag = await _dataLayer.HealthEssentialsContext.ConsultationTags
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingTag is null)
        {
            return new ()
            {
                Message = $"Consultation Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
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

        var updatedTag = request.Adapt(existingTag);
        updatedTag.Consultation = consultation;
        updatedTag.Tag = tag;

        _dataLayer.HealthEssentialsContext.Update(updatedTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Consultation Tag with Guid {updatedTag.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedTag.Guid)
            }
        };
    }
}
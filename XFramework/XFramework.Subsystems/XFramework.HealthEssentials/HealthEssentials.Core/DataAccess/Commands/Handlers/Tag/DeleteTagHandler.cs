using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Tag;

public class DeleteTagHandler : CommandBaseHandler, IRequestHandler<DeleteTagCmd, CmdResponse<DeleteTagCmd>>
{
    public DeleteTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteTagCmd>> Handle(DeleteTagCmd request, CancellationToken cancellationToken)
    {
        var existingTag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingTag == null)
        {
            return new()
            {
                Message = $"Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingTag.IsDeleted = true;
        existingTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new()
        {
            Message = $"Tag with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}
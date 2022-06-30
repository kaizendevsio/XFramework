using Community.Core.DataAccess.Commands.Entity.Content;

namespace Community.Core.DataAccess.Commands.Handlers.Content;

public class DeleteContentHandler : CommandBaseHandler, IRequestHandler<DeleteContentCmd, CmdResponse>
{
    public DeleteContentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(DeleteContentCmd request, CancellationToken cancellationToken)
    {
        var content = await _dataLayer.CommunityContents
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (content == null)
        {
            return new ()
            {
                Message = $"Content with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        content.IsDeleted = true;
        content.IsEnabled = false;
        
        _dataLayer.CommunityContents.Update(content);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}
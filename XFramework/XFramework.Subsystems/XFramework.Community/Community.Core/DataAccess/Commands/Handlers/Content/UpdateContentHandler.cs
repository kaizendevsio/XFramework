using Community.Core.DataAccess.Commands.Entity.Content;

namespace Community.Core.DataAccess.Commands.Handlers.Content;

public class UpdateContentHandler : CommandBaseHandler, IRequestHandler<UpdateContentCmd, CmdResponse>
{
    public UpdateContentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(UpdateContentCmd request, CancellationToken cancellationToken)
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

        CommunityIdentity? communityIdentity = null;
        if (request.SocialMediaIdentityGuid is not null)
        {
            communityIdentity = await _dataLayer.CommunityIdentities
                .FirstOrDefaultAsync(i => i.Guid == $"{request.SocialMediaIdentityGuid}", cancellationToken: cancellationToken);
            if (communityIdentity == null)
            {
                return new ()
                {
                    Message = $"Community identity with guid {request.SocialMediaIdentityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    IsSuccess = false
                };
            }
        }

        var updatedContent = request.Adapt(content);
        if (request.SocialMediaIdentityGuid is not null)
        {
            updatedContent.SocialMediaIdentity = communityIdentity;
        }

        _dataLayer.CommunityContents.Update(content);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}
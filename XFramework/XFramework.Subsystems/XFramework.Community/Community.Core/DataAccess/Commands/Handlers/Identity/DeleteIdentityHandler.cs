using Community.Core.DataAccess.Commands.Entity.Identity;

namespace Community.Core.DataAccess.Commands.Handlers.Identity;

public class DeleteIdentityHandler : CommandBaseHandler, IRequestHandler<DeleteIdentityCmd, CmdResponse>
{
    public DeleteIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse> Handle(DeleteIdentityCmd request, CancellationToken cancellationToken)
    {
        var communityIdentity = await _dataLayer.CommunityIdentities.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken: cancellationToken);
        if (communityIdentity == null)
        {
            return new ()
            {
                Message = $"Community identity with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        communityIdentity.IsDeleted = true;
        
        _dataLayer.CommunityIdentities.Update(communityIdentity);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}